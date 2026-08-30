using SCPU.Simulator.Core;

namespace SCode.Compiler.Tests.Support
{
    /// <summary>
    /// Simple IO device used by tests at DeviceId.Device0 (MMIO 0x2800..0x28FF).
    /// </summary>
    internal class TestDevice : IODevice
    {
        public ushort[] Data { get; } = new ushort[0x100];

        public override ushort this[byte address]
        {
            get => Data[address];
            set => Data[address] = value;
        }
    }
}
