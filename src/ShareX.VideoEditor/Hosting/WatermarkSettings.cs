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
/// Watermark configuration provided by the host application.
/// The video editor renders this configuration as an overlay during export.
/// </summary>
public class WatermarkSettings
{
    public bool Enabled { get; set; } = false;
    public string Text { get; set; } = string.Empty;
    /// <summary>Path to an image file for image watermark. Not yet applied during export; only text watermark is used by the FFmpeg pipeline.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Opacity: 0.0 (transparent) to 1.0 (opaque).</summary>
    public double Opacity { get; set; } = 0.8;

    /// <summary>Horizontal position as fraction of video width: 0.0 = left, 1.0 = right.</summary>
    public double PositionX { get; set; } = 0.95;

    /// <summary>Vertical position as fraction of video height: 0.0 = top, 1.0 = bottom.</summary>
    public double PositionY { get; set; } = 0.95;

    public int FontSize { get; set; } = 24;
    public string FontColor { get; set; } = "#FFFFFF";
}
