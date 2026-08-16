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

    private PhotinoWindow? _window;
    private CancellationTokenSource? _exportCts;
    private CancellationTokenSource? _thumbnailCts;

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
            var obj = JObject.Parse(message);
            string? type = obj["type"]?.Value<string>();

            switch (type)
            {
                case "ready":
                    SendConfig();
                    StartThumbnailExtraction();
                    break;

                case "requestExport":
                    var payload = obj.ToObject<ExportPayload>() ?? new ExportPayload();
                    HandleExportRequest(payload);
                    break;

                case "cancelExport":
                    _exportCts?.Cancel();
                    break;
            }
        }
        catch (Exception ex)
        {
            VideoEditorServices.ReportError(nameof(VideoEditorSession), "Error processing bridge message.", ex);
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

        Send(new
        {
            type = "config",
            videoUrl,
            theme = _options.Theme,
            culture = _options.Culture ?? string.Empty,
            ffmpegAvailable = _ffmpegAvailable,
            ffmpegPath = _ffmpegPath,
            ffprobeAvailable = _ffprobeAvailable,
            ffprobePath = _ffprobePath,
            runtimeDiagnostics = VideoEditorRuntimeDiagnosticsCollector.Capture(),
            watermark = _options.WatermarkSettings != null ? new
            {
                enabled = _options.WatermarkSettings.Enabled,
                text = _options.WatermarkSettings.Text,
                imagePath = _options.WatermarkSettings.ImagePath,
                opacity = _options.WatermarkSettings.Opacity,
                positionX = _options.WatermarkSettings.PositionX,
                positionY = _options.WatermarkSettings.PositionY,
                fontSize = _options.WatermarkSettings.FontSize,
                fontColor = _options.WatermarkSettings.FontColor
            } : null
        });
    }

    private void StartThumbnailExtraction()
    {
        if (!_ffmpegAvailable)
        {
            VideoEditorServices.ReportWarning(nameof(VideoEditorSession),
                "FFmpegPath is not set or does not exist — thumbnails will not be generated.");
            return;
        }

        _thumbnailCts?.Cancel();
        _thumbnailCts = new CancellationTokenSource();
        var token = _thumbnailCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var extractor = new ThumbnailExtractor(_ffmpegPath);
                var frames = await extractor.ExtractThumbnailsAsync(
                    _options.VideoPath, count: 24, cancellationToken: token);

                Send(new { type = "thumbnails", frames });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                VideoEditorServices.ReportWarning(nameof(VideoEditorSession), "Thumbnail extraction failed.", ex);
            }
        }, token);
    }

    private void HandleExportRequest(ExportPayload payload)
    {
        if (!_ffmpegAvailable)
        {
            VideoEditorServices.ReportWarning(nameof(VideoEditorSession),
                "Export requested without an available FFmpeg path.");
            Send(new { type = "exportError", message = "FFmpeg is not available." });
            return;
        }

        string? outputPath = ResolveExportOutputPath(payload);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Send(new { type = "exportCancelled" });
            return;
        }

        _exportCts?.Cancel();
        _exportCts = new CancellationTokenSource();
        var token = _exportCts.Token;

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
                        percent = progress.ProgressPercent,
                        message = progress.StatusMessage
                    }),
                    token);

                Send(new { type = "exportComplete", outputPath });
                try { _events?.ExportCompleted?.Invoke(outputPath); } catch { }

                if (_options.CloseAfterExport)
                {
                    try { _window?.Close(); } catch { }
                }
            }
            catch (OperationCanceledException)
            {
                Send(new { type = "exportCancelled" });
            }
            catch (Exception ex)
            {
                VideoEditorServices.ReportError(nameof(VideoEditorSession), "Export failed.", ex);
                Send(new { type = "exportError", message = ex.Message });
                try { _events?.ExportFailed?.Invoke(ex); } catch { }
            }
        }, token);
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

    private VideoExportOptions BuildExportOptions(ExportPayload payload, string outputPath) => new()
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
        Watermark = payload.WatermarkEnabled ? _options.WatermarkSettings : null,
        WatermarkText = payload.WatermarkEnabled ? payload.WatermarkText : string.Empty
    };

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
