namespace SCPU.Simulator.CLI.Infrastructure;

/// <summary>Prevents asynchronous device output from corrupting a live terminal view.</summary>
public sealed class InteractiveConsoleState
{
    private int _isActive;
    public bool IsActive { get => Volatile.Read(ref _isActive) != 0; set => Volatile.Write(ref _isActive, value ? 1 : 0); }
}
