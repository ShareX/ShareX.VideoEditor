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

namespace ShareX.VideoEditor.Hosting;

/// <summary>
/// Headless export request for the advertised editor operations:
/// trim, crop, format conversion, and watermarking.
/// </summary>
public class VideoEditorExportRequest
{
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional destination path. If omitted, the service writes
    /// "&lt;input&gt;_edited.&lt;ext&gt;" next to the source file.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>One of: "MP4", "WebM", "GIF", "WebP".</summary>
    public string OutputFormat { get; set; } = "MP4";

    public bool IsTrimActive { get; set; }
    public TimeSpan TrimStart { get; set; }
    public TimeSpan TrimEnd { get; set; }

    public bool IsCropActive { get; set; }
    public int CropX { get; set; }
    public int CropY { get; set; }
    public int CropWidth { get; set; }
    public int CropHeight { get; set; }

    /// <summary>0 preserves the source frame rate.</summary>
    public double OutputFps { get; set; }

    public double QualityScale { get; set; } = 1.0;

    public bool WatermarkEnabled { get; set; }
    public string WatermarkText { get; set; } = string.Empty;
    public WatermarkSettings? Watermark { get; set; }
}
