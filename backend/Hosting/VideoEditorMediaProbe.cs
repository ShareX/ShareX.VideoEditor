using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ShareX.VideoEditor.Hosting;

internal sealed class VideoEditorMediaInfo
{
    public string CodecName { get; init; } = string.Empty;

    public string CodecTagString { get; init; } = string.Empty;

    public string Profile { get; init; } = string.Empty;
}

internal static class VideoEditorMediaProbe
{
    public static VideoEditorMediaInfo? TryProbePrimaryVideoStream(string ffprobePath, string videoPath)
    {
        if (string.IsNullOrWhiteSpace(ffprobePath) ||
            string.IsNullOrWhiteSpace(videoPath) ||
            !File.Exists(ffprobePath) ||
            !File.Exists(videoPath))
        {
            return null;
        }

        var startInfo = new ProcessStartInfo(
            ffprobePath,
            $"-v error -select_streams v:0 -show_entries stream=codec_name,codec_tag_string,profile -of json \"{videoPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(startInfo);
        if (process == null)
        {
            return null;
        }

        string output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        JObject root = JObject.Parse(output);
        JObject? stream = root["streams"]?
            .Children<JObject>()
            .FirstOrDefault();
        if (stream == null)
        {
            return null;
        }

        string codecName = stream["codec_name"]?.Value<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codecName))
        {
            return null;
        }

        return new VideoEditorMediaInfo
        {
            CodecName = codecName,
            CodecTagString = stream["codec_tag_string"]?.Value<string>() ?? string.Empty,
            Profile = stream["profile"]?.Value<string>() ?? string.Empty
        };
    }
}
