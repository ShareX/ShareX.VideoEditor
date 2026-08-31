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

using System.Globalization;
using System.Text;

namespace ShareX.VideoEditor.Core;

/// <summary>
/// Builds FFmpeg CLI arguments for the advertised editor operations:
/// trim, crop, format conversion, and text/image watermarking.
/// </summary>
public static class FfmpegArgumentBuilder
{
    public static string Build(VideoExportOptions opts, string? outputPath = null)
    {
        IReadOnlyList<string> arguments = BuildArguments(opts, outputPath);
        var rendered = new List<string>(arguments.Count);

        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];
            bool isPathOrFilter = i == arguments.Count - 1 ||
                (i > 0 && arguments[i - 1] is "-i" or "-vf" or "-filter_complex");
            rendered.Add(isPathOrFilter || argument.Any(char.IsWhiteSpace)
                ? Quote(argument.Replace("\"", "\\\"", StringComparison.Ordinal))
                : argument);
        }

        return string.Join(' ', rendered);
    }

    /// <summary>
    /// Builds discrete FFmpeg arguments so callers can use
    /// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/> without
    /// platform-specific command-line parsing.
    /// </summary>
    public static IReadOnlyList<string> BuildArguments(VideoExportOptions opts, string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(opts);

        bool hasImageWatermark = TryResolveImageWatermarkPath(opts, out string? imagePath);
        var preprocess = BuildPreprocessFilters(opts);
        bool hasText = TryBuildDrawTextFilter(opts, out string? drawText);
        bool isGif = string.Equals(opts.OutputFormat, "GIF", StringComparison.OrdinalIgnoreCase);
        bool isAudioLess = isGif || string.Equals(opts.OutputFormat, "WEBP", StringComparison.OrdinalIgnoreCase);

        var arguments = new List<string>();
        AppendInputs(arguments, opts, hasImageWatermark ? imagePath : null);

        if (hasImageWatermark)
        {
            string graph = BuildOverlayFilterGraph(preprocess, opts, drawText, isGif);
            arguments.AddRange(["-filter_complex", graph, "-map", "[vout]"]);
            if (!isAudioLess)
            {
                arguments.AddRange(["-map", "0:a?"]);
            }
        }
        else
        {
            var filters = new List<string>(preprocess);
            if (hasText)
            {
                filters.Add(drawText!);
            }

            if (isGif)
            {
                filters.Add("split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse");
            }

            if (filters.Count > 0)
            {
                arguments.AddRange(["-vf", string.Join(",", filters)]);
            }
        }

        AppendOutputCodec(arguments, opts);
        arguments.Add("-y");
        arguments.Add(outputPath ?? opts.OutputPath);
        return arguments;
    }

    public static bool TryResolveImageWatermarkPath(VideoExportOptions opts, out string? imagePath)
    {
        imagePath = null;
        string? candidate = opts.Watermark?.ImagePath;
        if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
        {
            return false;
        }

        bool enabled = opts.Watermark?.Enabled == true || !string.IsNullOrWhiteSpace(opts.WatermarkText);
        if (!enabled)
        {
            return false;
        }

        imagePath = Path.GetFullPath(candidate);
        return true;
    }

    public static bool TryNormalizeCrop(
        VideoExportOptions opts,
        out int x,
        out int y,
        out int width,
        out int height)
    {
        x = AlignEven(Math.Max(0, opts.CropX));
        y = AlignEven(Math.Max(0, opts.CropY));
        width = AlignEven(opts.CropWidth);
        height = AlignEven(opts.CropHeight);
        return width > 0 && height > 0;
    }

    private static void AppendInputs(List<string> arguments, VideoExportOptions opts, string? imagePath)
    {
        if (opts.IsTrimActive)
        {
            arguments.AddRange(["-ss", FormatTimestamp(opts.TrimStart)]);
        }

        arguments.AddRange(["-i", opts.InputPath]);

        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            arguments.AddRange(["-i", imagePath]);
        }

        if (opts.IsTrimActive)
        {
            arguments.AddRange(["-t", FormatTimestamp(opts.TrimEnd - opts.TrimStart)]);
        }
    }

    private static List<string> BuildPreprocessFilters(VideoExportOptions opts)
    {
        var filters = new List<string>();

        if (opts.IsCropActive && TryNormalizeCrop(opts, out int cropX, out int cropY, out int cropWidth, out int cropHeight))
        {
            filters.Add($"crop={cropWidth}:{cropHeight}:{cropX}:{cropY}");
        }

        if (opts.OutputFps > 0)
        {
            filters.Add($"fps={opts.OutputFps.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Math.Abs(opts.QualityScale - 1.0) > 0.01)
        {
            string scale = opts.QualityScale.ToString(CultureInfo.InvariantCulture);
            filters.Add($"scale=iw*{scale}:ih*{scale}:flags=lanczos");
        }

        return filters;
    }

    private static string BuildOverlayFilterGraph(
        List<string> preprocess,
        VideoExportOptions opts,
        string? drawText,
        bool isGif)
    {
        var graph = new StringBuilder();
        string videoLabel = "0:v";

        if (preprocess.Count > 0)
        {
            graph.Append("[0:v]").Append(string.Join(",", preprocess)).Append("[base];");
            videoLabel = "base";
        }

        double opacity = opts.Watermark?.Opacity is > 0 and <= 1
            ? opts.Watermark.Opacity
            : 0.8;
        double px = opts.Watermark?.PositionX ?? 0.95;
        double py = opts.Watermark?.PositionY ?? 0.95;

        graph.Append("[1:v]format=rgba,colorchannelmixer=aa=")
            .Append(opacity.ToString(CultureInfo.InvariantCulture))
            .Append("[wm];");
        graph.Append('[').Append(videoLabel).Append("][wm]overlay=x=(main_w-overlay_w)*")
            .Append(px.ToString(CultureInfo.InvariantCulture))
            .Append(":y=(main_h-overlay_h)*")
            .Append(py.ToString(CultureInfo.InvariantCulture));

        string current = "ov";
        graph.Append('[').Append(current).Append(']');

        if (!string.IsNullOrWhiteSpace(drawText))
        {
            graph.Append(";[").Append(current).Append(']').Append(drawText).Append("[txt]");
            current = "txt";
        }

        if (isGif)
        {
            graph.Append(";[").Append(current)
                .Append("]split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse[vout]");
        }
        else if (current != "vout")
        {
            graph.Append(";[").Append(current).Append("]null[vout]");
        }

        return graph.ToString();
    }

    internal static string? ResolveDefaultFontFile()
    {
        foreach (string candidate in EnumerateDefaultFontCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void AppendOutputCodec(List<string> arguments, VideoExportOptions opts)
    {
        switch (opts.OutputFormat.ToUpperInvariant())
        {
            case "WEBM":
                string webmCodec = string.Equals(opts.VideoCodec, "libvpx", StringComparison.OrdinalIgnoreCase)
                    ? "libvpx"
                    : "libvpx-vp9";
                arguments.AddRange(["-c:v", webmCodec, "-crf", "33", "-b:v", "0", "-c:a", "libopus"]);
                break;

            case "GIF":
                arguments.AddRange(["-loop", "0", "-an"]);
                break;

            case "WEBP":
                arguments.AddRange(["-c:v", "libwebp_anim", "-loop", "0", "-lossless", "0", "-quality", "80", "-an"]);
                break;

            case "MP4":
            default:
                arguments.AddRange(["-c:v", "libx264", "-preset", "fast", "-crf", "23", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart"]);
                break;
        }
    }

    private static bool TryBuildDrawTextFilter(VideoExportOptions opts, out string? filter)
    {
        filter = null;

        bool hasText = !string.IsNullOrWhiteSpace(opts.WatermarkText);
        bool hasSettings = opts.Watermark != null && opts.Watermark.Enabled;
        if (!hasText && !hasSettings)
        {
            return false;
        }

        string text = hasText
            ? opts.WatermarkText
            : opts.Watermark?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string? fontFile = ResolveDefaultFontFile();
        string fontClause = string.IsNullOrWhiteSpace(fontFile)
            ? string.Empty
            : $":fontfile='{EscapeFilterPath(fontFile)}'";

        if (hasSettings)
        {
            double px = opts.Watermark!.PositionX;
            double py = opts.Watermark.PositionY;
            int fontSize = opts.Watermark.FontSize > 0 ? opts.Watermark.FontSize : 24;
            string fontColor = NormalizeFontColor(opts.Watermark.FontColor);
            string escapedText = EscapeDrawText(text);

            filter = $"drawtext=text='{escapedText}'" +
                     fontClause +
                     $":fontsize={fontSize}" +
                     $":fontcolor={fontColor}" +
                     $":x=(w-text_w)*{px.ToString(CultureInfo.InvariantCulture)}" +
                     $":y=(h-text_h)*{py.ToString(CultureInfo.InvariantCulture)}" +
                     $":alpha={opts.Watermark.Opacity.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        filter = $"drawtext=text='{EscapeDrawText(text)}'" +
                 fontClause +
                 ":fontsize=24:fontcolor=white:x=w-tw-10:y=h-th-10:alpha=0.8";
        return true;
    }

    private static IEnumerable<string> EnumerateDefaultFontCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            yield return Path.Combine(fonts, "arial.ttf");
            yield return Path.Combine(fonts, "segoeui.ttf");
            yield return Path.Combine(fonts, "tahoma.ttf");
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts/Supplemental/Arial.ttf";
            yield return "/Library/Fonts/Arial.ttf";
            yield return "/System/Library/Fonts/Helvetica.ttc";
            yield break;
        }

        yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        yield return "/usr/share/fonts/dejavu/DejaVuSans.ttf";
        yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/liberation/LiberationSans-Regular.ttf";
    }

    private static string NormalizeFontColor(string? fontColor)
    {
        if (string.IsNullOrWhiteSpace(fontColor))
        {
            return "white";
        }

        string trimmed = fontColor.Trim();
        if (trimmed.StartsWith('#') && trimmed.Length is 7 or 9)
        {
            return "0x" + trimmed[1..];
        }

        return trimmed;
    }

    private static string EscapeDrawText(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
    }

    private static string EscapeFilterPath(string path)
    {
        return path
            .Replace('\\', '/')
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
    }

    private static string FormatTimestamp(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static int AlignEven(int value) => value < 2 ? 0 : value & ~1;
}
