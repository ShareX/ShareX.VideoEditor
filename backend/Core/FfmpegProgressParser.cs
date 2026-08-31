#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Globalization;

namespace ShareX.VideoEditor.Core;

/// <summary>Parses FFmpeg's stable <c>-progress pipe:1</c> key/value protocol.</summary>
internal sealed class FfmpegProgressParser
{
    private readonly double _totalSeconds;
    private double _currentSeconds;
    private double _speed;
    private double _lastPercent;

    public FfmpegProgressParser(TimeSpan expectedDuration)
    {
        _totalSeconds = Math.Max(0, expectedDuration.TotalSeconds);
    }

    public VideoExportProgress? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        int separator = line.IndexOf('=');
        if (separator <= 0)
        {
            return null;
        }

        string key = line[..separator].Trim();
        string value = line[(separator + 1)..].Trim();

        switch (key)
        {
            case "out_time_us":
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds))
                {
                    _currentSeconds = Math.Max(_currentSeconds, Math.Max(0, microseconds / 1_000_000d));
                }
                break;

            case "out_time":
                if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan timestamp))
                {
                    _currentSeconds = Math.Max(_currentSeconds, Math.Max(0, timestamp.TotalSeconds));
                }
                break;

            case "speed":
                string normalized = value.EndsWith('x') ? value[..^1] : value;
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed) &&
                    double.IsFinite(speed))
                {
                    _speed = Math.Max(0, speed);
                }
                break;

            case "progress":
                if (value is "continue" or "end")
                {
                    return CreateSnapshot(value == "end");
                }
                break;
        }

        return null;
    }

    private VideoExportProgress CreateSnapshot(bool complete)
    {
        double calculatedPercent = complete
            ? 99.9
            : _totalSeconds > 0
                ? Math.Clamp((_currentSeconds / _totalSeconds) * 100, 0, 99.9)
                : 0;
        double percent = Math.Max(_lastPercent, calculatedPercent);
        _lastPercent = percent;
        string speedSuffix = _speed > 0 ? $" — {_speed:F1}x" : string.Empty;
        string elapsed = TimeSpan.FromSeconds(_currentSeconds)
            .ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        return new VideoExportProgress
        {
            ProgressPercent = percent,
            CurrentTime = TimeSpan.FromSeconds(_currentSeconds),
            Speed = _speed,
            StatusMessage = _totalSeconds > 0
                ? $"Encoding… {percent:F0}%{speedSuffix}"
                : $"Encoding… {elapsed}{speedSuffix}"
        };
    }
}
