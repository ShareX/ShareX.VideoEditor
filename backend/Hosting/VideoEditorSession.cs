#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Photino.NET;
using ShareX.VideoEditor.Core;
using ShareX.VideoEditor.Hosting.Bridge;
using ShareX.VideoEditor.Hosting.Diagnostics;

namespace ShareX.VideoEditor.Hosting;

/// <summary>
/// Manages one video editor session: owns the <see cref="PhotinoWindow"/>,
/// the C# ↔ JS message bridge, and the FFmpeg pipeline.
/// </summary>
internal sealed class VideoEditorSession
{
    private const string MediaScheme = "sharexmedia";

    private readonly VideoEditorOptions _options;
    private readonly VideoEditorEvents? _events;
    private readonly string _ffmpegPath;
    private readonly bool _ffmpegAvailable;
    private readonly string _ffprobePath;
    private readonly bool _ffprobeAvailable;
    private readonly object _operationGate = new();
    private readonly HashSet<string> _selectedWatermarkPaths = new(StringComparer.OrdinalIgnoreCase);

    private PhotinoWindow? _window;
    private CancellationTokenSource? _exportCts;
    private CancellationTokenSource? _thumbnailCts;
    private string? _activeExportRequestId;
    private string? _activeThumbnailRequestId;
    private int _activeThumbnailRevision;

    public VideoEditorSession(VideoEditorOptions options, VideoEditorEvents? events)
    {
        _options = options;
        _events = events;
        _ffmpegPath = NormalizeExecutablePath(options.FFmpegPath);
        _ffmpegAvailable = !string.IsNullOrWhiteSpace(_ffmpegPath) && File.Exists(_ffmpegPath);
        _ffprobePath = NormalizeExecutablePath(options.FFprobePath);
        _ffprobeAvailable = !string.IsNullOrWhiteSpace(_ffprobePath) && File.Exists(_ffprobePath);
    }

    public void Run()
    {
        string indexHtml = ResolveWebUiPath();
        VideoEditorRuntimeValidator.EnsureAvailable();

        using var linuxWaylandMitigation = LinuxWaylandExplicitSyncMitigationScope.Create(
            _options.EnableLinuxWaylandExplicitSyncMitigation);

        try
        {
            // file:// UI + file:// video requires web-security off so WebView2/WebKit
            // can play the local recording. Custom schemes cannot supply Range headers.
            _window = new PhotinoWindow()
                .SetTitle(_options.WindowTitle ?? "ShareX — Video Editor")
                .SetSize(1280, 800)
                .SetMinSize(900, 600)
                .SetResizable(true)
                .SetChromeless(false)
                .SetWebSecurityEnabled(false)
                .SetFileSystemAccessEnabled(true)
                .SetMediaAutoplayEnabled(true)
                .RegisterCustomSchemeHandler(MediaScheme, ServeMediaFile)
                .RegisterWebMessageReceivedHandler(OnWebMessage);

            _window.Load(new Uri(indexHtml));
            _window.WaitForClose();

            try { _events?.EditorClosed?.Invoke(); } catch { }
        }
        catch (Exception ex)
        {
            throw VideoEditorRuntimeValidator.NormalizeStartupException(ex);
        }
        finally
        {
            try { _window?.Close(); } catch { }
            _window = null;

            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _exportCts?.Cancel();
            _exportCts?.Dispose();
        }
    }

    private sealed class LinuxWaylandExplicitSyncMitigationScope : IDisposable
    {
        private const string GdkDebugEnvironmentVariable = "GDK_DEBUG";
        private const string MitigationToken = "no-explicit-sync";

        private readonly string? _previousValue;
        private readonly bool _changedValue;

        private LinuxWaylandExplicitSyncMitigationScope(string? previousValue, bool changedValue)
        {
            _previousValue = previousValue;
            _changedValue = changedValue;
        }

        public static LinuxWaylandExplicitSyncMitigationScope? Create(bool enabled)
        {
            if (!enabled || !OperatingSystem.IsLinux() || !IsWaylandSession())
            {
                return null;
            }

            string? currentValue = Environment.GetEnvironmentVariable(GdkDebugEnvironmentVariable);
            if (ContainsToken(currentValue, MitigationToken))
            {
                return new LinuxWaylandExplicitSyncMitigationScope(currentValue, false);
            }

            string updatedValue = string.IsNullOrWhiteSpace(currentValue)
                ? MitigationToken
                : $"{currentValue},{MitigationToken}";

            Environment.SetEnvironmentVariable(GdkDebugEnvironmentVariable, updatedValue);
            return new LinuxWaylandExplicitSyncMitigationScope(currentValue, true);
        }

        public void Dispose()
        {
            if (!_changedValue)
            {
                return;
            }

            Environment.SetEnvironmentVariable(GdkDebugEnvironmentVariable, _previousValue);
        }

        private static bool IsWaylandSession()
        {
            string? sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        }

        private static bool ContainsToken(string? value, string token)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Any(part => string.Equals(part.Trim(), token, StringComparison.OrdinalIgnoreCase));
        }
    }

    private Stream ServeMediaFile(object sender, string scheme, string url, out string contentType)
    {
        contentType = GetVideoMimeType(_options.VideoPath);
        try
        {
            return new FileStream(_options.VideoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            VideoEditorServices.ReportError(nameof(VideoEditorSession), "Failed to open video for streaming.", ex);
            contentType = "application/octet-stream";
            return Stream.Null;
        }
    }

    private void OnWebMessage(object? sender, string message)
    {
        try
        {
            BridgeMessageValidationResult validation = BridgeMessageValidator.Validate(message);
            if (!validation.IsValid || validation.Message == null)
            {
                SendBridgeError(validation.RequestId, validation.Error ?? "Invalid bridge message.");
                return;
            }

            JObject obj = validation.Message;

            switch (validation.Type)
            {
                case "ready":
                    SendConfig();
                    break;

                case "requestExport":
                    var payload = obj.ToObject<ExportPayload>()!;
                    HandleExportRequest(payload);
                    break;

                case "requestThumbnails":
                    StartThumbnailExtraction(obj.ToObject<ThumbnailRequestPayload>()!);
                    break;

                case "requestWatermarkImage":
                    HandleWatermarkImageRequest(validation.RequestId);
                    break;

                case "cancelExport":
                    CancelExport(validation.RequestId!);
                    break;
            }
        }
        catch (Exception ex)
        {
            VideoEditorServices.ReportError(nameof(VideoEditorSession), "Error processing bridge message.", ex);
            SendBridgeError(null, "Bridge message processing failed.");
        }
    }

    private void Send(object payload)
    {
        try
        {
            _window?.SendWebMessage(JsonConvert.SerializeObject(payload));
        }
        catch (Exception ex)
        {
            VideoEditorServices.ReportWarning(nameof(VideoEditorSession), "Failed to send bridge message.", ex);
        }
    }

    private void SendBridgeError(string? requestId, string message)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            Send(new { type = "bridgeError", message });
        }
        else
        {
            Send(new { type = "bridgeError", requestId, message });
        }
    }

    private void SendConfig()
    {
        string videoUrl = new Uri(_options.VideoPath).AbsoluteUri;

        if (_ffmpegAvailable)
        {
            VideoEditorServices.ReportInformation(
                nameof(VideoEditorSession),
                $"Using FFmpeg path '{_ffmpegPath}'.");
        }
        else
        {
            string configuredPath = string.IsNullOrWhiteSpace(_ffmpegPath) ? "(not set)" : _ffmpegPath;
            VideoEditorServices.ReportWarning(
                nameof(VideoEditorSession),
                $"FFmpeg is unavailable. Configured path: {configuredPath}");
        }

        if (_ffprobeAvailable)
        {
            VideoEditorServices.ReportInformation(
                nameof(VideoEditorSession),
                $"Using FFprobe path '{_ffprobePath}'.");
        }
        else if (!string.IsNullOrWhiteSpace(_ffprobePath))
        {
            VideoEditorServices.ReportWarning(
                nameof(VideoEditorSession),
                $"FFprobe path does not exist: {_ffprobePath}");
        }

        IReadOnlyList<string> availableFormats = _ffmpegAvailable
            ? FfmpegCapabilityProbe.Probe(_ffmpegPath).AvailableFormats
            : [];

        Send(new
        {
            type = "config",
            protocolVersion = 2,
            videoUrl,
            theme = _options.Theme,
            culture = _options.Culture ?? string.Empty,
            ffmpegAvailable = _ffmpegAvailable,
            ffmpegPath = _ffmpegPath,
            ffprobeAvailable = _ffprobeAvailable,
            ffprobePath = _ffprobePath,
            availableFormats,
            runtimeDiagnostics = VideoEditorRuntimeDiagnosticsCollector.Capture(),
            watermark = _options.WatermarkSettings != null ? new
            {
                enabled = _options.WatermarkSettings.Enabled,
                text = _options.WatermarkSettings.Text,
                imagePath = _options.WatermarkSettings.ImagePath,
                imageUrl = ToFileUrl(_options.WatermarkSettings.ImagePath),
                opacity = _options.WatermarkSettings.Opacity,
                positionX = _options.WatermarkSettings.PositionX,
                positionY = _options.WatermarkSettings.PositionY,
                fontSize = _options.WatermarkSettings.FontSize,
                fontColor = _options.WatermarkSettings.FontColor
            } : null
        });
    }

    private void StartThumbnailExtraction(ThumbnailRequestPayload payload)
    {
        if (!_ffmpegAvailable)
        {
            VideoEditorServices.ReportWarning(nameof(VideoEditorSession),
                "FFmpegPath is not set or does not exist — thumbnails will not be generated.");
            SendBridgeError(payload.RequestId, "FFmpeg is not available for thumbnail extraction.");
            return;
        }

        CancellationTokenSource thumbnailCts;
        lock (_operationGate)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = new CancellationTokenSource();
            thumbnailCts = _thumbnailCts;
            _activeThumbnailRequestId = payload.RequestId;
            _activeThumbnailRevision = payload.Revision;
        }

        CancellationToken token = thumbnailCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var extractor = new ThumbnailExtractor(_ffmpegPath);
                _ = await extractor.ExtractThumbnailBatchesAsync(
                    _options.VideoPath,
                    payload.StartTime,
                    payload.EndTime,
                    payload.Count,
                    96,
                    54,
                    batch =>
                    {
                        if (IsActiveThumbnailRequest(payload.RequestId, payload.Revision, thumbnailCts))
                        {
                            Send(new
                            {
                                type = "thumbnailBatch",
                                requestId = payload.RequestId,
                                revision = payload.Revision,
                                startIndex = batch.StartIndex,
                                totalCount = payload.Count,
                                frames = batch.Frames,
                                isComplete = batch.IsComplete
                            });
                        }
                    },
                    token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                VideoEditorServices.ReportWarning(nameof(VideoEditorSession), "Thumbnail extraction failed.", ex);
                if (IsActiveThumbnailRequest(payload.RequestId, payload.Revision, thumbnailCts))
                {
                    SendBridgeError(payload.RequestId, ex.Message);
                }
            }
        }, CancellationToken.None);
    }

    private void HandleExportRequest(ExportPayload payload)
    {
        if (!_ffmpegAvailable)
        {
            VideoEditorServices.ReportWarning(nameof(VideoEditorSession),
                "Export requested without an available FFmpeg path.");
            Send(new { type = "exportError", requestId = payload.RequestId, message = "FFmpeg is not available." });
            return;
        }

        if (!IsAllowedWatermarkPath(payload.WatermarkImagePath))
        {
            Send(new
            {
                type = "exportError",
                requestId = payload.RequestId,
                message = "Watermark image path was not selected by this editor session."
            });
            return;
        }

        lock (_operationGate)
        {
            if (_activeExportRequestId != null)
            {
                Send(new
                {
                    type = "exportError",
                    requestId = payload.RequestId,
                    message = "Another export is already in progress."
                });
                return;
            }

            _activeExportRequestId = payload.RequestId;
        }

        string? outputPath;
        try
        {
            outputPath = ResolveExportOutputPath(payload);
        }
        catch (Exception ex)
        {
            ClearActiveExport(payload.RequestId);
            VideoEditorServices.ReportError(nameof(VideoEditorSession), "Failed to select export destination.", ex);
            Send(new { type = "exportError", requestId = payload.RequestId, message = ex.Message });
            return;
        }
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Send(new { type = "exportCancelled", requestId = payload.RequestId });
            ClearActiveExport(payload.RequestId);
            return;
        }

        var exportCts = new CancellationTokenSource();
        lock (_operationGate)
        {
            _exportCts?.Dispose();
            _exportCts = exportCts;
        }
        CancellationToken token = exportCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var exportOptions = BuildExportOptions(payload, outputPath);
                var service = new VideoExportService(_ffmpegPath);

                await service.ExportAsync(
                    exportOptions,
                    progress => Send(new
                    {
                        type = "exportProgress",
                        requestId = payload.RequestId,
                        percent = progress.ProgressPercent,
                        message = progress.StatusMessage
                    }),
                    token);

                ClearActiveExport(payload.RequestId);
                Send(new { type = "exportComplete", requestId = payload.RequestId, outputPath });
                try { _events?.ExportCompleted?.Invoke(outputPath); } catch { }

                if (_options.CloseAfterExport)
                {
                    try { _window?.Close(); } catch { }
                }
            }
            catch (OperationCanceledException)
            {
                ClearActiveExport(payload.RequestId);
                Send(new { type = "exportCancelled", requestId = payload.RequestId });
            }
            catch (Exception ex)
            {
                ClearActiveExport(payload.RequestId);
                VideoEditorServices.ReportError(nameof(VideoEditorSession), "Export failed.", ex);
                Send(new { type = "exportError", requestId = payload.RequestId, message = ex.Message });
                try { _events?.ExportFailed?.Invoke(ex); } catch { }
            }
            finally
            {
                ClearActiveExport(payload.RequestId);
                exportCts.Dispose();
            }
        }, CancellationToken.None);
    }

    private string? ResolveExportOutputPath(ExportPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(_options.OutputPath))
        {
            string configuredPath = Path.GetFullPath(_options.OutputPath);
            string? directory = Path.GetDirectoryName(configuredPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return configuredPath;
        }

        string ext = GetExtension(payload.OutputFormat);
        string suggestedName = Path.GetFileNameWithoutExtension(_options.VideoPath) + "_edited." + ext;
        string defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(defaultDirectory))
        {
            defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        string defaultPath = Path.Combine(defaultDirectory, suggestedName);
        return _window?.ShowSaveFile(
            "Export Video",
            defaultPath,
            [(payload.OutputFormat + " File", new[] { "*." + ext })]);
    }

    private void HandleWatermarkImageRequest(string? requestId)
    {
        string[]? selected = _window?.ShowOpenFile(
            "Select watermark image",
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            false,
            [("Image files", new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" })]);

        string? path = selected is { Length: > 0 } ? selected[0] : null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Send(new { type = "watermarkImageSelected", requestId, path = string.Empty, imageUrl = string.Empty });
            return;
        }

        lock (_operationGate)
        {
            _selectedWatermarkPaths.Add(Path.GetFullPath(path));
        }

        Send(new
        {
            type = "watermarkImageSelected",
            requestId,
            path,
            imageUrl = ToFileUrl(path)
        });
    }

    private void CancelExport(string requestId)
    {
        CancellationTokenSource? exportCts;
        lock (_operationGate)
        {
            if (!string.Equals(_activeExportRequestId, requestId, StringComparison.Ordinal))
            {
                Send(new
                {
                    type = "exportError",
                    requestId,
                    message = "No active export matches this requestId."
                });
                return;
            }

            exportCts = _exportCts;
        }

        exportCts?.Cancel();
    }

    private void ClearActiveExport(string requestId)
    {
        lock (_operationGate)
        {
            if (string.Equals(_activeExportRequestId, requestId, StringComparison.Ordinal))
            {
                _activeExportRequestId = null;
                _exportCts = null;
            }
        }
    }

    private bool IsActiveThumbnailRequest(
        string requestId,
        int revision,
        CancellationTokenSource thumbnailCts)
    {
        lock (_operationGate)
        {
            return ReferenceEquals(_thumbnailCts, thumbnailCts) &&
                   !thumbnailCts.IsCancellationRequested &&
                   string.Equals(_activeThumbnailRequestId, requestId, StringComparison.Ordinal) &&
                   _activeThumbnailRevision == revision;
        }
    }

    private bool IsAllowedWatermarkPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        string? configuredPath = _options.WatermarkSettings?.ImagePath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            try
            {
                if (string.Equals(
                    normalized,
                    Path.GetFullPath(configuredPath),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    return File.Exists(normalized);
                }
            }
            catch
            {
            }
        }

        lock (_operationGate)
        {
            return File.Exists(normalized) && _selectedWatermarkPaths.Contains(normalized);
        }
    }

    private VideoExportOptions BuildExportOptions(ExportPayload payload, string outputPath)
    {
        WatermarkSettings? watermark = null;
        if (payload.WatermarkEnabled)
        {
            watermark = CloneWatermark(_options.WatermarkSettings);
            watermark.Enabled = true;
            if (!string.IsNullOrWhiteSpace(payload.WatermarkText))
            {
                watermark.Text = payload.WatermarkText;
            }

            if (!string.IsNullOrWhiteSpace(payload.WatermarkImagePath))
            {
                watermark.ImagePath = payload.WatermarkImagePath;
            }
        }

        return new VideoExportOptions
        {
            InputPath = _options.VideoPath,
            OutputPath = outputPath,
            OutputFormat = payload.OutputFormat,
            IsTrimActive = payload.IsTrimActive,
            TrimStart = TimeSpan.FromSeconds(payload.TrimStart),
            TrimEnd = TimeSpan.FromSeconds(payload.TrimEnd),
            IsCropActive = payload.IsCropActive,
            CropX = payload.CropX,
            CropY = payload.CropY,
            CropWidth = payload.CropWidth,
            CropHeight = payload.CropHeight,
            OutputFps = payload.Fps,
            QualityScale = payload.QualityScale,
            Watermark = watermark,
            WatermarkText = payload.WatermarkEnabled ? payload.WatermarkText : string.Empty
        };
    }

    private static WatermarkSettings CloneWatermark(WatermarkSettings? source)
    {
        if (source == null)
        {
            return new WatermarkSettings { Enabled = true };
        }

        return new WatermarkSettings
        {
            Enabled = source.Enabled,
            Text = source.Text,
            ImagePath = source.ImagePath,
            Opacity = source.Opacity,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            FontSize = source.FontSize,
            FontColor = source.FontColor
        };
    }

    private static string ToFileUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveWebUiPath()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(VideoEditorHost).Assembly.Location)
            ?? AppContext.BaseDirectory;

        foreach (string candidate in EnumerateWebUiCandidates(assemblyDir))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        string defaultCandidate = Path.Combine(assemblyDir, "frontend", "dist", "index.html");

        throw new FileNotFoundException(
            "Frontend dist not found. Run 'npm run build' inside frontend first.", defaultCandidate);
    }

    private static IEnumerable<string> EnumerateWebUiCandidates(string assemblyDir)
    {
        yield return Path.Combine(assemblyDir, "frontend", "dist", "index.html");

        string? dir = assemblyDir;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir == null)
                yield break;

            yield return Path.Combine(dir, "frontend", "dist", "index.html");
            yield return Path.Combine(dir, "ShareX.VideoEditor", "frontend", "dist", "index.html");
        }
    }

    private static string NormalizeExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalizedPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"', '\''));
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return string.Empty;

        try
        {
            return Path.GetFullPath(normalizedPath);
        }
        catch
        {
            return normalizedPath;
        }
    }

    private static string GetExtension(string format) => format.ToUpperInvariant() switch
    {
        "WEBM" => "webm",
        "GIF" => "gif",
        "WEBP" => "webp",
        _ => "mp4"
    };

    private static string GetVideoMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".webm" => "video/webm",
            ".ogv" => "video/ogg",
            ".mov" => "video/quicktime",
            _ => "video/mp4"
        };
}
