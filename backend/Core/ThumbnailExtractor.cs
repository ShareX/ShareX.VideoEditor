#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Globalization;
using System.Text.RegularExpressions;
using ShareX.VideoEditor.Hosting;

namespace ShareX.VideoEditor.Core;

internal sealed record ThumbnailBatch(int StartIndex, IReadOnlyList<string> Frames, bool IsComplete);

/// <summary>
/// Extracts timeline thumbnails with one decoder per request and reports files in
/// small batches as FFmpeg makes them available.
/// </summary>
public class ThumbnailExtractor
{
    private static readonly Regex DurationRegex = new(
        @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _ffmpegPath;

    public ThumbnailExtractor(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    public async Task<IReadOnlyList<string>> ExtractThumbnailsAsync(
        string videoPath,
        int count = 24,
        int thumbWidth = 96,
        int thumbHeight = 54,
        CancellationToken cancellationToken = default)
    {
        double duration = await GetDurationSecondsAsync(videoPath, cancellationToken);
        return await ExtractThumbnailBatchesAsync(
            videoPath,
            0,
            Math.Max(duration, 0.001),
            count,
            thumbWidth,
            thumbHeight,
            null,
            cancellationToken);
    }

    internal async Task<IReadOnlyList<string>> ExtractThumbnailBatchesAsync(
        string videoPath,
        double startTime,
        double endTime,
        int count,
        int thumbWidth,
        int thumbHeight,
        Action<ThumbnailBatch>? onBatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("The source video was not found.", videoPath);
        }

        if (!double.IsFinite(startTime) || !double.IsFinite(endTime) || startTime < 0 || endTime <= startTime)
        {
            throw new ArgumentOutOfRangeException(nameof(endTime), "Thumbnail range must be finite, non-negative, and have an end after its start.");
        }

        count = Math.Clamp(count, 1, 120);
        thumbWidth = Math.Clamp(thumbWidth, 16, 1920);
        thumbHeight = Math.Clamp(thumbHeight, 16, 1080);

        var results = new List<string>(count);
        string tempDir = Path.Combine(Path.GetTempPath(), "ShareX_VideoEditor_Thumbs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            double rangeSeconds = endTime - startTime;
            double fps = count / rangeSeconds;
            string outputPattern = Path.Combine(tempDir, "thumb_%04d.jpg");
            string filter = $"fps={fps.ToString("0.######", CultureInfo.InvariantCulture)}," +
                            $"scale={thumbWidth}:{thumbHeight}:force_original_aspect_ratio=decrease," +
                            $"pad={thumbWidth}:{thumbHeight}:(ow-iw)/2:(oh-ih)/2";
            int nextFileIndex = 1;

            void FlushAvailableFiles()
            {
                var frames = new List<string>(4);
                int startIndex = results.Count;
                while (frames.Count < 4 && results.Count < count)
                {
                    string file = Path.Combine(tempDir, $"thumb_{nextFileIndex:0000}.jpg");
                    if (!File.Exists(file))
                    {
                        break;
                    }

                    try
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        if (bytes.Length < 2 || bytes[^2] != 0xFF || bytes[^1] != 0xD9)
                        {
                            break;
                        }

                        string frame = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
                        results.Add(frame);
                        frames.Add(frame);
                        nextFileIndex++;
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }

                if (frames.Count > 0)
                {
                    onBatch?.Invoke(new ThumbnailBatch(startIndex, frames, false));
                }
            }

            var arguments = new List<string>
            {
                "-hide_banner", "-loglevel", "error",
                "-progress", "pipe:1", "-nostats",
                "-ss", startTime.ToString("0.######", CultureInfo.InvariantCulture),
                "-i", videoPath,
                "-t", rangeSeconds.ToString("0.######", CultureInfo.InvariantCulture),
                "-vf", filter,
                "-frames:v", count.ToString(CultureInfo.InvariantCulture),
                "-q:v", "4",
                "-y", outputPattern
            };

            FfmpegProcessResult result = await FfmpegProcessRunner.RunAsync(
                _ffmpegPath,
                arguments,
                line =>
                {
                    if (line.StartsWith("progress=", StringComparison.Ordinal))
                    {
                        FlushAvailableFiles();
                    }
                },
                cancellationToken: cancellationToken);

            FlushAvailableFiles();
            while (results.Count < count)
            {
                int before = results.Count;
                FlushAvailableFiles();
                if (results.Count == before)
                {
                    break;
                }
            }

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result.StandardError)
                        ? $"FFmpeg thumbnail extraction exited with code {result.ExitCode}."
                        : result.StandardError.Trim());
            }

            onBatch?.Invoke(new ThumbnailBatch(results.Count, [], true));
            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            VideoEditorServices.ReportError(nameof(ThumbnailExtractor), "Thumbnail extraction failed.", ex);
            throw;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private async Task<double> GetDurationSecondsAsync(string videoPath, CancellationToken cancellationToken)
    {
        FfmpegProcessResult result = await FfmpegProcessRunner.RunAsync(
            _ffmpegPath,
            ["-hide_banner", "-i", videoPath],
            cancellationToken: cancellationToken);
        Match match = DurationRegex.Match(result.StandardError);
        if (!match.Success)
        {
            return 60;
        }

        double hours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        double minutes = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        double seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        return (hours * 3600) + (minutes * 60) + seconds;
    }
}
