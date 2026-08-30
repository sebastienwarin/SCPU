namespace SCPU.Architecture
{
    /// <summary>
    /// S-CPU virtual memory map for ROM, RAM, MMIO, and reserved regions.
    /// </summary>
    public static class MemoryMap
    {
        /// <summary>
        /// ROM: 0x0000..0xFFFF (64K words).
        /// Contains program instructions and constant data.
        /// </summary>
        public static readonly MemorySpan Rom = new MemorySpan(0x0000, 1u << 16);

        /// <summary>
        /// RAM: 0x12000..0x127FF (2K words).
        /// Main volatile memory used for stack, user data and reserved area.
        /// </summary>
        public static readonly MemorySpan Ram = new MemorySpan(0x12000, 1u << 11);

        /// <summary>
        /// Stack area within RAM: 0x12000..0x120FF (256 words).
        /// Used for function calls, return addresses, and local variables.
        /// </summary>
        public static readonly MemorySpan Stack = Ram.Slice(0x000, 0x100);

        /// <summary>
        /// User page within RAM: 0x12100..0x126FF (1536 words).
        /// General-purpose storage for program variables.
        /// </summary>
        public static readonly MemorySpan UserPage = Ram.Slice(0x100, 0x600);

        /// <summary>
        /// Reserved area within RAM: 0x12700..0x127FF (256 words).
        /// Holds system registers such as registers, frame pointer, stack pointer, etc.
        /// </summary>
        public static readonly MemorySpan Reserved = Ram.Slice(0x700, 0x100);

        /// <summary>
        /// MMIO: 0x12800..0x12FFF (2K words).
        /// Memory-mapped input/output devices. Addressed like memory but routed to peripherals.
        /// </summary>
        public static readonly MemorySpan Mmio = new MemorySpan(0x12800, 1u << 11);

        /// <summary>
        /// Virtual address bias: RAM/MMIO virtual bases are offset by +0x10000
        /// compared to the old flat 16-bit view (0x2000/0x2800).
        /// </summary>
        public const uint VirtualAddressBias = 0x10000;

        /// <summary>
        /// Highest directly encodable ROM address as an operand (13 bits).
        /// </summary>
        public const uint MaxDirectRomAddress = 0x1FFF;

        /// <summary>
        /// Maximum value that can be encoded as an immediate operand (11 bits).
        /// </summary>
        public const uint ImmediateMaxValue = 0x7FF;
    }
}
