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

using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShareX.VideoEditor.Hosting.Diagnostics;

public sealed class VideoEditorPackageReferenceInfo
{
    [JsonProperty("name")]
    public string Name { get; }

    [JsonProperty("version")]
    public string Version { get; }

    public VideoEditorPackageReferenceInfo(string name, string version)
    {
        Name = name ?? string.Empty;
        Version = version ?? string.Empty;
    }
}

public sealed class VideoEditorLoadedAssemblyInfo
{
    [JsonProperty("name")]
    public string Name { get; }

    [JsonProperty("isLoaded")]
    public bool IsLoaded { get; }

    [JsonProperty("assemblyVersion")]
    public string AssemblyVersion { get; }

    [JsonProperty("informationalVersion")]
    public string InformationalVersion { get; }

    [JsonProperty("fileVersion")]
    public string FileVersion { get; }

    [JsonProperty("location")]
    public string Location { get; }

    public VideoEditorLoadedAssemblyInfo(
        string name,
        bool isLoaded,
        string assemblyVersion,
        string informationalVersion,
        string fileVersion,
        string location)
    {
        Name = name ?? string.Empty;
        IsLoaded = isLoaded;
        AssemblyVersion = assemblyVersion ?? string.Empty;
        InformationalVersion = informationalVersion ?? string.Empty;
        FileVersion = fileVersion ?? string.Empty;
        Location = location ?? string.Empty;
    }
}

public sealed class VideoEditorRuntimeDiagnosticsSnapshot
{
    [JsonProperty("packageReferences")]
    public IReadOnlyList<VideoEditorPackageReferenceInfo> PackageReferences { get; }

    [JsonProperty("loadedAssemblies")]
    public IReadOnlyList<VideoEditorLoadedAssemblyInfo> LoadedAssemblies { get; }

    public VideoEditorRuntimeDiagnosticsSnapshot(
        IReadOnlyList<VideoEditorPackageReferenceInfo> packageReferences,
        IReadOnlyList<VideoEditorLoadedAssemblyInfo> loadedAssemblies)
    {
        PackageReferences = packageReferences;
        LoadedAssemblies = loadedAssemblies;
    }
}

internal static class VideoEditorRuntimeDiagnosticsCollector
{
    public static VideoEditorRuntimeDiagnosticsSnapshot Capture()
    {
        VideoEditorPackageReferenceInfo[] packageReferences = CreatePackageReferences()
            .OrderBy(packageReference => packageReference.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var loadedAssembliesByName = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .GroupBy(assembly => assembly.GetName().Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var relevantAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            typeof(VideoEditorHost).Assembly.GetName().Name ?? "ShareX.VideoEditor"
        };

        foreach (VideoEditorPackageReferenceInfo packageReference in packageReferences)
        {
            relevantAssemblyNames.Add(packageReference.Name);
        }

        VideoEditorLoadedAssemblyInfo[] loadedAssemblies = relevantAssemblyNames
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                loadedAssembliesByName.TryGetValue(name, out Assembly? assembly);
                return CreateLoadedAssemblyInfo(name, assembly);
            })
            .ToArray();

        return new VideoEditorRuntimeDiagnosticsSnapshot(packageReferences, loadedAssemblies);
    }

    private static IReadOnlyList<VideoEditorPackageReferenceInfo> CreatePackageReferences()
    {
        foreach (string depsFilePath in EnumerateDependencyFiles())
        {
            if (TryReadPackageReferencesFromDepsFile(depsFilePath, out VideoEditorPackageReferenceInfo[] packageReferences))
            {
                return packageReferences;
            }
        }

        return Array.Empty<VideoEditorPackageReferenceInfo>();
    }

    private static IEnumerable<string> EnumerateDependencyFiles()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (AppContext.GetData("APP_CONTEXT_DEPS_FILES") is string appContextDepsFiles)
        {
            foreach (string depsFilePath in appContextDepsFiles.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (File.Exists(depsFilePath))
                {
                    paths.Add(depsFilePath);
                }
            }
        }

        AddDependencyFileForAssembly(paths, Assembly.GetEntryAssembly());
        AddDependencyFileForAssembly(paths, typeof(VideoEditorHost).Assembly);

        return paths;
    }

    private static void AddDependencyFileForAssembly(ISet<string> paths, Assembly? assembly)
    {
        if (assembly == null)
        {
            return;
        }

        string assemblyLocation = GetAssemblyLocation(assembly);

        if (string.IsNullOrWhiteSpace(assemblyLocation))
        {
            return;
        }

        string depsFilePath = Path.ChangeExtension(assemblyLocation, ".deps.json");

        if (File.Exists(depsFilePath))
        {
            paths.Add(depsFilePath);
        }
    }

    private static bool TryReadPackageReferencesFromDepsFile(string depsFilePath, out VideoEditorPackageReferenceInfo[] packageReferences)
    {
        packageReferences = Array.Empty<VideoEditorPackageReferenceInfo>();

        try
        {
            var root = JObject.Parse(File.ReadAllText(depsFilePath));
            var libraries = root["libraries"] as JObject;

            if (libraries == null)
            {
                return false;
            }

            string projectAssemblyName = typeof(VideoEditorHost).Assembly.GetName().Name ?? "ShareX.VideoEditor";
            string? projectLibraryKey = libraries.Properties()
                .Select(property => new
                {
                    property.Name,
                    Type = property.Value["type"]?.Value<string>()
                })
                .FirstOrDefault(entry =>
                    string.Equals(entry.Type, "project", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetLibraryName(entry.Name), projectAssemblyName, StringComparison.OrdinalIgnoreCase))
                ?.Name;

            if (string.IsNullOrWhiteSpace(projectLibraryKey))
            {
                return false;
            }

            JObject? targets = root["targets"] as JObject;

            if (targets == null)
            {
                return false;
            }

            string? runtimeTargetName = root["runtimeTarget"]?["name"]?.Value<string>();
            JObject? runtimeTarget = !string.IsNullOrWhiteSpace(runtimeTargetName)
                ? targets[runtimeTargetName] as JObject
                : targets.Properties().Select(property => property.Value as JObject).FirstOrDefault(target => target != null);

            JObject? projectTarget = runtimeTarget?[projectLibraryKey] as JObject;
            JObject? dependencyMap = projectTarget?["dependencies"] as JObject;

            if (dependencyMap == null || dependencyMap.Count == 0)
            {
                return false;
            }

            packageReferences = dependencyMap.Properties()
                .Select(dependency => CreatePackageReferenceInfo(libraries, dependency))
                .Where(packageReference => packageReference != null)
                .Cast<VideoEditorPackageReferenceInfo>()
                .OrderBy(packageReference => packageReference.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return packageReferences.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static VideoEditorPackageReferenceInfo? CreatePackageReferenceInfo(JObject libraries, JProperty dependency)
    {
        string packageName = dependency.Name;
        string requestedVersion = dependency.Value.Value<string>() ?? string.Empty;
        string exactLibraryKey = $"{packageName}/{requestedVersion}";

        if (libraries[exactLibraryKey] is JObject exactLibrary &&
            string.Equals(exactLibrary["type"]?.Value<string>(), "package", StringComparison.OrdinalIgnoreCase))
        {
            return new VideoEditorPackageReferenceInfo(packageName, requestedVersion);
        }

        JProperty? resolvedPackage = libraries.Properties()
            .FirstOrDefault(property =>
                string.Equals(GetLibraryName(property.Name), packageName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(property.Value["type"]?.Value<string>(), "package", StringComparison.OrdinalIgnoreCase));

        if (resolvedPackage != null)
        {
            return new VideoEditorPackageReferenceInfo(packageName, GetLibraryVersion(resolvedPackage.Name));
        }

        return string.IsNullOrWhiteSpace(requestedVersion)
            ? null
            : new VideoEditorPackageReferenceInfo(packageName, requestedVersion);
    }

    private static string GetLibraryName(string libraryKey)
    {
        int separatorIndex = libraryKey.LastIndexOf('/');
        return separatorIndex >= 0 ? libraryKey[..separatorIndex] : libraryKey;
    }

    private static string GetLibraryVersion(string libraryKey)
    {
        int separatorIndex = libraryKey.LastIndexOf('/');
        return separatorIndex >= 0 && separatorIndex < libraryKey.Length - 1
            ? libraryKey[(separatorIndex + 1)..]
            : string.Empty;
    }

    private static VideoEditorLoadedAssemblyInfo CreateLoadedAssemblyInfo(string name, Assembly? assembly)
    {
        if (assembly == null)
        {
            return new VideoEditorLoadedAssemblyInfo(name, false, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        string location = GetAssemblyLocation(assembly);
        string fileVersion = string.Empty;

        if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
        {
            try
            {
                fileVersion = FileVersionInfo.GetVersionInfo(location).FileVersion ?? string.Empty;
            }
            catch
            {
                fileVersion = string.Empty;
            }
        }

        string assemblyVersion = assembly.GetName().Version?.ToString() ?? string.Empty;
        string informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? string.Empty;

        return new VideoEditorLoadedAssemblyInfo(
            name,
            true,
            assemblyVersion,
            informationalVersion,
            fileVersion,
            location);
    }

    private static string GetAssemblyLocation(Assembly assembly)
    {
        try
        {
            return assembly.Location;
        }
        catch
        {
            return string.Empty;
        }
    }
}
