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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReactiveUI;
using ShareX.VideoEditor.Core;
using ShareX.VideoEditor.Hosting;
using ShareX.VideoEditor.Presentation.Controls;
using ShareX.VideoEditor.Presentation.ViewModels;
using System.Reactive.Concurrency;

namespace ShareX.VideoEditor.Presentation.Views;

public partial class VideoEditorWindow : Window
{
    private readonly VideoEditorViewModel _vm;
    private readonly VideoEditorOptions _options;
    private VideoExportService? _exportService;
    private CancellationTokenSource? _exportCts;
    private CancellationTokenSource? _thumbnailCts;

    // ── Events raised for the host application ───────────────────────────────

    public event Action<string>? ExportCompleted;
    public event Action<Exception>? ExportFailed;

    // ── Constructor ──────────────────────────────────────────────────────────

    // Parameterless constructor required by Avalonia AXAML compiler for design-time support
    public VideoEditorWindow() : this(new VideoEditorOptions()) { }

    public VideoEditorWindow(VideoEditorOptions options)
    {
        // ReactiveCommand internally does ObserveOn(RxApp.MainThreadScheduler) to route
        // CanExecuteChanged to the main thread. Without UseReactiveUI() in the host app's
        // AppBuilder, RxApp.MainThreadScheduler defaults to DefaultScheduler (LongRunning),
        // causing CanExecuteChanged to fire from a background thread and crashing Avalonia
        // with "Call from invalid thread". Set it to the Avalonia SynchronizationContext
        // before the ViewModel (and its ReactiveCommands) are created.
        if (SynchronizationContext.Current is { } ctx)
        {
            RxApp.MainThreadScheduler = new SynchronizationContextScheduler(ctx);
        }

        _options = options;
        _vm = new VideoEditorViewModel(options);
        DataContext = _vm;

        InitializeComponent();
        WireKeyboardShortcuts();
        WireViewModelEvents();

        this.Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var timeline = this.FindControl<TimelineScrubber>("TimelineScrubberControl");
        if (timeline != null)
        {
            timeline.PositionChanged += OnTimelineScrubPosition;
            timeline.TrimStartChanged += ts => { _vm.TrimStart = ts; _vm.IsTrimActive = true; };
            timeline.TrimEndChanged += te => { _vm.TrimEnd = te; _vm.IsTrimActive = true; };
        }

        // Start thumbnail generation if FFmpeg is available
        if (!string.IsNullOrWhiteSpace(_options.FFmpegPath) && File.Exists(_options.FFmpegPath))
            _ = GenerateThumbnailsAsync();
        else
            VideoEditorServices.ReportWarning(nameof(VideoEditorWindow),
                "FFmpegPath is not set or does not exist. Thumbnails and export will not be available.");
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _thumbnailCts?.Cancel();
        _exportCts?.Cancel();
        base.OnClosing(e);
    }

    // ── Keyboard shortcuts ───────────────────────────────────────────────────

    private void WireKeyboardShortcuts()
    {
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Space:
                    _vm.PlayPauseCommand?.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                    break;
                case Key.Left:
                    _vm.SkipBackCommand?.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                    break;
                case Key.Right:
                    _vm.SkipForwardCommand?.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                    break;
                case Key.I:
                    _vm.SetTrimStartCommand?.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                    break;
                case Key.O:
                    _vm.SetTrimEndCommand?.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                    break;
                case Key.E when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    _vm.ExportCommand?.Execute(System.Reactive.Unit.Default);
                    e.Handled = true;
                    break;
            }
        };
    }

    // ── ViewModel event wiring ───────────────────────────────────────────────

    private void WireViewModelEvents()
    {
        _vm.ExportRequested += path => { var t = ExportAsync(); };
        _vm.ExportCancelRequested += () => _exportCts?.Cancel();
    }

    private void OnTimelineScrubPosition(TimeSpan position)
    {
        _vm.Position = position;
        // Media seek delegated here; actual player hook added in Phase 2
    }

    // ── Thumbnail generation ─────────────────────────────────────────────────

    private async Task GenerateThumbnailsAsync()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts = new CancellationTokenSource();
        var token = _thumbnailCts.Token;

        _vm.IsThumbnailsLoading = true;
        _vm.TimelineThumbnails.Clear();

        try
        {
            var extractor = new ThumbnailExtractor(_options.FFmpegPath);
            var thumbnails = await extractor.ExtractThumbnailsAsync(
                _options.VideoPath,
                count: 24,
                cancellationToken: token);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var bmp in thumbnails)
                    _vm.TimelineThumbnails.Add(bmp);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            VideoEditorServices.ReportWarning(nameof(VideoEditorWindow), "Thumbnail generation failed.", ex);
        }
        finally
        {
            // Always dispatch to the UI thread — the awaited FFmpeg process tasks
            // resume on a thread-pool thread, so setting VM properties here without
            // marshalling triggers ReactiveUI property-change notifications off the
            // UI thread, causing Avalonia's "Call from invalid thread" crash.
            await Dispatcher.UIThread.InvokeAsync(() => _vm.IsThumbnailsLoading = false);
        }
    }

    // ── Export ───────────────────────────────────────────────────────────────

    private async Task ExportAsync()
    {
        if (_vm.IsExporting) return;

        // Determine output path via modern StorageProvider API
        string ext = GetExtension(_vm.OutputFormat);
        string formatName = _vm.OutputFormat switch
        {
            "MP4" => "MPEG-4 Video",
            "WebM" => "WebM Video",
            "GIF" => "Animated GIF",
            "WebP" => "Animated WebP",
            _ => "Video Files"
        };

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Export Video",
            DefaultExtension = ext,
            SuggestedFileName = Path.GetFileNameWithoutExtension(_options.VideoPath) + "_edited",
            FileTypeChoices =
            [
                new Avalonia.Platform.Storage.FilePickerFileType(formatName)
                {
                    Patterns = [$"*.{ext}"]
                }
            ]
        });

        string? outputPath = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        _vm.IsExporting = true;
        _vm.ExportProgress = 0;
        _vm.ExportStatusMessage = "Preparing…";

        _exportCts = new CancellationTokenSource();

        try
        {
            _exportService = new VideoExportService(_options.FFmpegPath);

            var exportOptions = BuildExportOptions(outputPath);

            await _exportService.ExportAsync(
                exportOptions,
                // Progress callback fires from FFmpeg stderr read thread — marshal to UI.
                progress => Dispatcher.UIThread.Post(() =>
                {
                    _vm.ExportProgress = progress.ProgressPercent;
                    _vm.ExportStatusMessage = progress.StatusMessage;
                }),
                _exportCts.Token);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.ExportProgress = 100;
                _vm.ExportStatusMessage = "Done!";
                ExportCompleted?.Invoke(outputPath);
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => _vm.ExportStatusMessage = "Cancelled");
        }
        catch (Exception ex)
        {
            VideoEditorServices.ReportError(nameof(VideoEditorWindow), "Export failed.", ex);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExportFailed?.Invoke(ex);
                _vm.ExportStatusMessage = "Export failed";
            });
        }
        finally
        {
            await Task.Delay(1500); // brief display of final status
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.IsExporting = false;
                _vm.ExportProgress = 0;
                _vm.ExportStatusMessage = string.Empty;
            });
        }
    }

    private VideoExportOptions BuildExportOptions(string outputPath) => new()
    {
        InputPath = _options.VideoPath,
        OutputPath = outputPath,
        OutputFormat = _vm.OutputFormat,
        TrimStart = _vm.IsTrimActive ? _vm.TrimStart : TimeSpan.Zero,
        TrimEnd = _vm.IsTrimActive ? _vm.TrimEnd : _vm.Duration,
        IsTrimActive = _vm.IsTrimActive,
        IsCropActive = _vm.IsCropActive,
        CropX = (int)_vm.CropX,
        CropY = (int)_vm.CropY,
        CropWidth = (int)_vm.CropWidth,
        CropHeight = (int)_vm.CropHeight,
        OutputFps = _vm.OutputFps,
        QualityScale = _vm.QualityScale,
        Watermark = _vm.IsWatermarkEnabled && _options.WatermarkSettings != null
            ? _options.WatermarkSettings
            : null,
        WatermarkText = _vm.IsWatermarkEnabled ? _vm.WatermarkText : string.Empty
    };

    private static string GetExtension(string format) => format.ToLowerInvariant() switch
    {
        "mp4" => "mp4",
        "webm" => "webm",
        "gif" => "gif",
        "webp" => "webp",
        _ => "mp4"
    };
}
