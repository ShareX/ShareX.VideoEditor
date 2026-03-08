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
/// Raised when the editor host cannot start because a required native runtime dependency is missing.
/// </summary>
public sealed class VideoEditorDependencyException : InvalidOperationException
{
    public string? NativeLibraryPath { get; }
    public IReadOnlyList<string> MissingLibraries { get; }

    public VideoEditorDependencyException(
        string message,
        Exception innerException,
        string? nativeLibraryPath = null,
        IReadOnlyList<string>? missingLibraries = null)
        : base(message, innerException)
    {
        NativeLibraryPath = nativeLibraryPath;
        MissingLibraries = missingLibraries ?? Array.Empty<string>();
    }
}
