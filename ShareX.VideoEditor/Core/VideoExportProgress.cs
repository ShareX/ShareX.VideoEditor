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

namespace ShareX.VideoEditor.Core;

/// <summary>
/// Granular progress snapshot emitted by <see cref="VideoExportService"/> during encoding.
/// </summary>
public sealed class VideoExportProgress
{
    /// <summary>Overall progress: 0–100.</summary>
    public double ProgressPercent { get; init; }

    /// <summary>Human-readable status line (e.g. "Encoding frame 420 / 1200").</summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>Current encoded position in the output.</summary>
    public TimeSpan CurrentTime { get; init; }

    /// <summary>Encoding speed multiplier reported by FFmpeg (e.g. 2.5x).</summary>
    public double Speed { get; init; }
}
