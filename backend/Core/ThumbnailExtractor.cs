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
        var results = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "ShareX_VideoEditor_Thumbs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            double fps = (double)count / await GetDurationSecondsAsync(videoPath, cancellationToken);
            if (double.IsNaN(fps) || fps <= 0) fps = 1;

            string outputPattern = Path.Combine(tempDir, "thumb_%04d.jpg");
            string args = $"-i \"{videoPath}\" " +
                          $"-vf \"fps={fps:F4},scale={thumbWidth}:{thumbHeight}:force_original_aspect_ratio=decrease,pad={thumbWidth}:{thumbHeight}:(ow-iw)/2:(oh-ih)/2\" " +
                          $"-frames:v {count} " +
                          $"-q:v 4 " +
                          $"\"{outputPattern}\" -y";

            bool success = await RunFFmpegAsync(args, cancellationToken);
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
        string args = $"-i \"{videoPath}\"";
        var output = new System.Text.StringBuilder();

        var psi = new ProcessStartInfo(_ffmpegPath, args)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return 60;

        string? line;
        while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
            output.AppendLine(line);

        await process.WaitForExitAsync(cancellationToken);

        var durationLine = output.ToString()
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

    private async Task<bool> RunFFmpegAsync(string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(_ffmpegPath, args)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return false;

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0;
    }
}
