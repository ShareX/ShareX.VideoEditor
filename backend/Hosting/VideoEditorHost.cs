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

using System.Runtime.InteropServices;
using ShareX.VideoEditor.Hosting.Diagnostics;

namespace ShareX.VideoEditor.Hosting;

/// <summary>
/// Public entry point for host applications to open the video editor.
///
/// <para>Architecture: A Photino.NET window hosts the compiled React/TypeScript
/// WebUI (built by Vite into <c>frontend/dist/</c>). The two sides communicate
/// through a JSON bridge:</para>
/// <list type="bullet">
///   <item>JS → C#: <c>window.external.sendMessage(json)</c></item>
///   <item>C# → JS: <c>PhotinoWindow.SendWebMessage(json)</c></item>
/// </list>
/// </summary>
public static class VideoEditorHost
{
    /// <summary>
    /// Opens the video editor as a modeless window on a dedicated background thread.
    /// Returns immediately; the editor runs independently of the caller.
    /// </summary>
    public static void ShowEditor(VideoEditorOptions options, VideoEditorEvents? events = null)
    {
        _ = StartEditorThread(options, events);
    }

    /// <summary>
    /// Opens the video editor and blocks the calling thread until the editor window closes.
    /// Returns the path of the exported file, or <c>null</c> if the user cancelled.
    /// </summary>
    public static string? ShowEditorDialog(VideoEditorOptions options, VideoEditorEvents? events = null)
    {
        string? exportedPath = null;

        var wrappedEvents = new VideoEditorEvents
        {
            ExportCompleted = path =>
            {
                exportedPath = path;
                try { events?.ExportCompleted?.Invoke(path); } catch { }
            },
            ExportFailed = ex => { try { events?.ExportFailed?.Invoke(ex); } catch { } },
            EditorClosed = () => { try { events?.EditorClosed?.Invoke(); } catch { } },
            DiagnosticReported = evt => { try { events?.DiagnosticReported?.Invoke(evt); } catch { } }
        };

        Thread thread = StartEditorThread(options, wrappedEvents);
        // Wait for the session thread to fully unwind so a follow-up open cannot race native teardown.
        thread.Join();
        return exportedPath;
    }

    private static Thread StartEditorThread(VideoEditorOptions options, VideoEditorEvents? events)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.VideoPath))
            throw new ArgumentException("VideoEditorOptions.VideoPath must be set.", nameof(options));

        IVideoEditorDiagnosticsSink? diagnostics = events?.DiagnosticReported != null
            ? new DelegateVideoEditorDiagnosticsSink(events.DiagnosticReported)
            : null;

        var thread = new Thread(() =>
        {
            IVideoEditorDiagnosticsSink? previousDiagnostics = VideoEditorServices.Diagnostics;
            VideoEditorServices.Diagnostics = diagnostics;

            try
            {
                new VideoEditorSession(options, events).Run();
            }
            catch (Exception ex)
            {
                VideoEditorServices.ReportError(nameof(VideoEditorHost), "Video editor session failed to start.", ex);

                try { events?.ExportFailed?.Invoke(ex); } catch { }
                try { events?.EditorClosed?.Invoke(); } catch { }
            }
            finally
            {
                VideoEditorServices.Diagnostics = previousDiagnostics;
            }
        })
        {
            IsBackground = true,
            Name = "ShareX.VideoEditor.Session"
        };

        // WebView2 on Windows requires STA
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        return thread;
    }
}
