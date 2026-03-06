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
        string args = BuildArguments(options);
        VideoEditorServices.ReportInformation(nameof(VideoExportService), $"FFmpeg args: {args}");

        var psi = new ProcessStartInfo(_ffmpegPath, args)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start FFmpeg process.");

        double totalSeconds = (options.TrimEnd - options.TrimStart).TotalSeconds;
        if (totalSeconds <= 0) totalSeconds = 1;

        await using (cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        }))
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
            {
                var progress = ParseProgressLine(line, totalSeconds);
                if (progress != null) onProgress?.Invoke(progress);
            }

            await process.WaitForExitAsync(cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            // Clean up partial output
            if (File.Exists(options.OutputPath))
                try { File.Delete(options.OutputPath); } catch { }

            throw new OperationCanceledException(cancellationToken);
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}.");
    }

    // ── Argument builder ─────────────────────────────────────────────────────

    private static string BuildArguments(VideoExportOptions opts)
    {
        var sb = new System.Text.StringBuilder();

        // Input + trim
        if (opts.IsTrimActive)
        {
            sb.Append($"-ss {opts.TrimStart:hh\\:mm\\:ss\\.ff} ");
            sb.Append($"-i \"{opts.InputPath}\" ");
            sb.Append($"-to {opts.TrimEnd - opts.TrimStart:hh\\:mm\\:ss\\.ff} ");
        }
        else
        {
            sb.Append($"-i \"{opts.InputPath}\" ");
        }

        // Video filters chain
        var filters = new List<string>();

        // Crop
        if (opts.IsCropActive && opts.CropWidth > 0 && opts.CropHeight > 0)
            filters.Add($"crop={opts.CropWidth}:{opts.CropHeight}:{opts.CropX}:{opts.CropY}");

        // FPS
        if (opts.OutputFps > 0)
            filters.Add($"fps={opts.OutputFps.ToString(CultureInfo.InvariantCulture)}");

        // Quality / scale (if not 100%)
        if (Math.Abs(opts.QualityScale - 1.0) > 0.01)
            filters.Add($"scale=iw*{opts.QualityScale.ToString(CultureInfo.InvariantCulture)}:ih*{opts.QualityScale.ToString(CultureInfo.InvariantCulture)}:flags=lanczos");

        // Watermark
        bool hasTextWatermark = !string.IsNullOrWhiteSpace(opts.WatermarkText);
        bool hasWatermarkSettings = opts.Watermark != null && opts.Watermark.Enabled;

        if (hasWatermarkSettings && !string.IsNullOrWhiteSpace(opts.WatermarkText))
        {
            double px = opts.Watermark!.PositionX;
            double py = opts.Watermark!.PositionY;
            int fontSize = opts.Watermark.FontSize;
            string fontColor = opts.Watermark.FontColor.TrimStart('#');
            string text = opts.WatermarkText.Replace(":", "\\:");
            // drawtext filter
            filters.Add($"drawtext=text='{text}'" +
                        $":fontsize={fontSize}" +
                        $":fontcolor=0x{fontColor}" +
                        $":x=(w-text_w)*{px.ToString(CultureInfo.InvariantCulture)}" +
                        $":y=(h-text_h)*{py.ToString(CultureInfo.InvariantCulture)}" +
                        $":alpha={opts.Watermark.Opacity.ToString(CultureInfo.InvariantCulture)}");
        }
        else if (hasTextWatermark)
        {
            string text = opts.WatermarkText.Replace(":", "\\:");
            filters.Add($"drawtext=text='{text}':fontsize=24:fontcolor=white:x=w-tw-10:y=h-th-10:alpha=0.8");
        }

        if (filters.Count > 0)
            sb.Append($"-vf \"{string.Join(",", filters)}\" ");

        // Codec / format
        AppendOutputCodec(sb, opts);

        // Output
        sb.Append($"-y \"{opts.OutputPath}\"");

        return sb.ToString();
    }

    private static void AppendOutputCodec(System.Text.StringBuilder sb, VideoExportOptions opts)
    {
        switch (opts.OutputFormat.ToUpperInvariant())
        {
            case "WEBM":
                sb.Append("-c:v libvpx-vp9 -crf 33 -b:v 0 -c:a libopus ");
                break;

            case "GIF":
                // Two-pass palettegen for high-quality GIF
                // (single-pass for simplicity; two-pass requires intermediate file)
                sb.Append("-loop 0 ");
                sb.Append("-vf \"split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" ");
                sb.Append("-an ");
                break;

            case "WEBP":
                sb.Append("-c:v libwebp_anim -loop 0 -lossless 0 -quality 80 -an ");
                break;

            case "MP4":
            default:
                sb.Append("-c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k -movflags +faststart ");
                break;
        }
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

        double percent = Math.Min(100, (currentSecs / totalSeconds) * 100);

        double speed = 0;
        var speedMatch = SpeedRegex.Match(line);
        if (speedMatch.Success)
            double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out speed);

        string eta = speed > 0
            ? $" — {speed:F1}x"
            : string.Empty;

        return new VideoExportProgress
        {
            ProgressPercent = percent,
            CurrentTime = TimeSpan.FromSeconds(currentSecs),
            Speed = speed,
            StatusMessage = $"Encoding… {percent:F0}%{eta}"
        };
    }
}
