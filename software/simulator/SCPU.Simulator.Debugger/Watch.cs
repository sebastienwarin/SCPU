namespace SCPU.Simulator.Debugger;

/// <summary>A named memory value kept in the debugger watch list.</summary>
public sealed record Watch(int Id, uint Address, string Expression);
