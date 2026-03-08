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

using ShareX.VideoEditor.Hosting;

namespace ShareX.VideoEditor.Core;

/// <summary>
/// All parameters the VideoExportService needs to construct the FFmpeg command.
/// Populated from the VideoEditorViewModel state by the VideoEditorWindow.
/// </summary>
public class VideoExportOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = "MP4";

    // Trim
    public bool IsTrimActive { get; set; }
    public TimeSpan TrimStart { get; set; }
    public TimeSpan TrimEnd { get; set; }

    // Crop
    public bool IsCropActive { get; set; }
    public int CropX { get; set; }
    public int CropY { get; set; }
    public int CropWidth { get; set; }
    public int CropHeight { get; set; }

    // Encoding
    public double OutputFps { get; set; } = 30;
    public double QualityScale { get; set; } = 1.0;

    // Watermark
    public WatermarkSettings? Watermark { get; set; }
    public string WatermarkText { get; set; } = string.Empty;
}
