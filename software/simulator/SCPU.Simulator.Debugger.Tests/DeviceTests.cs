using SCPU.Simulator.Devices;
using SCPU.Simulator.Core;

namespace SCPU.Simulator.Debugger.Tests;

public sealed class DeviceTests
{
    [Fact]
    public void Terminal_buffers_output_and_input_without_ui_dependency()
    {
        var terminal = new BufferedTerminalDevice();
        terminal[TerminalRegisters.Output] = 'O';
        terminal[TerminalRegisters.Output] = 'K';
        terminal.Enqueue("A", appendNewLine: true);

        Assert.Equal("OK", terminal.Output);
        Assert.Equal(2, terminal.PendingInput);
        Assert.Equal((ushort)'A', terminal[TerminalRegisters.Input]);
        Assert.Equal((ushort)'\n', terminal[TerminalRegisters.Input]);
    }

    [Fact]
    public void Clearing_terminal_output_keeps_pending_input()
    {
        var terminal = new BufferedTerminalDevice();
        terminal[TerminalRegisters.Output] = 'X';
        terminal.Enqueue("A");

        terminal.ClearOutput();

        Assert.Empty(terminal.Output);
        Assert.Equal(1, terminal.PendingInput);
    }

    [Fact]
    public void Cpu_reset_resets_all_mmio_devices()
    {
        var terminal = new BufferedTerminalDevice();
        var panel = new LedPanelDevice();
        terminal[TerminalRegisters.Output] = 'X';
        terminal.Enqueue("A");
        panel[LedPanelRegisters.Display1] = 0x1234;
        panel[LedPanelRegisters.Leds] = 0x00FF;
        var cpu = new Processor();
        cpu.Devices.Add(DeviceId.Device0, panel);
        cpu.Devices.Add(DeviceId.Device1, terminal);

        cpu.Reset();

        Assert.Empty(terminal.Output);
        Assert.Equal(0, terminal.PendingInput);
        Assert.Equal(0, panel.Display1);
        Assert.Equal(0, panel.Leds);
    }

    [Fact]
    public void Demo_panel_exposes_the_display_and_led_registers()
    {
        var panel = new LedPanelDevice();
        panel[LedPanelRegisters.Display1] = 0x1234;
        panel[LedPanelRegisters.Leds] = 0x00A5;

        Assert.Equal(0x1234, panel.Display1);
        Assert.Equal(0x00A5, panel.Leds);
    }

    [Fact]
    public void Debugger_bus_inspection_does_not_consume_terminal_input()
    {
        var terminal = new BufferedTerminalDevice();
        terminal.Enqueue("A");
        var cpu = new Processor();
        cpu.Devices.Add(DeviceId.Device1, terminal);
        cpu.Load([0x29, 0x02]); // NOR from terminal input register (device 1, register 2)
        cpu.Tick();

        Assert.Equal((ushort)'A', cpu.PeekDataBus);
        Assert.Equal((ushort)'A', cpu.PeekDataBus);
        Assert.Equal(1, terminal.PendingInput);

        cpu.Tick();
        Assert.Equal(0, terminal.PendingInput);
    }
}
