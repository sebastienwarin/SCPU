using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SCPU.Simulator.Debugger;

/// <summary>Runs a debug session sequentially on a cancellable background loop.</summary>
public sealed class SimulationRunner(DebugSession session, ILogger<SimulationRunner> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private double _actualFrequency;
    private double _actualInstructionsPerSecond;
    private readonly Stopwatch _executionTime = new();
    private int _targetFrequency = 2_000_000;

    /// <summary>Raised when a throttled UI snapshot is available.</summary>
    public event EventHandler<CpuSnapshot>? SnapshotAvailable;

    /// <summary>Gets or sets target hardware cycles per second. Zero means maximum speed.</summary>
    public int TargetFrequency
    {
        get => Volatile.Read(ref _targetFrequency);
        set
        {
            Volatile.Write(ref _targetFrequency, Math.Max(0, value));
            _actualFrequency = 0;
            _actualInstructionsPerSecond = 0;
        }
    }

    /// <summary>Gets or sets the maximum snapshot publication rate.</summary>
    public int RefreshFrequency { get; set; } = 20;

    /// <summary>Gets or sets whether detected HALT instructions stop continuous execution.</summary>
    public bool StopOnHalt { get; set; } = true;

    /// <summary>Gets whether a continuous run is active.</summary>
    public bool IsRunning => _runTask is { IsCompleted: false };

    /// <summary>Gets the most recently measured hardware cycles per second.</summary>
    public double ActualFrequency => _actualFrequency;

    /// <summary>Gets the most recently measured instructions per second.</summary>
    public double ActualInstructionsPerSecond => _actualInstructionsPerSecond;

    /// <summary>Starts continuous execution when no run is already active.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
                return;
            if (session.State == SimulatorState.Ready && session.CycleCount == 0)
                _executionTime.Reset();
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executionTime.Start();
            session.State = SimulatorState.Running;
            Publish();
            // Task.Run also guarantees that a very short run cannot complete before
            // _runTask is published, keeping concurrent RunAsync calls idempotent.
            _runTask = Task.Run(() => RunLoopAsync(_runCancellation.Token));
        }
        finally { _gate.Release(); }
    }

    /// <summary>Cancels the active run and waits until it has stopped.</summary>
    public async Task PauseAsync()
    {
        Task? task;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _runCancellation?.Cancel();
            task = _runTask;
        }
        finally { _gate.Release(); }

        if (task is not null)
            await task.ConfigureAwait(false);
    }

    /// <summary>Pauses and executes one complete instruction.</summary>
    public async Task StepInstructionAsync(CancellationToken cancellationToken = default)
    {
        await PauseAsync();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            session.StepInstruction(cancellationToken);
            session.State = SimulatorState.Paused;
            session.LastStopReason = StopReason.Paused;
            Publish();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Pauses and advances to the next mapped source line.</summary>
    public async Task StepSourceAsync(CancellationToken cancellationToken = default)
    {
        await PauseAsync();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() => session.StepSource(cancellationToken), cancellationToken);
            session.State = SimulatorState.Paused;
            session.LastStopReason = StopReason.Paused;
            Publish();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Pauses and executes one hardware cycle.</summary>
    public async Task StepCycleAsync(CancellationToken cancellationToken = default)
    {
        await PauseAsync();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            session.StepCycle();
            session.State = SimulatorState.Paused;
            session.LastStopReason = StopReason.Paused;
            Publish();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Pauses and resets the debug session.</summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await PauseAsync();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            session.Reset();
            _executionTime.Reset();
            Publish();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Resets the session and immediately starts continuous execution.</summary>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await ResetAsync(cancellationToken);
        await RunAsync(cancellationToken);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var measurement = Stopwatch.StartNew();
        var refresh = Stopwatch.StartNew();
        var measuredCycles = 0L;
        var measuredInstructionStart = session.InstructionCount;
        var firstCycle = true;
        var firstIteration = true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check stop conditions OUTSIDE the gate to keep UI responsive
                if (StopOnHalt && session.IsAtHalt) { Stop(SimulatorState.Halted, StopReason.Halt); break; }
                if (!firstCycle && session.Breakpoints.Contains(session.Cpu.ProgramCounter) && session.Cpu.ShouldFetchIR)
                { Stop(SimulatorState.Breakpoint, StopReason.Breakpoint); break; }

                // Calculate batch parameters
                var targetFrequency = TargetFrequency;
                var batch = targetFrequency <= 0
                    ? 10_000
                    : Math.Clamp(targetFrequency / Math.Max(RefreshFrequency, 1), 1, 100_000);
                var batchStart = Stopwatch.GetTimestamp();
                
                // Execute batch cycles WITH the gate held
                await _gate.WaitAsync();
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    for (var i = 0; i < batch && !cancellationToken.IsCancellationRequested; i++)
                    {
                        session.StepCycle();
                        measuredCycles++;
                        firstCycle = false;
                        if (StopOnHalt && session.IsAtHalt)
                        {
                            Stop(SimulatorState.Halted, StopReason.Halt);
                            break;
                        }
                        if (session.Breakpoints.Contains(session.Cpu.ProgramCounter) && session.Cpu.ShouldFetchIR)
                        {
                            Stop(SimulatorState.Breakpoint, StopReason.Breakpoint);
                            break;
                        }
                    }

                    if (session.State != SimulatorState.Running)
                        break;
                }
                finally { _gate.Release(); }

                // Measurements and publishing OUTSIDE the gate
                if (measurement.ElapsedMilliseconds >= 500)
                {
                    var elapsedSeconds = measurement.Elapsed.TotalSeconds;
                    _actualFrequency = measuredCycles / elapsedSeconds;
                    _actualInstructionsPerSecond = (session.InstructionCount - measuredInstructionStart) / elapsedSeconds;
                    measuredCycles = 0;
                    measuredInstructionStart = session.InstructionCount;
                    measurement.Restart();
                }
                // Force a publish on the first iteration to ensure UI gets updated immediately
                if (firstIteration || refresh.Elapsed.TotalSeconds >= 1d / Math.Max(RefreshFrequency, 1))
                { Publish(); refresh.Restart(); firstIteration = false; }

                if (targetFrequency > 0)
                {
                    // Use the same frequency that produced this batch. A live
                    // change takes effect on the next iteration.
                    var expected = TimeSpan.FromSeconds((double)batch / targetFrequency);
                    var elapsed = Stopwatch.GetElapsedTime(batchStart);
                    if (expected > elapsed)
                        await Task.Delay(expected - elapsed, cancellationToken);
                }
                else
                {
                    await Task.Yield();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Simulation faulted.");
            session.Fault = exception.Message;
            Stop(SimulatorState.Faulted, StopReason.Faulted);
        }
        finally
        {
            _executionTime.Stop();
            if (session.State == SimulatorState.Running)
                Stop(SimulatorState.Paused, StopReason.Paused);
            Publish();
        }
    }

    private void Stop(SimulatorState state, StopReason reason)
    { session.State = state; session.LastStopReason = reason; }

    private void Publish() => SnapshotAvailable?.Invoke(this,
        session.Snapshot(_actualFrequency, _actualInstructionsPerSecond, _executionTime.Elapsed));

    /// <summary>Stops execution and releases runner resources.</summary>
    public async ValueTask DisposeAsync()
    {
        await PauseAsync().ConfigureAwait(false);
        _runCancellation?.Dispose();
        _gate.Dispose();
    }
}
