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

using System.Diagnostics;
using System.Text;

namespace ShareX.VideoEditor.Core;

internal sealed record FfmpegProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs FFmpeg-family executables without shell parsing and guarantees that both
/// redirected streams are drained while the process is alive.
/// </summary>
internal static class FfmpegProcessRunner
{
    private static readonly TimeSpan DefaultReapTimeout = TimeSpan.FromSeconds(5);

    public static async Task<FfmpegProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string>? onStandardOutputLine = null,
        Action<string>? onStandardErrorLine = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{executablePath}'.");
        }

        using var timeoutCts = timeout is { } value && value != Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource(value)
            : null;
        using var linkedCts = timeoutCts == null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        Task stdoutTask = DrainAsync(process.StandardOutput, stdout, onStandardOutputLine, linkedCts.Token);
        Task stderrTask = DrainAsync(process.StandardError, stderr, onStandardErrorLine, linkedCts.Token);

        try
        {
            Task exitTask = process.WaitForExitAsync(linkedCts.Token);
            Task drainTask = Task.WhenAll(stdoutTask, stderrTask);
            Task firstCompleted = await Task.WhenAny(exitTask, drainTask).ConfigureAwait(false);
            if (ReferenceEquals(firstCompleted, drainTask) && !drainTask.IsCompletedSuccessfully)
            {
                await drainTask.ConfigureAwait(false);
            }

            await exitTask.ConfigureAwait(false);
            await drainTask.ConfigureAwait(false);
            return new FfmpegProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await ReapAndDrainAsync(process, stdoutTask, stderrTask);
            throw new TimeoutException($"Process '{Path.GetFileName(executablePath)}' exceeded its {timeout!.Value.TotalSeconds:0.###} second timeout.");
        }
        catch
        {
            TryKill(process);
            await ReapAndDrainAsync(process, stdoutTask, stderrTask);
            throw;
        }
    }

    private static async Task DrainAsync(
        StreamReader reader,
        StringBuilder destination,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                return;
            }

            destination.AppendLine(line);
            try
            {
                onLine?.Invoke(line);
            }
            catch
            {
                // Observers must not be able to stop a redirected pipe from
                // draining and deadlock a long-running encoder.
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static async Task ReapAndDrainAsync(Process process, Task stdoutTask, Task stderrTask)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(DefaultReapTimeout).ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(DefaultReapTimeout).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
