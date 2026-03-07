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

namespace ShareX.VideoEditor.Hosting.Bridge;

/// <summary>
/// JSON payload sent by the React UI when the user requests an export.
/// The C# host receives this, shows a native save dialog, then invokes FFmpeg.
/// </summary>
internal sealed class ExportPayload
{
    // ── Trim ─────────────────────────────────────────────────────────────────

    [JsonProperty("isTrimActive")]
    public bool IsTrimActive { get; set; }

    /// <summary>Trim start in seconds.</summary>
    [JsonProperty("trimStart")]
    public double TrimStart { get; set; }

    /// <summary>Trim end in seconds.</summary>
    [JsonProperty("trimEnd")]
    public double TrimEnd { get; set; }

    // ── Crop ─────────────────────────────────────────────────────────────────

    [JsonProperty("isCropActive")]
    public bool IsCropActive { get; set; }

    [JsonProperty("cropX")]
    public int CropX { get; set; }

    [JsonProperty("cropY")]
    public int CropY { get; set; }

    [JsonProperty("cropWidth")]
    public int CropWidth { get; set; }

    [JsonProperty("cropHeight")]
    public int CropHeight { get; set; }

    // ── Output settings ───────────────────────────────────────────────────────

    /// <summary>One of: "MP4", "WebM", "GIF", "WebP".</summary>
    [JsonProperty("outputFormat")]
    public string OutputFormat { get; set; } = "MP4";

    [JsonProperty("fps")]
    public double Fps { get; set; } = 30;

    /// <summary>Resolution scale: 1.0 = original, 0.5 = half, etc.</summary>
    [JsonProperty("qualityScale")]
    public double QualityScale { get; set; } = 1.0;

    // ── Watermark ─────────────────────────────────────────────────────────────

    [JsonProperty("watermarkEnabled")]
    public bool WatermarkEnabled { get; set; }

    [JsonProperty("watermarkText")]
    public string WatermarkText { get; set; } = string.Empty;
}
