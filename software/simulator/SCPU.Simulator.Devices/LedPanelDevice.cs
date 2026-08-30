using SCPU.Simulator.Core;

namespace SCPU.Simulator.Devices;

/// <summary>
/// LED and hexadecimal display panel connected to MMIO device 0 (0x12800-0x128FF).
/// </summary>
public sealed class LedPanelDevice : IODevice
{
    private readonly ushort[] _registers = new ushort[256];

    /// <summary>Gets the first hexadecimal display value (0x12801).</summary>
    public ushort Display1 => _registers[LedPanelRegisters.Display1];

    /// <summary>Gets the LED output register (0x12802).</summary>
    public ushort Leds => _registers[LedPanelRegisters.Leds];

    /// <summary>Raised after a register value changes.</summary>
    public event EventHandler<DeviceRegisterChangedEventArgs>? RegisterChanged;

    /// <summary>Reads or writes a panel register.</summary>
    public override ushort this[byte address]
    {
        get => _registers[address];
        set
        {
            if (_registers[address] == value) return;
            _registers[address] = value;
            RegisterChanged?.Invoke(this, new DeviceRegisterChangedEventArgs(address, value));
        }
    }
    /// <inheritdoc />
    public override void Reset() => Array.Clear(_registers);
}

/// <summary>Register offsets for MMIO device 0.</summary>
public static class LedPanelRegisters
{
    /// <summary>First 16-bit hexadecimal display (0x12801).</summary>
    public const byte Display1 = 0x01;

    /// <summary>LED output register (0x12802). Frontends may expose the low bits as a bank.</summary>
    public const byte Leds = 0x02;
}

/// <summary>Describes a changed panel register.</summary>
/// <param name="Address">Register offset inside device 0.</param>
/// <param name="Value">New 16-bit value.</param>
public sealed record DeviceRegisterChangedEventArgs(byte Address, ushort Value);
