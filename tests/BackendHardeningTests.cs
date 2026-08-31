#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShareX.VideoEditor.Core;
using ShareX.VideoEditor.Hosting.Bridge;

namespace ShareX.VideoEditor.Tests;

[TestFixture]
public sealed class BackendHardeningTests
{
    [Test]
    public void ProgressParser_EmitsMachineReadableSnapshot()
    {
        var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(20));

        Assert.That(parser.ParseLine("out_time_us=5000000"), Is.Null);
        Assert.That(parser.ParseLine("speed=2.5x"), Is.Null);
        VideoExportProgress? progress = parser.ParseLine("progress=continue");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress, Is.Not.Null);
            Assert.That(progress!.CurrentTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(progress.ProgressPercent, Is.EqualTo(25).Within(0.001));
            Assert.That(progress.Speed, Is.EqualTo(2.5));
        }
    }

    [Test]
    public void ProgressParser_DoesNotReportOneHundredBeforeCommit()
    {
        var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(1));
        _ = parser.ParseLine("out_time_us=1000000");

        VideoExportProgress? progress = parser.ParseLine("progress=end");

        Assert.That(progress!.ProgressPercent, Is.LessThan(100));
    }

    [Test]
    public void ProgressParser_IsMonotonicAndRemainsBelowCommitProgress()
    {
        var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(10));
        _ = parser.ParseLine("out_time_us=9000000");
        double first = parser.ParseLine("progress=continue")!.ProgressPercent;
        _ = parser.ParseLine("out_time_us=2000000");
        double second = parser.ParseLine("progress=continue")!.ProgressPercent;
        _ = parser.ParseLine("out_time_us=20000000");
        double third = parser.ParseLine("progress=continue")!.ProgressPercent;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(third, Is.EqualTo(99.9).Within(0.001));
        }
    }

    [Test]
    public void CommandLineParser_PreservesSpacesAndEscapedQuotes()
    {
        IReadOnlyList<string> arguments = CommandLineArgumentParser.Parse(
            "-i \"C:\\video files\\source.mp4\" -metadata \"title=An \\\"exact\\\" clip\"");

        Assert.That(arguments, Is.EqualTo(new[]
        {
            "-i",
            "C:\\video files\\source.mp4",
            "-metadata",
            "title=An \"exact\" clip"
        }));
    }

    [Test]
    public void BridgeValidator_AcceptsBoundedCorrelatedExport()
    {
        string requestId = Guid.NewGuid().ToString();
        var message = new JObject
        {
            ["type"] = "requestExport",
            ["requestId"] = requestId,
            ["outputFormat"] = "MP4",
            ["fps"] = 30,
            ["qualityScale"] = 1,
            ["isTrimActive"] = true,
            ["trimStart"] = 1,
            ["trimEnd"] = 2,
            ["isCropActive"] = false,
            ["cropX"] = 0,
            ["cropY"] = 0,
            ["cropWidth"] = 0,
            ["cropHeight"] = 0,
            ["watermarkText"] = string.Empty,
            ["watermarkImagePath"] = "Logo.PnG",
            ["watermarkEnabled"] = false
        };

        BridgeMessageValidationResult result = BridgeMessageValidator.Validate(message.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.True, result.Error);
            Assert.That(result.RequestId, Is.EqualTo(requestId));
        }
    }

    [TestCase("{not json", "not valid JSON")]
    [TestCase("{\"type\":\"unexpected\"}", "Unknown")]
    public void BridgeValidator_RejectsMalformedEnvelope(string message, string expectedError)
    {
        BridgeMessageValidationResult result = BridgeMessageValidator.Validate(message);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Does.Contain(expectedError));
        }
    }

    [Test]
    public void BridgeValidator_RejectsInvalidThumbnailBounds()
    {
        var message = new JObject
        {
            ["type"] = "requestThumbnails",
            ["requestId"] = Guid.NewGuid().ToString(),
            ["revision"] = 0,
            ["startTime"] = 5,
            ["endTime"] = 4,
            ["count"] = 121
        };

        BridgeMessageValidationResult result = BridgeMessageValidator.Validate(message.ToString());

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("{\"type\":\"ready\",\"protocolVersion\":1}")]
    [TestCase("{\"type\":\"ready\"}")]
    [TestCase("{\"type\":\"ready\",\"protocolVersion\":2,\"extra\":true}")]
    public void BridgeValidator_RejectsUnsupportedOrLooseReadyEnvelope(string message)
    {
        Assert.That(BridgeMessageValidator.Validate(message).IsValid, Is.False);
    }

    [Test]
    public void BridgeValidator_RejectsDuplicateProperties()
    {
        const string message = "{\"type\":\"ready\",\"type\":\"ready\",\"protocolVersion\":2}";

        Assert.That(BridgeMessageValidator.Validate(message).IsValid, Is.False);
    }

    [Test]
    public void FfmpegArgumentBuilder_ProducesStructuredPathAndFilterArguments()
    {
        var options = new VideoExportOptions
        {
            InputPath = "source clip.mp4",
            OutputPath = "final clip.mp4",
            IsCropActive = true,
            CropWidth = 1920,
            CropHeight = 1080,
            OutputFps = 30
        };

        IReadOnlyList<string> arguments = FfmpegArgumentBuilder.BuildArguments(options, "staging clip.mp4");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments, Does.Contain("source clip.mp4"));
            Assert.That(arguments, Does.Contain("crop=1920:1080:0:0,fps=30"));
            Assert.That(arguments[^1], Is.EqualTo("staging clip.mp4"));
            Assert.That(arguments.Any(static argument => argument.Contains('"')), Is.False);
        }
    }

    [Test]
    public async Task ProcessRunner_DrainsStandardOutputAndUsesStructuredArguments()
    {
        FfmpegProcessResult result = await FfmpegProcessRunner.RunAsync(
            "dotnet",
            ["--version"],
            timeout: TimeSpan.FromSeconds(15));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardOutput, Is.Not.Empty);
        }
    }

    [Test]
    public async Task ProcessRunner_ContinuesDrainingWhenObserverThrows()
    {
        FfmpegProcessResult result = await FfmpegProcessRunner.RunAsync(
            "dotnet",
            ["--version"],
            _ => throw new InvalidOperationException("observer failure"),
            timeout: TimeSpan.FromSeconds(15));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardOutput, Is.Not.Empty);
        }
    }

    [Test]
    public async Task ProcessRunner_DoesNotRequireAPumpingSynchronizationContext()
    {
        var completion = new TaskCompletionSource<FfmpegProcessResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            try
            {
                completion.SetResult(FfmpegProcessRunner.RunAsync(
                    "dotnet",
                    ["--version"],
                    timeout: TimeSpan.FromSeconds(15)).GetAwaiter().GetResult());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
        FfmpegProcessResult result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.That(result.ExitCode, Is.Zero);
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }
}
