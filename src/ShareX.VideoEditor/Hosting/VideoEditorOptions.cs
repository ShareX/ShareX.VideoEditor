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
/// Configuration payload passed by the host application when opening the video editor.
/// Host applications (XerahS, ShareX) create an instance of this and pass it to
/// <see cref="VideoEditorHost.ShowEditor"/> or <see cref="VideoEditorHost.ShowEditorDialog"/>.
/// </summary>
public class VideoEditorOptions
{
    /// <summary>
    /// Absolute path to the video file to be edited.
    /// Required. The editor will not open without a valid path.
    /// </summary>
    public string VideoPath { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the FFmpeg executable.
    /// Required for export, format conversion, and thumbnail generation.
    /// The video editor does not download or manage FFmpeg; the host application is responsible.
    /// </summary>
    public string FFmpegPath { get; set; } = string.Empty;

    /// <summary>
    /// Theme variant: "Dark", "Light", or "System".
    /// Defaults to "Dark" (Premium Dark Mode).
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// BCP-47 culture code for UI localization (e.g., "en-US", "de-DE").
    /// If null or empty, falls back to the current thread culture.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Optional custom window title. If null, defaults to "ShareX - Video Editor".
    /// </summary>
    public string? WindowTitle { get; set; }

    /// <summary>
    /// Watermark configuration to be applied during export.
    /// If null, watermarking is disabled.
    /// </summary>
    public WatermarkSettings? WatermarkSettings { get; set; }
}
