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
using System.Text;

namespace ShareX.VideoEditor.Hosting;

internal static class VideoEditorRuntimeValidator
{
    public static void EnsureAvailable()
    {
        string? nativeLibraryPath = ResolvePhotinoNativeLibraryPath();
        if (string.IsNullOrWhiteSpace(nativeLibraryPath))
        {
            return;
        }

        nint handle = 0;

        try
        {
            handle = NativeLibrary.Load(nativeLibraryPath);
        }
        catch (Exception ex)
        {
            throw NormalizeStartupException(ex, nativeLibraryPath);
        }
        finally
        {
            if (handle != 0)
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    public static Exception NormalizeStartupException(Exception exception, string? nativeLibraryPath = null)
    {
        if (exception is VideoEditorDependencyException)
        {
            return exception;
        }

        Exception? loadException = FindNativeLoadException(exception);
        if (loadException == null || !OperatingSystem.IsLinux())
        {
            return exception;
        }

        string? resolvedNativeLibraryPath = nativeLibraryPath ?? ResolvePhotinoNativeLibraryPath();
        string[] missingLibraries = ExtractMissingLibraries(loadException.Message);
        string? webKitGtkVersion = GetMissingWebKitGtkVersion(missingLibraries);

        var message = new StringBuilder();
        message.Append("The video editor could not start because its native Photino webview host could not be loaded.");
        message.Append(' ');
        message.Append("On Linux, Photino requires ");
        message.Append(string.IsNullOrWhiteSpace(webKitGtkVersion)
            ? "WebKitGTK libraries."
            : $"WebKitGTK {webKitGtkVersion} libraries.");

        if (missingLibraries.Length > 0)
        {
            message.Append(' ');
            message.Append("Install the package(s) that provide these libraries and try again:");
            message.Append(' ');
            message.Append(string.Join(", ", missingLibraries));
            message.Append('.');
        }
        else
        {
            message.Append(' ');
            message.Append("Install the distro package(s) that provide the required WebKitGTK libraries and try again.");
        }

        string? installHint = GetLinuxInstallHint(missingLibraries, webKitGtkVersion);
        if (!string.IsNullOrWhiteSpace(installHint))
        {
            message.AppendLine();
            message.Append(installHint);
        }

        if (!string.IsNullOrWhiteSpace(resolvedNativeLibraryPath))
        {
            message.AppendLine();
            message.Append("Native library: ");
            message.Append(resolvedNativeLibraryPath);
        }

        return new VideoEditorDependencyException(
            message.ToString(),
            exception,
            resolvedNativeLibraryPath,
            missingLibraries);
    }

    private static Exception? FindNativeLoadException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is DllNotFoundException or BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                return current;
            }
        }

        return null;
    }

    private static string[] ExtractMissingLibraries(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Array.Empty<string>();
        }

        var missingLibraries = new List<string>();

        foreach (string rawLine in message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (!line.Contains("cannot open shared object file", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string token = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(token) ||
                token.Contains('/') ||
                token.Contains('\\') ||
                !token.Contains(".so", StringComparison.Ordinal))
            {
                continue;
            }

            missingLibraries.Add(token);
        }

        return missingLibraries
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ResolvePhotinoNativeLibraryPath()
    {
        string fileName = GetPhotinoNativeLibraryFileName();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string assemblyDir = Path.GetDirectoryName(typeof(VideoEditorHost).Assembly.Location)
            ?? AppContext.BaseDirectory;

        foreach (string candidate in EnumeratePhotinoNativeLibraryCandidates(assemblyDir, fileName))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? GetLinuxInstallHint(
        IReadOnlyCollection<string> missingLibraries,
        string? webKitGtkVersion)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(webKitGtkVersion))
        {
            return null;
        }

        string? distroId = TryGetLinuxDistroId();

        if (string.Equals(distroId, "fedora", StringComparison.OrdinalIgnoreCase))
        {
            return webKitGtkVersion switch
            {
                "4.1" => "Fedora: sudo dnf install webkit2gtk4.1 javascriptcoregtk4.1",
                "4.0" => "Fedora: sudo dnf install webkit2gtk4.0 javascriptcoregtk4.0",
                _ => null
            };
        }

        return null;
    }

    private static string? GetMissingWebKitGtkVersion(IReadOnlyCollection<string> missingLibraries)
    {
        if (missingLibraries.Any(name =>
                name.StartsWith("libwebkit2gtk-4.1", StringComparison.Ordinal) ||
                name.StartsWith("libjavascriptcoregtk-4.1", StringComparison.Ordinal)))
        {
            return "4.1";
        }

        if (missingLibraries.Any(name =>
                name.StartsWith("libwebkit2gtk-4.0", StringComparison.Ordinal) ||
                name.StartsWith("libjavascriptcoregtk-4.0", StringComparison.Ordinal)))
        {
            return "4.0";
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePhotinoNativeLibraryCandidates(string assemblyDir, string fileName)
    {
        string? runtimeIdentifier = GetCurrentRuntimeIdentifier();

        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            yield return Path.Combine(assemblyDir, "runtimes", runtimeIdentifier, "native", fileName);
        }

        yield return Path.Combine(assemblyDir, fileName);

        string? dir = assemblyDir;

        for (int i = 0; i < 6 && dir != null; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                yield return Path.Combine(dir, "runtimes", runtimeIdentifier, "native", fileName);
            }

            yield return Path.Combine(dir, fileName);
        }
    }

    private static string GetPhotinoNativeLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Photino.Native.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "Photino.Native.dylib";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Photino.Native.so";
        }

        return string.Empty;
    }

    private static string? TryGetLinuxDistroId()
    {
        const string osReleasePath = "/etc/os-release";

        if (!File.Exists(osReleasePath))
        {
            return null;
        }

        try
        {
            foreach (string line in File.ReadLines(osReleasePath))
            {
                if (!line.StartsWith("ID=", StringComparison.Ordinal))
                {
                    continue;
                }

                return line["ID=".Length..].Trim().Trim('"', '\'');
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? GetCurrentRuntimeIdentifier()
    {
        if (OperatingSystem.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                Architecture.X86 => "win-x86",
                Architecture.Arm => "win-arm",
                _ => null
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => null
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                Architecture.X86 => "linux-x86",
                Architecture.Arm => "linux-arm",
                _ => null
            };
        }

        return null;
    }
}
