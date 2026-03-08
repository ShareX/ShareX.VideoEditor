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
using ShareX.VideoEditor.Core;

namespace ShareX.VideoEditor.Hosting;

/// <summary>
/// Host-facing automation API for non-interactive video editor operations.
/// </summary>
public class VideoEditorAutomationService
{
    private static readonly Regex DurationRegex =
        new(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private const double TimestampSelectionEpsilonSeconds = 0.0005;
    private const long ExactTrimTimescale = 1_000_000;

    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly VideoExportService _videoExportService;

    public VideoEditorAutomationService(string ffmpegPath, string? ffprobePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath, nameof(ffmpegPath));

        _ffmpegPath = Path.GetFullPath(ffmpegPath);

        if (!File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException("FFmpeg executable was not found.", _ffmpegPath);
        }

        _ffprobePath = ResolveFFprobePath(ffprobePath, _ffmpegPath);
        _videoExportService = new VideoExportService(_ffmpegPath);
    }

    /// <summary>
    /// Probes the duration of a video file using ffprobe when available, with an
    /// ffmpeg stderr fallback.
    /// </summary>
    public async Task<TimeSpan> ProbeDurationAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        string normalizedInputPath = NormalizeExistingFilePath(inputPath, nameof(inputPath));

        double durationSeconds = await ProbeDurationSecondsAsync(normalizedInputPath, cancellationToken);

        if (durationSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"Could not determine the duration of '{normalizedInputPath}'.");
        }

        return TimeSpan.FromSeconds(durationSeconds);
    }

    /// <summary>
    /// Trims a source video without opening the editor UI and exports the result.
    /// </summary>
    public async Task<VideoEditorTrimResult> TrimAsync(
        VideoEditorTrimRequest request,
        Action<VideoExportProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string inputPath = NormalizeExistingFilePath(request.InputPath, nameof(request.InputPath));
        string outputPath = ResolveOutputPath(request.OutputPath, inputPath);

        if (request.TrimStart < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.TrimStart),
                "TrimStart must be zero or greater.");
        }

        if (request.TrimEndOffset < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.TrimEndOffset),
                "TrimEndOffset must be zero or greater.");
        }

        TimeSpan sourceDuration = await ProbeDurationAsync(inputPath, cancellationToken);
        TimeSpan trimEnd = sourceDuration - request.TrimEndOffset;

        if (trimEnd <= request.TrimStart)
        {
            throw new InvalidOperationException(
                $"Trim range is invalid. Start={request.TrimStart.TotalSeconds:F2}s, " +
                $"End={trimEnd.TotalSeconds:F2}s, Duration={sourceDuration.TotalSeconds:F2}s.");
        }

        VideoEditorServices.ReportInformation(
            nameof(VideoEditorAutomationService),
            $"Headless trim requested for '{inputPath}' -> '{outputPath}'.");

        ExactVideoTrimPlan? exactTrimPlan = await TryBuildExactVideoTrimPlanAsync(
            inputPath,
            outputPath,
            request,
            trimEnd,
            cancellationToken);

        if (exactTrimPlan != null)
        {
            return await ExecuteExactVideoTrimAsync(
                inputPath,
                outputPath,
                request,
                sourceDuration,
                trimEnd,
                exactTrimPlan,
                onProgress,
                cancellationToken);
        }

        var exportOptions = new VideoExportOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            OutputFormat = string.IsNullOrWhiteSpace(request.OutputFormat) ? "MP4" : request.OutputFormat,
            IsTrimActive = true,
            TrimStart = request.TrimStart,
            TrimEnd = trimEnd,
            OutputFps = 0,
            QualityScale = request.QualityScale
        };

        await _videoExportService.ExportAsync(exportOptions, onProgress, cancellationToken);

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException("Export completed but the output file was not created.");
        }

        return new VideoEditorTrimResult
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            FFmpegPath = _ffmpegPath,
            SourceDuration = sourceDuration,
            TrimStart = request.TrimStart,
            TrimEnd = trimEnd,
            OutputDuration = trimEnd - request.TrimStart
        };
    }

    private async Task<ExactVideoTrimPlan?> TryBuildExactVideoTrimPlanAsync(
        string inputPath,
        string outputPath,
        VideoEditorTrimRequest request,
        TimeSpan trimEnd,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_ffprobePath) ||
            !File.Exists(_ffprobePath) ||
            !string.Equals(
                Path.GetExtension(outputPath),
                ".mp4",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                string.IsNullOrWhiteSpace(request.OutputFormat) ? "MP4" : request.OutputFormat,
                "MP4",
                StringComparison.OrdinalIgnoreCase) ||
            Math.Abs(request.QualityScale - 1.0) > 0.01)
        {
            return null;
        }

        string[] streamTypes = await ProbeStreamTypesAsync(inputPath, cancellationToken);

        if (streamTypes.Length != 1 ||
            !string.Equals(streamTypes[0], "video", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        double[] frameTimestamps = await ProbeVideoFrameTimestampsAsync(inputPath, cancellationToken);

        if (frameTimestamps.Length == 0)
        {
            return null;
        }

        return BuildExactVideoTrimPlan(
            frameTimestamps,
            request.TrimStart,
            trimEnd);
    }

    private async Task<VideoEditorTrimResult> ExecuteExactVideoTrimAsync(
        string inputPath,
        string outputPath,
        VideoEditorTrimRequest request,
        TimeSpan sourceDuration,
        TimeSpan trimEnd,
        ExactVideoTrimPlan plan,
        Action<VideoExportProgress>? onProgress,
        CancellationToken cancellationToken)
    {
        string arguments = BuildExactTrimArguments(inputPath, outputPath, request.TrimStart, trimEnd, plan);

        VideoEditorServices.ReportInformation(
            nameof(VideoEditorAutomationService),
            $"Using exact video-only trim path. Boundary={plan.BoundaryFrameTimestampSeconds:F6}s, " +
            $"RangeStart={(plan.RangeStartTimestampSeconds?.ToString("F6", CultureInfo.InvariantCulture) ?? "n/a")}s.");

        try
        {
            await _videoExportService.ExportWithCustomArgumentsAsync(
                arguments,
                outputPath,
                trimEnd - request.TrimStart,
                onProgress,
                cancellationToken);

            Mp4DurationPatcher.PatchExactDuration(outputPath, trimEnd - request.TrimStart);
        }
        catch
        {
            TryDelete(outputPath);
            throw;
        }

        return new VideoEditorTrimResult
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            FFmpegPath = _ffmpegPath,
            SourceDuration = sourceDuration,
            TrimStart = request.TrimStart,
            TrimEnd = trimEnd,
            OutputDuration = trimEnd - request.TrimStart
        };
    }

    private async Task<double> ProbeDurationSecondsAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_ffprobePath) && File.Exists(_ffprobePath))
        {
            var probeStartInfo = new ProcessStartInfo(
                _ffprobePath,
                $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{inputPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? probeProcess = Process.Start(probeStartInfo);
            if (probeProcess != null)
            {
                using CancellationTokenRegistration probeRegistration =
                    cancellationToken.Register(() => TryKill(probeProcess));

                string rawDuration = await probeProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                await probeProcess.WaitForExitAsync(cancellationToken);

                if (double.TryParse(
                    rawDuration.Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double durationSeconds) &&
                    durationSeconds > 0)
                {
                    return durationSeconds;
                }
            }
        }

        var ffmpegStartInfo = new ProcessStartInfo(_ffmpegPath, $"-i \"{inputPath}\"")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? ffmpegProcess = Process.Start(ffmpegStartInfo);
        if (ffmpegProcess == null)
        {
            return 0;
        }

        using CancellationTokenRegistration ffmpegRegistration =
            cancellationToken.Register(() => TryKill(ffmpegProcess));

        string stderr = await ffmpegProcess.StandardError.ReadToEndAsync(cancellationToken);
        await ffmpegProcess.WaitForExitAsync(cancellationToken);

        Match durationMatch = DurationRegex.Match(stderr);
        if (!durationMatch.Success)
        {
            return 0;
        }

        double hours = double.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double minutes = double.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double seconds = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);

        return (hours * 3600) + (minutes * 60) + seconds;
    }

    private async Task<string[]> ProbeStreamTypesAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var probeStartInfo = new ProcessStartInfo(
            _ffprobePath,
            $"-v error -show_entries stream=codec_type -of default=noprint_wrappers=1:nokey=1 \"{inputPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? probeProcess = Process.Start(probeStartInfo);
        if (probeProcess == null)
        {
            return [];
        }

        using CancellationTokenRegistration probeRegistration =
            cancellationToken.Register(() => TryKill(probeProcess));

        string rawOutput = await probeProcess.StandardOutput.ReadToEndAsync(cancellationToken);
        await probeProcess.WaitForExitAsync(cancellationToken);

        return rawOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private async Task<double[]> ProbeVideoFrameTimestampsAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var probeStartInfo = new ProcessStartInfo(
            _ffprobePath,
            $"-v error -select_streams v:0 -show_entries frame=best_effort_timestamp_time -of csv=p=0 \"{inputPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? probeProcess = Process.Start(probeStartInfo);
        if (probeProcess == null)
        {
            return [];
        }

        using CancellationTokenRegistration probeRegistration =
            cancellationToken.Register(() => TryKill(probeProcess));

        string rawOutput = await probeProcess.StandardOutput.ReadToEndAsync(cancellationToken);
        await probeProcess.WaitForExitAsync(cancellationToken);

        return rawOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Split(',')[0].Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => double.Parse(line, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static ExactVideoTrimPlan? BuildExactVideoTrimPlan(
        IReadOnlyList<double> frameTimestamps,
        TimeSpan trimStart,
        TimeSpan trimEnd)
    {
        double trimStartSeconds = trimStart.TotalSeconds;
        double trimEndSeconds = trimEnd.TotalSeconds;

        int boundaryIndex = 0;

        while (boundaryIndex + 1 < frameTimestamps.Count &&
               frameTimestamps[boundaryIndex + 1] <= trimStartSeconds + TimestampSelectionEpsilonSeconds)
        {
            boundaryIndex++;
        }

        int rangeStartIndex = 0;

        while (rangeStartIndex < frameTimestamps.Count &&
               frameTimestamps[rangeStartIndex] < trimStartSeconds - TimestampSelectionEpsilonSeconds)
        {
            rangeStartIndex++;
        }

        int lastRetainedIndex = frameTimestamps.Count - 1;

        while (lastRetainedIndex >= 0 &&
               frameTimestamps[lastRetainedIndex] >= trimEndSeconds - TimestampSelectionEpsilonSeconds)
        {
            lastRetainedIndex--;
        }

        if (lastRetainedIndex < 0)
        {
            return null;
        }

        bool hasBoundaryCarryFrame =
            boundaryIndex < rangeStartIndex &&
            boundaryIndex <= lastRetainedIndex;

        bool hasRange = rangeStartIndex <= lastRetainedIndex;

        if (!hasBoundaryCarryFrame && !hasRange)
        {
            return null;
        }

        double boundaryFrameTimestamp = frameTimestamps[boundaryIndex];
        double? rangeStartTimestamp = hasRange ? frameTimestamps[rangeStartIndex] : null;

        return new ExactVideoTrimPlan(
            boundaryFrameTimestamp,
            hasBoundaryCarryFrame,
            rangeStartTimestamp);
    }

    private static string BuildExactTrimArguments(
        string inputPath,
        string outputPath,
        TimeSpan trimStart,
        TimeSpan trimEnd,
        ExactVideoTrimPlan plan)
    {
        string trimStartSeconds = FormatSeconds(trimStart.TotalSeconds);
        string trimEndSeconds = FormatSeconds(trimEnd.TotalSeconds);
        string boundaryLowerSeconds = FormatSeconds(Math.Max(
            0,
            plan.BoundaryFrameTimestampSeconds - TimestampSelectionEpsilonSeconds));
        string boundaryUpperSeconds = FormatSeconds(
            plan.BoundaryFrameTimestampSeconds + TimestampSelectionEpsilonSeconds);
        string startTimestampMicroseconds = ((long)Math.Round(
            trimStart.TotalSeconds * ExactTrimTimescale,
            MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

        string selectExpression;

        if (plan.HasBoundaryCarryFrame && plan.RangeStartTimestampSeconds.HasValue)
        {
            string rangeStartLowerSeconds = FormatSeconds(Math.Max(
                0,
                plan.RangeStartTimestampSeconds.Value - TimestampSelectionEpsilonSeconds));

            selectExpression =
                $"between(t,{boundaryLowerSeconds},{boundaryUpperSeconds})+" +
                $"gte(t,{rangeStartLowerSeconds})*lt(t,{trimEndSeconds})";
        }
        else if (plan.HasBoundaryCarryFrame)
        {
            selectExpression = $"between(t,{boundaryLowerSeconds},{boundaryUpperSeconds})";
        }
        else
        {
            string rangeStartLowerSeconds = FormatSeconds(Math.Max(
                0,
                (plan.RangeStartTimestampSeconds ?? trimStart.TotalSeconds) - TimestampSelectionEpsilonSeconds));

            selectExpression =
                $"gte(t,{rangeStartLowerSeconds})*lt(t,{trimEndSeconds})";
        }

        string filterComplex =
            $"[0:v]select='{selectExpression}',settb=AVTB," +
            $"setpts='if(lt(T,{trimStartSeconds}),0,PTS-{startTimestampMicroseconds})'[v]";

        return
            $"-i \"{inputPath}\" " +
            $"-filter_complex \"{filterComplex}\" " +
            "-map \"[v]\" " +
            "-fps_mode passthrough " +
            "-enc_time_base 1:1000000 " +
            "-video_track_timescale 1000000 " +
            "-c:v libx264 -x264-params bframes=0 -preset fast -crf 23 " +
            "-movflags +faststart -an " +
            $"-y \"{outputPath}\"";
    }

    private static string FormatSeconds(double seconds)
    {
        return seconds.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string ResolveFFprobePath(string? ffprobePath, string ffmpegPath)
    {
        if (!string.IsNullOrWhiteSpace(ffprobePath))
        {
            string normalizedProbePath = Path.GetFullPath(ffprobePath);
            if (File.Exists(normalizedProbePath))
            {
                return normalizedProbePath;
            }
        }

        string directory = Path.GetDirectoryName(ffmpegPath) ?? string.Empty;
        string fileName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        string siblingProbePath = Path.Combine(directory, fileName);

        return File.Exists(siblingProbePath) ? siblingProbePath : string.Empty;
    }

    private static string NormalizeExistingFilePath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

        string normalizedPath = Path.GetFullPath(path);

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("The specified file does not exist.", normalizedPath);
        }

        return normalizedPath;
    }

    private static string ResolveOutputPath(string? outputPath, string inputPath)
    {
        string resolvedOutputPath = !string.IsNullOrWhiteSpace(outputPath)
            ? Path.GetFullPath(outputPath)
            : Path.Combine(
                Path.GetDirectoryName(inputPath) ?? ".",
                $"{Path.GetFileNameWithoutExtension(inputPath)}_trimmed.mp4");

        string? outputDirectory = Path.GetDirectoryName(resolvedOutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return resolvedOutputPath;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
        }
    }

    private sealed record ExactVideoTrimPlan(
        double BoundaryFrameTimestampSeconds,
        bool HasBoundaryCarryFrame,
        double? RangeStartTimestampSeconds);
}
