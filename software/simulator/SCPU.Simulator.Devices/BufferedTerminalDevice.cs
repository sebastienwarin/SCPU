using System.Text;
using SCPU.Simulator.Core;

namespace SCPU.Simulator.Devices;

/// <summary>
/// Buffered ASCII terminal connected to MMIO device 1 (0x12900-0x129FF).
/// </summary>
public sealed class BufferedTerminalDevice : IODevice
{
    private readonly object _sync = new();
    private readonly Queue<byte> _input = [];
    private readonly StringBuilder _output = new();

    /// <summary>Gets all characters written to register 0x01.</summary>
    public string Output { get { lock (_sync) return _output.ToString(); } }

    /// <summary>Gets the number of characters waiting in the input queue.</summary>
    public int PendingInput { get { lock (_sync) return _input.Count; } }

    /// <summary>Raised after a character is written to the output register.</summary>
    public event EventHandler<char>? OutputProduced;

    /// <summary>Reads or writes a terminal register.</summary>
    public override ushort this[byte address]
    {
        get
        {
            lock (_sync)
            {
                return address switch
                {
                    TerminalRegisters.Input => _input.TryDequeue(out var value) ? value : (ushort)0,
                    TerminalRegisters.InputAvailable => _input.Count > 0 ? (ushort)1 : (ushort)0,
                    _ => 0
                };
            }
        }
        set
        {
            if (address != TerminalRegisters.Output) return;
            var character = (char)(value & 0x7F);
            if (character == '\0') return;
            lock (_sync) _output.Append(character);
            OutputProduced?.Invoke(this, character);
        }
    }

    /// <inheritdoc />
    public override ushort Peek(byte address)
    {
        lock (_sync)
        {
            return address switch
            {
                TerminalRegisters.Input => _input.TryPeek(out var value) ? value : (ushort)0,
                TerminalRegisters.InputAvailable => _input.Count > 0 ? (ushort)1 : (ushort)0,
                _ => 0
            };
        }
    }

    /// <summary>Adds ASCII characters to the queue read by the simulated CPU.</summary>
    /// <param name="text">Characters to enqueue.</param>
    /// <param name="appendNewLine">Adds an LF character after the text when set.</param>
    public void Enqueue(string text, bool appendNewLine = false)
    {
        lock (_sync)
        {
            foreach (var value in Encoding.ASCII.GetBytes(text)) _input.Enqueue((byte)(value & 0x7F));
            if (appendNewLine) _input.Enqueue((byte)'\n');
        }
    }

    /// <summary>Clears displayed output without changing pending keyboard input.</summary>
    public void ClearOutput() { lock (_sync) _output.Clear(); }

    /// <inheritdoc />
    public override void Reset() { lock (_sync) { _input.Clear(); _output.Clear(); } }
}

/// <summary>Register offsets for MMIO device 1.</summary>
public static class TerminalRegisters
{
    /// <summary>Write-only ASCII output register (0x12901).</summary>
    public const byte Output = 0x01;

    /// <summary>Read-only input register; reading consumes one character (0x12902).</summary>
    public const byte Input = 0x02;

    /// <summary>Read-only flag: 1 when input is waiting, otherwise 0 (0x12903).</summary>
    public const byte InputAvailable = 0x03;
}
