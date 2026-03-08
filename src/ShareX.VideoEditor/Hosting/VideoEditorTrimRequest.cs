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
/// Parameters for a headless trim/export operation.
/// </summary>
public class VideoEditorTrimRequest
{
    /// <summary>
    /// Absolute or relative path to the source video file.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional destination path. If omitted, the service uses
    /// "&lt;input&gt;_trimmed.mp4" next to the source file.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Duration to remove from the start of the source video.
    /// </summary>
    public TimeSpan TrimStart { get; set; }

    /// <summary>
    /// Duration to remove from the end of the source video.
    /// </summary>
    public TimeSpan TrimEndOffset { get; set; }

    /// <summary>
    /// Container/codec preset to use for export. Defaults to MP4.
    /// </summary>
    public string OutputFormat { get; set; } = "MP4";

    /// <summary>
    /// Output scaling multiplier. 1.0 preserves the original resolution.
    /// </summary>
    public double QualityScale { get; set; } = 1.0;
}
