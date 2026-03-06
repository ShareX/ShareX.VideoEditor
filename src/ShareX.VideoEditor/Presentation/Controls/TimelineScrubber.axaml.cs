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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ShareX.VideoEditor.Presentation.Controls;

/// <summary>
/// Custom timeline scrubber control.
/// Displays frame thumbnails along a horizontal track with:
/// - Draggable playhead
/// - Draggable trim-in and trim-out handles
/// - Time ruler with markers
/// - Trim region highlight overlay
/// </summary>
public partial class TimelineScrubber : UserControl
{
    // ── Avalonia Properties ──────────────────────────────────────────────────

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<TimelineScrubber, TimeSpan>(nameof(Duration), TimeSpan.Zero);

    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<TimelineScrubber, TimeSpan>(nameof(Position), TimeSpan.Zero);

    public static readonly StyledProperty<TimeSpan> TrimStartProperty =
        AvaloniaProperty.Register<TimelineScrubber, TimeSpan>(nameof(TrimStart), TimeSpan.Zero);

    public static readonly StyledProperty<TimeSpan> TrimEndProperty =
        AvaloniaProperty.Register<TimelineScrubber, TimeSpan>(nameof(TrimEnd), TimeSpan.Zero);

    public static readonly StyledProperty<ObservableCollection<Bitmap>?> ThumbnailsProperty =
        AvaloniaProperty.Register<TimelineScrubber, ObservableCollection<Bitmap>?>(nameof(Thumbnails));

    // ── CLR wrappers ─────────────────────────────────────────────────────────

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public TimeSpan Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public TimeSpan TrimStart
    {
        get => GetValue(TrimStartProperty);
        set => SetValue(TrimStartProperty, value);
    }

    public TimeSpan TrimEnd
    {
        get => GetValue(TrimEndProperty);
        set => SetValue(TrimEndProperty, value);
    }

    public ObservableCollection<Bitmap>? Thumbnails
    {
        get => GetValue(ThumbnailsProperty);
        set => SetValue(ThumbnailsProperty, value);
    }

    // ── Events ───────────────────────────────────────────────────────────────

    public event Action<TimeSpan>? PositionChanged;
    public event Action<TimeSpan>? TrimStartChanged;
    public event Action<TimeSpan>? TrimEndChanged;

    // ── Private state ────────────────────────────────────────────────────────

    private enum DragTarget { None, Playhead, TrimStart, TrimEnd }

    private DragTarget _drag = DragTarget.None;
    private double _dragStartX;

    // Named controls (resolved after AXAML load)
    private Canvas? _trimRegionCanvas;
    private Border? _trimRegionFill;
    private Border? _trimStartHandle;
    private Border? _trimEndHandle;
    private Border? _playhead;
    private Border? _playheadHead;
    private Canvas? _rulerCanvas;
    private Canvas? _markersCanvas;

    private static readonly SolidColorBrush RulerTextBrush = new(Color.Parse("#7A7A96"));
    private static readonly Pen RulerTickPen = new(new SolidColorBrush(Color.Parse("#3A3A52")), 1);

    // ── Constructor ──────────────────────────────────────────────────────────

    public TimelineScrubber()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _trimRegionCanvas = this.FindControl<Canvas>("TrimRegionCanvas");
        _trimRegionFill = this.FindControl<Border>("TrimRegionFill");
        _trimStartHandle = this.FindControl<Border>("TrimStartHandle");
        _trimEndHandle = this.FindControl<Border>("TrimEndHandle");
        _playhead = this.FindControl<Border>("Playhead");
        _playheadHead = this.FindControl<Border>("PlayheadHead");
        _rulerCanvas = this.FindControl<Canvas>("RulerCanvas");
        _markersCanvas = this.FindControl<Canvas>("MarkersCanvas");

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;

        // Re-layout overlays when properties change
        DurationProperty.Changed.AddClassHandler<TimelineScrubber>((s, _) => s.UpdateOverlays());
        PositionProperty.Changed.AddClassHandler<TimelineScrubber>((s, _) => s.UpdateOverlays());
        TrimStartProperty.Changed.AddClassHandler<TimelineScrubber>((s, _) => s.UpdateOverlays());
        TrimEndProperty.Changed.AddClassHandler<TimelineScrubber>((s, _) => s.UpdateOverlays());
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateOverlays();
        DrawRuler();
    }

    private void UpdateOverlays()
    {
        double trackWidth = Bounds.Width;
        double trackHeight = _trimRegionCanvas?.Bounds.Height ?? 0;
        if (trackWidth <= 0 || Duration == TimeSpan.Zero) return;

        double totalSecs = Duration.TotalSeconds;
        double startFrac = TrimStart.TotalSeconds / totalSecs;
        double endFrac = TrimEnd.TotalSeconds / totalSecs;
        double posFrac = Math.Clamp(Position.TotalSeconds / totalSecs, 0, 1);

        double startX = startFrac * trackWidth;
        double endX = endFrac * trackWidth;
        double posX = posFrac * trackWidth;

        const double handleWidth = 6;
        const double playheadWidth = 2;
        const double headSize = 10;

        // Trim region fill
        if (_trimRegionFill != null)
        {
            Canvas.SetLeft(_trimRegionFill, startX);
            Canvas.SetTop(_trimRegionFill, 0);
            _trimRegionFill.Width = Math.Max(0, endX - startX);
            _trimRegionFill.Height = trackHeight;
        }

        // Trim start handle
        if (_trimStartHandle != null)
        {
            Canvas.SetLeft(_trimStartHandle, startX - handleWidth);
            Canvas.SetTop(_trimStartHandle, 0);
            _trimStartHandle.Height = trackHeight;
        }

        // Trim end handle
        if (_trimEndHandle != null)
        {
            Canvas.SetLeft(_trimEndHandle, endX);
            Canvas.SetTop(_trimEndHandle, 0);
            _trimEndHandle.Height = trackHeight;
        }

        // Playhead line
        if (_playhead != null)
        {
            Canvas.SetLeft(_playhead, posX - playheadWidth / 2.0);
            Canvas.SetTop(_playhead, 0);
            _playhead.Height = trackHeight;
        }

        // Playhead head (diamond at top)
        if (_playheadHead != null)
        {
            Canvas.SetLeft(_playheadHead, posX - headSize / 2.0);
            Canvas.SetTop(_playheadHead, -headSize / 2.0);
        }
    }

    private void DrawRuler()
    {
        if (_rulerCanvas == null || Duration == TimeSpan.Zero) return;

        _rulerCanvas.Children.Clear();
        double width = Bounds.Width;
        double height = _rulerCanvas.Bounds.Height;
        double totalSecs = Duration.TotalSeconds;
        int tickCount = (int)(width / 60); // one tick per ~60px
        if (tickCount < 2) tickCount = 2;

        for (int i = 0; i <= tickCount; i++)
        {
            double frac = (double)i / tickCount;
            double x = frac * width;
            var ts = TimeSpan.FromSeconds(frac * totalSecs);

            // Tick mark
            var tick = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(x, height - 4),
                EndPoint = new Avalonia.Point(x, height),
                Stroke = RulerTickPen.Brush,
                StrokeThickness = 1
            };
            _rulerCanvas.Children.Add(tick);

            // Label
            var label = new TextBlock
            {
                Text = FormatRulerTime(ts),
                FontSize = 9,
                Foreground = RulerTextBrush,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace")
            };
            Canvas.SetLeft(label, x + 2);
            Canvas.SetTop(label, 0);
            _rulerCanvas.Children.Add(label);
        }
    }

    private static string FormatRulerTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return ts.ToString(@"h\:mm\:ss");
        return ts.ToString(@"m\:ss");
    }

    // ── Hit testing helpers ───────────────────────────────────────────────────

    private DragTarget HitTest(Avalonia.Point p)
    {
        double trackWidth = Bounds.Width;
        if (trackWidth <= 0 || Duration == TimeSpan.Zero) return DragTarget.None;

        double totalSecs = Duration.TotalSeconds;
        double startX = (TrimStart.TotalSeconds / totalSecs) * trackWidth;
        double endX = (TrimEnd.TotalSeconds / totalSecs) * trackWidth;

        const double handleHitZone = 10;

        if (Math.Abs(p.X - startX) <= handleHitZone) return DragTarget.TrimStart;
        if (Math.Abs(p.X - endX) <= handleHitZone) return DragTarget.TrimEnd;
        return DragTarget.Playhead;
    }

    private TimeSpan XToTime(double x)
    {
        double trackWidth = Bounds.Width;
        if (trackWidth <= 0) return TimeSpan.Zero;
        double frac = Math.Clamp(x / trackWidth, 0, 1);
        return TimeSpan.FromSeconds(frac * Duration.TotalSeconds);
    }

    // ── Pointer events ───────────────────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        _drag = HitTest(pos);
        _dragStartX = pos.X;
        e.Pointer.Capture(this);
        ApplyDrag(pos.X);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_drag == DragTarget.None) return;
        ApplyDrag(e.GetPosition(this).X);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ApplyDrag(e.GetPosition(this).X);
        _drag = DragTarget.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _drag = DragTarget.None;
    }

    private void ApplyDrag(double x)
    {
        var time = XToTime(x);
        switch (_drag)
        {
            case DragTarget.Playhead:
                Position = time;
                PositionChanged?.Invoke(time);
                break;
            case DragTarget.TrimStart:
                var clampedStart = time < TrimEnd - TimeSpan.FromMilliseconds(100) ? time : TrimStart;
                TrimStart = clampedStart;
                TrimStartChanged?.Invoke(clampedStart);
                break;
            case DragTarget.TrimEnd:
                var clampedEnd = time > TrimStart + TimeSpan.FromMilliseconds(100) ? time : TrimEnd;
                TrimEnd = clampedEnd;
                TrimEndChanged?.Invoke(clampedEnd);
                break;
        }
    }
}
