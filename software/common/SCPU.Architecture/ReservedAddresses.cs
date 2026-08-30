namespace SCPU.Architecture
{
    /// <summary>
    /// Reserved RAM addresses inside <see cref="MemoryMap.Reserved"/>.
    /// These are special-purpose registers and variables used by the runtime,
    /// stack handling, and calling conventions.
    /// </summary>
    public static class ReservedAddresses
    {
        /// <summary>
        /// General-purpose register R0 (reserved RAM offset 0x00).
        /// </summary>
        public static readonly uint R0 = MemoryMap.Reserved.Start + 0x00;
        /// <summary>
        /// General-purpose register R1 (reserved RAM offset 0x01).
        /// </summary>
        public static readonly uint R1 = MemoryMap.Reserved.Start + 0x01;
        /// <summary>
        /// General-purpose register R2 (reserved RAM offset 0x02).
        /// </summary>
        public static readonly uint R2 = MemoryMap.Reserved.Start + 0x02;
        /// <summary>
        /// General-purpose register R3 (reserved RAM offset 0x03).
        /// </summary>
        public static readonly uint R3 = MemoryMap.Reserved.Start + 0x03;
        /// <summary>
        /// General-purpose register R4 (reserved RAM offset 0x04).
        /// </summary>
        public static readonly uint R4 = MemoryMap.Reserved.Start + 0x04;
        /// <summary>
        /// General-purpose register R5 (reserved RAM offset 0x05).
        /// </summary>
        public static readonly uint R5 = MemoryMap.Reserved.Start + 0x05;
        /// <summary>
        /// General-purpose register R6 (reserved RAM offset 0x06).
        /// </summary>
        public static readonly uint R6 = MemoryMap.Reserved.Start + 0x06;
        /// <summary>
        /// General-purpose register R7 (reserved RAM offset 0x07).
        /// </summary>
        public static readonly uint R7 = MemoryMap.Reserved.Start + 0x07;
        /// <summary>
        /// General-purpose register R8 (reserved RAM offset 0x08).
        /// </summary>
        public static readonly uint R8 = MemoryMap.Reserved.Start + 0x08;
        /// <summary>
        /// General-purpose register R9 (reserved RAM offset 0x09).
        /// </summary>
        public static readonly uint R9 = MemoryMap.Reserved.Start + 0x09;

        /// <summary>
        /// Parameter register (offset 0x0A).
        /// </summary>
        public static readonly uint ParameterRegister = MemoryMap.Reserved.Start + 0x0A;
        /// <summary>
        /// Return address register (offset 0x0B).
        /// </summary>
        public static readonly uint ReturnAddressRegister = MemoryMap.Reserved.Start + 0x0B;
        /// <summary>
        /// Temporary "peek" register (offset 0x0C).
        /// </summary>
        public static readonly uint PeekRegister = MemoryMap.Reserved.Start + 0x0C;
        /// <summary>
        /// Frame pointer (offset 0x0E).
        /// </summary>
        public static readonly uint FramePointer = MemoryMap.Reserved.Start + 0x0E;
        /// <summary>
        /// Stack pointer (offset 0x0F).
        /// </summary>
        public static readonly uint StackPointer = MemoryMap.Reserved.Start + 0x0F;
        /// <summary>
        /// Base address for temporary variables (offset 0x10).
        /// </summary>
        public static readonly uint TemporaryVariables = MemoryMap.Reserved.Start + 0x10;
    }
}
