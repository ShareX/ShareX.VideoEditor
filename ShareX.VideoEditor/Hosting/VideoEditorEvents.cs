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

using ShareX.VideoEditor.Hosting.Diagnostics;

namespace ShareX.VideoEditor.Hosting;

/// <summary>
/// Callbacks provided by the host application to receive results from the video editor.
/// </summary>
public class VideoEditorEvents
{
    /// <summary>
    /// Invoked when the user successfully exports/saves a video.
    /// Receives the absolute path to the exported output file.
    /// </summary>
    public Action<string>? ExportCompleted { get; set; }

    /// <summary>
    /// Invoked when an export operation fails.
    /// Receives the exception that caused the failure.
    /// </summary>
    public Action<Exception>? ExportFailed { get; set; }

    /// <summary>
    /// Invoked when the editor window is closed by the user.
    /// </summary>
    public Action? EditorClosed { get; set; }

    /// <summary>
    /// Optional diagnostics sink for receiving log/error events from the editor.
    /// </summary>
    public Action<VideoEditorDiagnosticEvent>? DiagnosticReported { get; set; }
}
