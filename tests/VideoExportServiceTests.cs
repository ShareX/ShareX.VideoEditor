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

using ShareX.VideoEditor.Core;
using NUnit.Framework;

namespace ShareX.VideoEditor.Tests;

[TestFixture]
public sealed class VideoExportServiceTests
{
    private string _testDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "ShareX.VideoEditor.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_testDirectory, recursive: true); } catch { }
    }

    [Test]
    public void CreateStagingOutputPath_UsesSameDirectoryAndExtension()
    {
        string outputPath = Path.Combine(_testDirectory, "edited clip.mp4");

        string first = VideoExportService.CreateStagingOutputPath(outputPath);
        string second = VideoExportService.CreateStagingOutputPath(outputPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Path.GetDirectoryName(first), Is.EqualTo(_testDirectory));
            Assert.That(Path.GetExtension(first), Is.EqualTo(".mp4"));
            Assert.That(Path.GetFileName(first), Does.Contain(".part.mp4"));
            Assert.That(second, Is.Not.EqualTo(first));
        }
    }

    [Test]
    public void CommitStagedOutput_ReplacesExistingDestinationOnlyAfterSuccess()
    {
        string outputPath = Path.Combine(_testDirectory, "edited.mp4");
        string stagingPath = VideoExportService.CreateStagingOutputPath(outputPath);
        File.WriteAllText(outputPath, "original");
        File.WriteAllText(stagingPath, "completed export");

        VideoExportService.CommitStagedOutput(stagingPath, outputPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(outputPath), Is.EqualTo("completed export"));
            Assert.That(File.Exists(stagingPath), Is.False);
        }
    }

    [Test]
    public void CommitStagedOutput_RejectsEmptyOutputAndPreservesDestination()
    {
        string outputPath = Path.Combine(_testDirectory, "edited.mp4");
        string stagingPath = VideoExportService.CreateStagingOutputPath(outputPath);
        File.WriteAllText(outputPath, "original");
        File.WriteAllBytes(stagingPath, []);

        Assert.Throws<InvalidOperationException>(() =>
            VideoExportService.CommitStagedOutput(stagingPath, outputPath));

        Assert.That(File.ReadAllText(outputPath), Is.EqualTo("original"));
    }

    [Test]
    public void ReplaceTrailingOutputPath_StagesCustomArgumentsWithoutChangingOtherPaths()
    {
        string outputPath = Path.Combine(_testDirectory, "edited clip.mp4");
        string stagingPath = VideoExportService.CreateStagingOutputPath(outputPath);
        string arguments = $"-i \"source clip.mp4\" -y \"{outputPath}\"";

        string stagedArguments = VideoExportService.ReplaceTrailingOutputPath(
            arguments,
            outputPath,
            stagingPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stagedArguments, Does.StartWith("-i \"source clip.mp4\" -y "));
            Assert.That(stagedArguments, Does.EndWith($"\"{stagingPath}\""));
            Assert.That(stagedArguments, Does.Not.EndWith($"\"{outputPath}\""));
        }
    }

    [Test]
    public void FfmpegArguments_PreserveLongPreciseTimestampsAndStagingDestination()
    {
        var options = new VideoExportOptions
        {
            InputPath = "source.mp4",
            OutputPath = "final.mp4",
            IsTrimActive = true,
            TrimStart = TimeSpan.FromSeconds(90_061.123456),
            TrimEnd = TimeSpan.FromSeconds(90_062.654321)
        };

        string arguments = FfmpegArgumentBuilder.Build(options, "staging.mp4");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments, Does.Contain("-ss 90061.123456"));
            Assert.That(arguments, Does.Contain("-t 1.530865"));
            Assert.That(arguments, Does.EndWith("-y \"staging.mp4\""));
        }
    }

    [Test]
    public void ExportAsync_RejectsSourceAsDestinationBeforeStartingFfmpeg()
    {
        string videoPath = Path.Combine(_testDirectory, "source.mp4");
        File.WriteAllText(videoPath, "source");
        var options = new VideoExportOptions
        {
            InputPath = videoPath,
            OutputPath = videoPath
        };
        var service = new VideoExportService(Path.Combine(_testDirectory, "missing-ffmpeg"));

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ExportAsync(options));

        Assert.That(exception!.Message, Does.Contain("different from the source"));
        Assert.That(File.ReadAllText(videoPath), Is.EqualTo("source"));
    }
}
