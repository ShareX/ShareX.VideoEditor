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

using System.Collections.Concurrent;
using System.Diagnostics;

namespace ShareX.VideoEditor.Core;

public sealed class FfmpegCapabilitySnapshot
{
    public bool HasLibX264 { get; init; }
    public bool HasVp9 { get; init; }
    public bool HasVp8 { get; init; }
    public bool HasWebPAnim { get; init; }
    public bool HasGif { get; init; }

    public IReadOnlyList<string> AvailableFormats
    {
        get
        {
            var formats = new List<string>();
            if (HasLibX264)
            {
                formats.Add("MP4");
            }

            if (HasVp9 || HasVp8)
            {
                formats.Add("WebM");
            }

            if (HasGif)
            {
                formats.Add("GIF");
            }

            if (HasWebPAnim)
            {
                formats.Add("WebP");
            }

            return formats;
        }
    }

    public bool Supports(string format) => format.ToUpperInvariant() switch
    {
        "MP4" => HasLibX264,
        "WEBM" => HasVp9 || HasVp8,
        "GIF" => HasGif,
        "WEBP" => HasWebPAnim,
        _ => false
    };

    public string? ResolveWebMCodec()
    {
        if (HasVp9)
        {
            return "libvpx-vp9";
        }

        return HasVp8 ? "libvpx" : null;
    }
}

public static class FfmpegCapabilityProbe
{
    private static readonly ConcurrentDictionary<string, FfmpegCapabilitySnapshot> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static FfmpegCapabilitySnapshot ParseEncoderList(string encoderList)
    {
        string text = encoderList ?? string.Empty;
        return new FfmpegCapabilitySnapshot
        {
            HasLibX264 = ContainsEncoder(text, "libx264"),
            HasVp9 = ContainsEncoder(text, "libvpx-vp9"),
            HasVp8 = ContainsEncoder(text, "libvpx"),
            HasWebPAnim = ContainsEncoder(text, "libwebp_anim"),
            HasGif = ContainsEncoder(text, "gif")
        };
    }

    public static FfmpegCapabilitySnapshot Probe(string ffmpegPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);

        string cacheKey = Path.GetFullPath(ffmpegPath);
        return Cache.GetOrAdd(cacheKey, static path => ProbeCore(path));
    }

    private static FfmpegCapabilitySnapshot ProbeCore(string ffmpegPath)
    {
        if (!File.Exists(ffmpegPath))
        {
            return new FfmpegCapabilitySnapshot();
        }

        try
        {
            var startInfo = new ProcessStartInfo(ffmpegPath, "-hide_banner -encoders")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return new FfmpegCapabilitySnapshot();
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(8000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                try { process.WaitForExit(2000); } catch { }
                return new FfmpegCapabilitySnapshot();
            }

            Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(2));
            string stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty;
            string stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
            return ParseEncoderList(stdout + Environment.NewLine + stderr);
        }
        catch
        {
            return new FfmpegCapabilitySnapshot();
        }
    }

    private static bool ContainsEncoder(string encoderList, string encoderName)
    {
        using var reader = new StringReader(encoderList);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            ReadOnlySpan<char> span = line.AsSpan().Trim();
            if (span.IsEmpty)
            {
                continue;
            }

            // Encoder listing lines look like: " V..... libx264             H.264 / AVC"
            int nameStart = span.IndexOf(encoderName, StringComparison.Ordinal);
            if (nameStart < 0)
            {
                continue;
            }

            int nameEnd = nameStart + encoderName.Length;
            bool boundedStart = nameStart == 0 || char.IsWhiteSpace(span[nameStart - 1]);
            bool boundedEnd = nameEnd == span.Length || char.IsWhiteSpace(span[nameEnd]);
            if (boundedStart && boundedEnd)
            {
                return true;
            }
        }

        return false;
    }
}
