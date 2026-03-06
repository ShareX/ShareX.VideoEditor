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

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ShareX.VideoEditor.Hosting;

namespace ShareX.VideoEditor.Presentation.ViewModels;

/// <summary>
/// Primary ViewModel for the video editor. Manages all playback state, editing state,
/// and export configuration. Follows strict ReactiveUI MVVM patterns.
/// </summary>
public class VideoEditorViewModel : ReactiveObject
{
    // ── Backing fields ──────────────────────────────────────────────────────

    private string _windowTitle = "ShareX — Video Editor";
    private string _videoPath = string.Empty;
    private string _ffmpegPath = string.Empty;
    private TimeSpan _duration = TimeSpan.Zero;
    private TimeSpan _position = TimeSpan.Zero;
    private bool _isPlaying;
    private bool _isMediaLoaded;
    private double _volume = 1.0;
    private double _playbackRate = 1.0;

    // Trim
    private TimeSpan _trimStart = TimeSpan.Zero;
    private TimeSpan _trimEnd = TimeSpan.Zero;
    private bool _isTrimActive;

    // Crop
    private bool _isCropMode;
    private double _cropX;
    private double _cropY;
    private double _cropWidth;
    private double _cropHeight;
    private bool _isCropActive;

    // Export
    private string _outputFormat = "MP4";
    private int _outputWidth;
    private int _outputHeight;
    private double _outputFps = 30.0;
    private bool _maintainAspectRatio = true;
    private double _qualityScale = 1.0;

    // Watermark
    private bool _isWatermarkEnabled;
    private string _watermarkText = string.Empty;
    private string _watermarkImagePath = string.Empty;

    // Progress / status
    private bool _isExporting;
    private double _exportProgress;
    private string _exportStatusMessage = string.Empty;
    private bool _isThumbnailsLoading;

    // Active tool panel
    private ToolPanel _activePanel = ToolPanel.Trim;

    // ── Constructor ──────────────────────────────────────────────────────────

    public VideoEditorViewModel(VideoEditorOptions options)
    {
        _videoPath = options.VideoPath;
        _ffmpegPath = options.FFmpegPath;
        _windowTitle = options.WindowTitle ?? "ShareX — Video Editor";

        if (options.WatermarkSettings != null)
        {
            _isWatermarkEnabled = options.WatermarkSettings.Enabled;
            _watermarkText = options.WatermarkSettings.Text;
            _watermarkImagePath = options.WatermarkSettings.ImagePath;
        }

        SetupCommands();
    }

    // ── Enumerations ────────────────────────────────────────────────────────

    public enum ToolPanel { Trim, Crop, Watermark, Export }

    public static IEnumerable<string> OutputFormats { get; } = ["MP4", "WebM", "GIF", "WebP"];

    // ── Properties — Window / Identity ─────────────────────────────────────

    public string WindowTitle
    {
        get => _windowTitle;
        set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
    }

    // ── Properties — Media ──────────────────────────────────────────────────

    public string VideoPath
    {
        get => _videoPath;
        set => this.RaiseAndSetIfChanged(ref _videoPath, value);
    }

    public string FFmpegPath
    {
        get => _ffmpegPath;
        set => this.RaiseAndSetIfChanged(ref _ffmpegPath, value);
    }

    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            this.RaiseAndSetIfChanged(ref _duration, value);
            if (_trimEnd == TimeSpan.Zero || _trimEnd > value)
                TrimEnd = value;
            this.RaisePropertyChanged(nameof(DurationDisplay));
            this.RaisePropertyChanged(nameof(DurationSeconds));
        }
    }

    public TimeSpan Position
    {
        get => _position;
        set
        {
            this.RaiseAndSetIfChanged(ref _position, value);
            this.RaisePropertyChanged(nameof(PositionDisplay));
            this.RaisePropertyChanged(nameof(PositionSeconds));
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            this.RaiseAndSetIfChanged(ref _isPlaying, value);
            this.RaisePropertyChanged(nameof(PlayPauseGlyph));
        }
    }

    public bool IsMediaLoaded
    {
        get => _isMediaLoaded;
        set => this.RaiseAndSetIfChanged(ref _isMediaLoaded, value);
    }

    public double Volume
    {
        get => _volume;
        set => this.RaiseAndSetIfChanged(ref _volume, value);
    }

    public double PlaybackRate
    {
        get => _playbackRate;
        set => this.RaiseAndSetIfChanged(ref _playbackRate, value);
    }

    // ── Properties — Trim ───────────────────────────────────────────────────

    public TimeSpan TrimStart
    {
        get => _trimStart;
        set
        {
            this.RaiseAndSetIfChanged(ref _trimStart, value);
            this.RaisePropertyChanged(nameof(TrimStartDisplay));
            this.RaisePropertyChanged(nameof(TrimStartSeconds));
            this.RaisePropertyChanged(nameof(TrimDurationDisplay));
        }
    }

    public TimeSpan TrimEnd
    {
        get => _trimEnd;
        set
        {
            this.RaiseAndSetIfChanged(ref _trimEnd, value);
            this.RaisePropertyChanged(nameof(TrimEndDisplay));
            this.RaisePropertyChanged(nameof(TrimEndSeconds));
            this.RaisePropertyChanged(nameof(TrimDurationDisplay));
        }
    }

    public bool IsTrimActive
    {
        get => _isTrimActive;
        set => this.RaiseAndSetIfChanged(ref _isTrimActive, value);
    }

    // ── Properties — Crop ───────────────────────────────────────────────────

    public bool IsCropMode
    {
        get => _isCropMode;
        set => this.RaiseAndSetIfChanged(ref _isCropMode, value);
    }

    public bool IsCropActive
    {
        get => _isCropActive;
        set => this.RaiseAndSetIfChanged(ref _isCropActive, value);
    }

    public double CropX
    {
        get => _cropX;
        set => this.RaiseAndSetIfChanged(ref _cropX, value);
    }

    public double CropY
    {
        get => _cropY;
        set => this.RaiseAndSetIfChanged(ref _cropY, value);
    }

    public double CropWidth
    {
        get => _cropWidth;
        set => this.RaiseAndSetIfChanged(ref _cropWidth, value);
    }

    public double CropHeight
    {
        get => _cropHeight;
        set => this.RaiseAndSetIfChanged(ref _cropHeight, value);
    }

    // ── Properties — Watermark ──────────────────────────────────────────────

    public bool IsWatermarkEnabled
    {
        get => _isWatermarkEnabled;
        set => this.RaiseAndSetIfChanged(ref _isWatermarkEnabled, value);
    }

    public string WatermarkText
    {
        get => _watermarkText;
        set => this.RaiseAndSetIfChanged(ref _watermarkText, value);
    }

    public string WatermarkImagePath
    {
        get => _watermarkImagePath;
        set => this.RaiseAndSetIfChanged(ref _watermarkImagePath, value);
    }

    // ── Properties — Export ──────────────────────────────────────────────────

    public string OutputFormat
    {
        get => _outputFormat;
        set => this.RaiseAndSetIfChanged(ref _outputFormat, value);
    }

    public int OutputWidth
    {
        get => _outputWidth;
        set => this.RaiseAndSetIfChanged(ref _outputWidth, value);
    }

    public int OutputHeight
    {
        get => _outputHeight;
        set => this.RaiseAndSetIfChanged(ref _outputHeight, value);
    }

    public double OutputFps
    {
        get => _outputFps;
        set => this.RaiseAndSetIfChanged(ref _outputFps, value);
    }

    public bool MaintainAspectRatio
    {
        get => _maintainAspectRatio;
        set => this.RaiseAndSetIfChanged(ref _maintainAspectRatio, value);
    }

    public double QualityScale
    {
        get => _qualityScale;
        set => this.RaiseAndSetIfChanged(ref _qualityScale, value);
    }

    // ── Properties — Progress / Status ──────────────────────────────────────

    public bool IsExporting
    {
        get => _isExporting;
        set => this.RaiseAndSetIfChanged(ref _isExporting, value);
    }

    public double ExportProgress
    {
        get => _exportProgress;
        set => this.RaiseAndSetIfChanged(ref _exportProgress, value);
    }

    public string ExportStatusMessage
    {
        get => _exportStatusMessage;
        set => this.RaiseAndSetIfChanged(ref _exportStatusMessage, value);
    }

    public bool IsThumbnailsLoading
    {
        get => _isThumbnailsLoading;
        set => this.RaiseAndSetIfChanged(ref _isThumbnailsLoading, value);
    }

    // ── Properties — UI State ────────────────────────────────────────────────

    public ToolPanel ActivePanel
    {
        get => _activePanel;
        set => this.RaiseAndSetIfChanged(ref _activePanel, value);
    }

    // ── Properties — Timeline thumbnails ────────────────────────────────────

    public ObservableCollection<Bitmap> TimelineThumbnails { get; } = [];

    // ── Derived display properties ───────────────────────────────────────────

    public string PositionDisplay => FormatTimecode(Position);
    public string DurationDisplay => FormatTimecode(Duration);
    public string TrimStartDisplay => FormatTimecode(TrimStart);
    public string TrimEndDisplay => FormatTimecode(TrimEnd);
    public string TrimDurationDisplay => FormatTimecode(TrimEnd - TrimStart);
    public string PlayPauseGlyph => IsPlaying ? "⏸" : "▶";

    public double PositionSeconds
    {
        get => Position.TotalSeconds;
        set => Position = TimeSpan.FromSeconds(Math.Clamp(value, 0, Duration.TotalSeconds));
    }

    public double DurationSeconds => Duration.TotalSeconds;

    public double TrimStartSeconds
    {
        get => TrimStart.TotalSeconds;
        set => TrimStart = TimeSpan.FromSeconds(Math.Clamp(value, 0, TrimEnd.TotalSeconds - 0.1));
    }

    public double TrimEndSeconds
    {
        get => TrimEnd.TotalSeconds;
        set => TrimEnd = TimeSpan.FromSeconds(Math.Clamp(value, TrimStart.TotalSeconds + 0.1, Duration.TotalSeconds));
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? PlayPauseCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SkipBackCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SkipForwardCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SetTrimStartCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SetTrimEndCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? ResetTrimCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? ToggleCropModeCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? ExportCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? CancelExportCommand { get; private set; }

    // Panel selection
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SelectTrimPanelCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SelectCropPanelCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SelectWatermarkPanelCommand { get; private set; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>? SelectExportPanelCommand { get; private set; }

    // ── Events ───────────────────────────────────────────────────────────────

    public event Action? PlayRequested;
    public event Action? PauseRequested;
    public event Action<TimeSpan>? SeekRequested;
    public event Action<string>? ExportRequested;
    public event Action? ExportCancelRequested;

    // ── Private helpers ──────────────────────────────────────────────────────

    private void SetupCommands()
    {
        PlayPauseCommand = ReactiveCommand.Create(() =>
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                PauseRequested?.Invoke();
            }
            else
            {
                IsPlaying = true;
                PlayRequested?.Invoke();
            }
        });

        SkipBackCommand = ReactiveCommand.Create(() =>
        {
            var target = Position - TimeSpan.FromSeconds(5);
            if (target < TimeSpan.Zero) target = TimeSpan.Zero;
            Position = target;
            SeekRequested?.Invoke(Position);
        });

        SkipForwardCommand = ReactiveCommand.Create(() =>
        {
            var target = Position + TimeSpan.FromSeconds(5);
            if (target > Duration) target = Duration;
            Position = target;
            SeekRequested?.Invoke(Position);
        });

        SetTrimStartCommand = ReactiveCommand.Create(() =>
        {
            TrimStart = Position;
            IsTrimActive = true;
        });

        SetTrimEndCommand = ReactiveCommand.Create(() =>
        {
            TrimEnd = Position;
            IsTrimActive = true;
        });

        ResetTrimCommand = ReactiveCommand.Create(() =>
        {
            TrimStart = TimeSpan.Zero;
            TrimEnd = Duration;
            IsTrimActive = false;
        });

        ToggleCropModeCommand = ReactiveCommand.Create(() =>
        {
            IsCropMode = !IsCropMode;
        });

        ExportCommand = ReactiveCommand.Create(() =>
        {
            ExportRequested?.Invoke(string.Empty);
        });

        CancelExportCommand = ReactiveCommand.Create(() =>
        {
            ExportCancelRequested?.Invoke();
        });

        SelectTrimPanelCommand = ReactiveCommand.Create(() => { ActivePanel = ToolPanel.Trim; });
        SelectCropPanelCommand = ReactiveCommand.Create(() => { ActivePanel = ToolPanel.Crop; });
        SelectWatermarkPanelCommand = ReactiveCommand.Create(() => { ActivePanel = ToolPanel.Watermark; });
        SelectExportPanelCommand = ReactiveCommand.Create(() => { ActivePanel = ToolPanel.Export; });
    }

    private static string FormatTimecode(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"h\:mm\:ss\.ff");
        return ts.ToString(@"m\:ss\.ff");
    }
}
