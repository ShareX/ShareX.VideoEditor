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
/// Service locator for services shared across the video editor.
/// </summary>
public static class VideoEditorServices
{
    /// <summary>
    /// Optional diagnostics sink. Host applications may set this before opening the editor.
    /// </summary>
    public static IVideoEditorDiagnosticsSink? Diagnostics { get; set; }

    public static void ReportInformation(string source, string message)
        => ReportDiagnostic(VideoEditorDiagnosticLevel.Information, source, message, null);

    public static void ReportWarning(string source, string message, Exception? exception = null)
        => ReportDiagnostic(VideoEditorDiagnosticLevel.Warning, source, message, exception);

    public static void ReportError(string source, string message, Exception? exception = null)
        => ReportDiagnostic(VideoEditorDiagnosticLevel.Error, source, message, exception);

    private static void ReportDiagnostic(
        VideoEditorDiagnosticLevel level,
        string source,
        string message,
        Exception? exception)
    {
        IVideoEditorDiagnosticsSink? sink = Diagnostics;
        if (sink == null) return;

        var evt = new VideoEditorDiagnosticEvent(level, source, message, exception);
        try
        {
            sink.Report(evt);
        }
        catch
        {
            // Diagnostics must never break editor functionality.
        }
    }
}
