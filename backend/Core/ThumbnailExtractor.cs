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
using ShareX.VideoEditor.Hosting;

namespace ShareX.VideoEditor.Core;

/// <summary>
/// Asynchronously extracts frame thumbnails from a video using FFmpeg.
/// Thumbnails are returned as Base64-encoded JPEG data URIs, ready to be
/// sent over the JSON bridge to the React WebUI timeline scrubber.
/// </summary>
public class ThumbnailExtractor
{
    private readonly string _ffmpegPath;

    public ThumbnailExtractor(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    /// <summary>
    /// Extracts <paramref name="count"/> evenly-spaced frame thumbnails from the video.
    /// Each thumbnail is returned as a <c>data:image/jpeg;base64,…</c> data URI string.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExtractThumbnailsAsync(
        string videoPath,
        int count = 24,
        int thumbWidth = 96,
        int thumbHeight = 54,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath, nameof(videoPath));
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("The source video was not found.", videoPath);
        }

        count = Math.Clamp(count, 1, 120);
        thumbWidth = Math.Clamp(thumbWidth, 16, 1920);
        thumbHeight = Math.Clamp(thumbHeight, 16, 1080);

        var results = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "ShareX_VideoEditor_Thumbs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            double durationSeconds = await GetDurationSecondsAsync(videoPath, cancellationToken);
            double fps = durationSeconds > 0 ? count / durationSeconds : 1;
            if (!double.IsFinite(fps) || fps <= 0) fps = 1;

            string outputPattern = Path.Combine(tempDir, "thumb_%04d.jpg");
            string filter = $"fps={fps.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)}," +
                            $"scale={thumbWidth}:{thumbHeight}:force_original_aspect_ratio=decrease," +
                            $"pad={thumbWidth}:{thumbHeight}:(ow-iw)/2:(oh-ih)/2";

            bool success = await RunFFmpegAsync(
                [
                    "-hide_banner", "-loglevel", "error",
                    "-i", videoPath,
                    "-vf", filter,
                    "-frames:v", count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-q:v", "4",
                    outputPattern,
                    "-y"
                ],
                cancellationToken);
            if (!success) return results;

            foreach (var file in Directory.GetFiles(tempDir, "thumb_*.jpg").OrderBy(f => f))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    byte[] bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                    results.Add("data:image/jpeg;base64," + Convert.ToBase64String(bytes));
                }
                catch (Exception ex)
                {
                    VideoEditorServices.ReportWarning(nameof(ThumbnailExtractor), $"Failed to encode thumbnail '{file}'.", ex);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            VideoEditorServices.ReportError(nameof(ThumbnailExtractor), "Thumbnail extraction failed.", ex);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }

        return results;
    }

    private async Task<double> GetDurationSecondsAsync(string videoPath, CancellationToken cancellationToken)
    {
        var psi = CreateStartInfo(["-hide_banner", "-i", videoPath]);

        using var process = Process.Start(psi);
        if (process == null) return 60;

        string output = await WaitForExitAndReadStderrAsync(process, cancellationToken);

        var durationLine = output
            .Split('\n')
            .FirstOrDefault(l => l.TrimStart().StartsWith("Duration:", StringComparison.OrdinalIgnoreCase));

        if (durationLine != null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                durationLine, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (match.Success)
            {
                double hours = double.Parse(match.Groups[1].Value);
                double minutes = double.Parse(match.Groups[2].Value);
                double seconds = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                return hours * 3600 + minutes * 60 + seconds;
            }
        }

        return 60;
    }

    private async Task<bool> RunFFmpegAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var psi = CreateStartInfo(arguments);

        using var process = Process.Start(psi);
        if (process == null) return false;

        _ = await WaitForExitAndReadStderrAsync(process, cancellationToken);
        return process.ExitCode == 0;
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(_ffmpegPath)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task<string> WaitForExitAndReadStderrAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenRegistration registration = cancellationToken.Register(() => TryKill(process));

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return await stderrTask;
        }
        catch
        {
            TryKill(process);
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            try { _ = await stderrTask.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            throw;
        }
    }

    private static void TryKill(Process process)
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
}
