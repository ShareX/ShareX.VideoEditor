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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using ShareX.VideoEditor.Hosting.Diagnostics;
using ShareX.VideoEditor.Presentation.Views;

namespace ShareX.VideoEditor.Hosting;

/// <summary>
/// Bootstraps the Avalonia runtime (once) and provides the public API
/// for host applications to open the video editor window.
/// </summary>
public static class AvaloniaIntegration
{
    private static bool _initialized;
    private static readonly object _initLock = new();

    /// <summary>
    /// Ensures the Avalonia application is initialized. Safe to call multiple times.
    /// Must be called on the UI thread if the host app already has an Avalonia app running.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;

            if (Application.Current == null)
            {
                AppBuilder.Configure<VideoEditorApp>()
                    .UsePlatformDetect()
                    .WithInterFont()
                    .LogToTrace()
                    .SetupWithoutStarting();
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Opens the video editor as a modeless window.
    /// </summary>
    /// <param name="options">Editor configuration from the host application.</param>
    /// <param name="events">Optional callbacks for export results and diagnostics.</param>
    public static void ShowEditor(VideoEditorOptions options, VideoEditorEvents? events = null)
    {
        ValidateOptions(options);
        Initialize();
        SetupDiagnostics(events);

        Dispatcher.UIThread.Post(() =>
        {
            var window = new VideoEditorWindow(options);
            WireEvents(window, events);
            window.Show();
        });
    }

    /// <summary>
    /// Opens the video editor as a modal-like dialog (blocks via a dispatcher frame until closed).
    /// Returns the path of the exported file, or null if the user cancelled.
    /// </summary>
    /// <param name="options">Editor configuration from the host application.</param>
    /// <param name="events">Optional callbacks for export results and diagnostics.</param>
    public static string? ShowEditorDialog(VideoEditorOptions options, VideoEditorEvents? events = null)
    {
        ValidateOptions(options);
        Initialize();

        IVideoEditorDiagnosticsSink? previousSink = null;
        bool restoreSink = false;

        if (events?.DiagnosticReported != null)
        {
            previousSink = VideoEditorServices.Diagnostics;
            restoreSink = true;

            var diagnosticHandler = events.DiagnosticReported;
            VideoEditorServices.Diagnostics = new DelegateVideoEditorDiagnosticsSink(evt =>
            {
                try { diagnosticHandler(evt); } catch { }
                try { previousSink?.Report(evt); } catch { }
            });
        }

        string? exportedPath = null;
        var window = new VideoEditorWindow(options);

        if (events?.ExportCompleted != null)
        {
            window.ExportCompleted += path =>
            {
                exportedPath = path;
                try { events.ExportCompleted(path); } catch { }
            };
        }

        if (events?.ExportFailed != null)
        {
            window.ExportFailed += ex =>
            {
                try { events.ExportFailed(ex); } catch { }
            };
        }

        window.Show();

        var frame = new DispatcherFrame();
        window.Closed += (_, _) =>
        {
            frame.Continue = false;
            if (restoreSink) VideoEditorServices.Diagnostics = previousSink;
            try { events?.EditorClosed?.Invoke(); } catch { }
        };
        Dispatcher.UIThread.PushFrame(frame);

        return exportedPath;
    }

    private static void WireEvents(VideoEditorWindow window, VideoEditorEvents? events)
    {
        if (events == null) return;

        if (events.ExportCompleted != null)
            window.ExportCompleted += path => { try { events.ExportCompleted(path); } catch { } };

        if (events.ExportFailed != null)
            window.ExportFailed += ex => { try { events.ExportFailed(ex); } catch { } };

        window.Closed += (_, _) => { try { events.EditorClosed?.Invoke(); } catch { } };
    }

    private static void SetupDiagnostics(VideoEditorEvents? events)
    {
        if (events?.DiagnosticReported != null)
            VideoEditorServices.Diagnostics = new DelegateVideoEditorDiagnosticsSink(events.DiagnosticReported);
    }

    private static void ValidateOptions(VideoEditorOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.VideoPath))
            throw new ArgumentException("VideoEditorOptions.VideoPath must be set.", nameof(options));
    }
}

/// <summary>
/// Minimal Avalonia Application used when the host does not already have one running.
/// </summary>
internal class VideoEditorApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnFrameworkInitializationCompleted();
    }
}
