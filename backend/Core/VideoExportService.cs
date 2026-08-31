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

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using ShareX.VideoEditor.Hosting;

namespace ShareX.VideoEditor.Core;

/// <summary>
/// Builds FFmpeg arguments from <see cref="VideoExportOptions"/> and executes the
/// encoding pipeline asynchronously, streaming progress back to the caller.
///
/// The host application provides the FFmpeg executable path via
/// <see cref="VideoEditorOptions.FFmpegPath"/>; this service never downloads or manages FFmpeg.
/// </summary>
public class VideoExportService
{
    private static readonly Regex TimeRegex = new(@"time=(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new(@"speed=\s*([\d.]+)x", RegexOptions.Compiled);

    private readonly string _ffmpegPath;

    public VideoExportService(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    /// <summary>
    /// Runs the FFmpeg export pipeline asynchronously.
    /// </summary>
    /// <param name="options">Export parameters.</param>
    /// <param name="onProgress">Callback invoked for each FFmpeg progress line.</param>
    /// <param name="cancellationToken">Cancels the FFmpeg process.</param>
    public async Task ExportAsync(
        VideoExportOptions options,
        Action<VideoExportProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateExportOptions(options);
        ApplyFormatCapabilities(options);
        TimeSpan expectedDuration = options.IsTrimActive && options.TrimEnd > options.TrimStart
            ? options.TrimEnd - options.TrimStart
            : TimeSpan.Zero;

        await ExportTransactionalAsync(
            options.OutputPath,
            stagingPath => FfmpegArgumentBuilder.Build(options, stagingPath),
            expectedDuration,
            null,
            onProgress,
            cancellationToken);
    }

    public async Task ExportWithCustomArgumentsAsync(
        string arguments,
        string outputPath,
        TimeSpan expectedDuration,
        Action<VideoExportProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments, nameof(arguments));
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath, nameof(outputPath));

        await ExportTransactionalAsync(
            outputPath,
            stagingPath => ReplaceTrailingOutputPath(arguments, outputPath, stagingPath),
            expectedDuration,
            null,
            onProgress,
            cancellationToken);
    }

    internal async Task ExportWithCustomArgumentsAsync(
        Func<string, string> buildArguments,
        string outputPath,
        TimeSpan expectedDuration,
        Action<string>? prepareStagedOutput = null,
        Action<VideoExportProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buildArguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath, nameof(outputPath));

        await ExportTransactionalAsync(
            outputPath,
            buildArguments,
            expectedDuration,
            prepareStagedOutput,
            onProgress,
            cancellationToken);
    }

    private async Task ExportTransactionalAsync(
        string outputPath,
        Func<string, string> buildArguments,
        TimeSpan expectedDuration,
        Action<string>? prepareStagedOutput,
        Action<VideoExportProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        string normalizedOutputPath = Path.GetFullPath(outputPath);
        string? outputDirectory = Path.GetDirectoryName(normalizedOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("The export destination must have a parent directory.");
        }

        Directory.CreateDirectory(outputDirectory);
        string stagingPath = CreateStagingOutputPath(normalizedOutputPath);

        try
        {
            string arguments = buildArguments(stagingPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(arguments, nameof(buildArguments));

            await RunFfmpegAsync(arguments, expectedDuration, onProgress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            prepareStagedOutput?.Invoke(stagingPath);
            CommitStagedOutput(stagingPath, normalizedOutputPath);
        }
        finally
        {
            TryDeleteFile(stagingPath);
        }
    }

    private async Task RunFfmpegAsync(
        string arguments,
        TimeSpan expectedDuration,
        Action<VideoExportProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments, nameof(arguments));

        VideoEditorServices.ReportInformation(nameof(VideoExportService), "Starting FFmpeg export process.");

        var psi = new ProcessStartInfo(_ffmpegPath, arguments)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start FFmpeg process.");

        double totalSeconds = Math.Max(0, expectedDuration.TotalSeconds);
        var errorTail = new Queue<string>();

        try
        {
            using (cancellationToken.Register(() => TryKillProcess(process)))
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
                {
                    RememberErrorLine(errorTail, line);
                    var progress = ParseProgressLine(line, totalSeconds);
                    if (progress != null)
                    {
                        onProgress?.Invoke(progress);
                    }
                }

                await process.WaitForExitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(FormatFfmpegFailure(process.ExitCode, errorTail));
            }
        }
        catch
        {
            TryKillProcess(process);
            await WaitForExitAfterKillAsync(process);
            throw;
        }
    }

    internal static string CreateStagingOutputPath(string outputPath)
    {
        string normalizedOutputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(normalizedOutputPath)
            ?? throw new InvalidOperationException("The export destination must have a parent directory.");
        string extension = Path.GetExtension(normalizedOutputPath);
        string name = Path.GetFileNameWithoutExtension(normalizedOutputPath);
        string stagingName = $".{name}.{Guid.NewGuid():N}.part{extension}";
        return Path.Combine(directory, stagingName);
    }

    internal static void CommitStagedOutput(string stagingPath, string outputPath)
    {
        if (!File.Exists(stagingPath) || new FileInfo(stagingPath).Length == 0)
        {
            throw new InvalidOperationException("FFmpeg completed without producing a valid output file.");
        }

        File.Move(stagingPath, outputPath, overwrite: true);
    }

    internal static string ReplaceTrailingOutputPath(
        string arguments,
        string outputPath,
        string stagingPath)
    {
        string trimmedArguments = arguments.TrimEnd();
        string quotedOutputPath = $"\"{outputPath}\"";
        string replacement = $"\"{stagingPath}\"";

        if (trimmedArguments.EndsWith(quotedOutputPath, StringComparison.Ordinal))
        {
            return trimmedArguments[..^quotedOutputPath.Length] + replacement;
        }

        if (trimmedArguments.EndsWith(outputPath, StringComparison.Ordinal))
        {
            int outputStart = trimmedArguments.Length - outputPath.Length;
            if (outputStart == 0 || char.IsWhiteSpace(trimmedArguments[outputStart - 1]))
            {
                return trimmedArguments[..outputStart] + replacement;
            }
        }

        throw new InvalidOperationException(
            "Custom FFmpeg arguments must end with the outputPath value so the export can be staged safely.");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static async Task WaitForExitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }
    }

    private static void ValidateExportOptions(VideoExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.InputPath, nameof(options.InputPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputPath, nameof(options.OutputPath));

        string inputPath = Path.GetFullPath(options.InputPath);
        string outputPath = Path.GetFullPath(options.OutputPath);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("The source video was not found.", inputPath);
        }

        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(inputPath, outputPath, pathComparison))
        {
            throw new InvalidOperationException("The export destination must be different from the source video.");
        }

        options.OutputFormat = string.IsNullOrWhiteSpace(options.OutputFormat)
            ? "MP4"
            : options.OutputFormat.Trim();

        if (options.IsTrimActive &&
            (options.TrimStart < TimeSpan.Zero || options.TrimEnd <= options.TrimStart))
        {
            throw new InvalidOperationException("The trim range must have a non-negative start and an end after the start.");
        }

        if (!double.IsFinite(options.OutputFps) || options.OutputFps < 0 || options.OutputFps > 240)
        {
            throw new ArgumentOutOfRangeException(nameof(options.OutputFps), "Output FPS must be between 0 and 240.");
        }

        if (!double.IsFinite(options.QualityScale) || options.QualityScale <= 0 || options.QualityScale > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(options.QualityScale), "Quality scale must be greater than 0 and no more than 4.");
        }

        if (options.IsCropActive &&
            (options.CropX < 0 || options.CropY < 0 || options.CropWidth <= 0 || options.CropHeight <= 0))
        {
            throw new InvalidOperationException("An active crop must have non-negative coordinates and a positive size.");
        }
    }

    private void ApplyFormatCapabilities(VideoExportOptions options)
    {
        FfmpegCapabilitySnapshot capabilities = FfmpegCapabilityProbe.Probe(_ffmpegPath);
        string format = string.IsNullOrWhiteSpace(options.OutputFormat) ? "MP4" : options.OutputFormat;

        if (!capabilities.Supports(format))
        {
            string available = capabilities.AvailableFormats.Count == 0
                ? "none"
                : string.Join(", ", capabilities.AvailableFormats);
            throw new InvalidOperationException(
                $"FFmpeg cannot export {format}. Available formats: {available}.");
        }

        if (string.Equals(format, "WebM", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.VideoCodec))
        {
            options.VideoCodec = capabilities.ResolveWebMCodec() ?? string.Empty;
        }
    }

    private static void RememberErrorLine(Queue<string> errorTail, string line)
    {
        string trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        errorTail.Enqueue(trimmed);
        while (errorTail.Count > 24)
        {
            errorTail.Dequeue();
        }
    }

    private static string FormatFfmpegFailure(int exitCode, Queue<string> errorTail)
    {
        if (errorTail.Count == 0)
        {
            return $"FFmpeg exited with code {exitCode}.";
        }

        string[] interesting = errorTail
            .Where(static line =>
                line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Unknown", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Invalid", StringComparison.OrdinalIgnoreCase))
            .TakeLast(4)
            .ToArray();

        string detail = interesting.Length > 0
            ? string.Join(" ", interesting)
            : string.Join(" ", errorTail.TakeLast(2));

        return $"FFmpeg exited with code {exitCode}. {detail}";
    }

    // ── Progress parsing ─────────────────────────────────────────────────────

    private static VideoExportProgress? ParseProgressLine(string line, double totalSeconds)
    {
        // FFmpeg outputs lines like: frame=  420 fps= 90 q=28.0 size=   3456kB time=00:00:14.01 bitrate=2020.5kbits/s speed=2.51x
        var timeMatch = TimeRegex.Match(line);
        if (!timeMatch.Success) return null;

        double hours = double.Parse(timeMatch.Groups[1].Value);
        double minutes = double.Parse(timeMatch.Groups[2].Value);
        double seconds = double.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        double currentSecs = hours * 3600 + minutes * 60 + seconds;

        double percent = totalSeconds > 0
            ? Math.Min(100, (currentSecs / totalSeconds) * 100)
            : 0;

        double speed = 0;
        var speedMatch = SpeedRegex.Match(line);
        if (speedMatch.Success)
            double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out speed);

        string eta = speed > 0
            ? $" — {speed:F1}x"
            : string.Empty;
        string elapsed = TimeSpan.FromSeconds(currentSecs)
            .ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        return new VideoExportProgress
        {
            ProgressPercent = percent,
            CurrentTime = TimeSpan.FromSeconds(currentSecs),
            Speed = speed,
            StatusMessage = totalSeconds > 0
                ? $"Encoding… {percent:F0}%{eta}"
                : $"Encoding… {elapsed}{eta}"
        };
    }
}
