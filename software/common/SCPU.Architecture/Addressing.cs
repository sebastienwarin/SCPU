namespace SCPU.Architecture
{
    /// <summary>
    /// Address conversion helpers between virtual addresses (uint) and physical view (region + 16-bit offset).
    /// </summary>
    public static class Addressing
    {
        /// <summary>
        /// Translates an assembler virtual address into either an encoded ISA operand
        /// or a physical region offset, depending on <paramref name="view"/>.
        /// </summary>
        /// <param name="vaddr">Virtual address as seen by the assembler (e.g., 0x12000 for RAM base).</param>
        /// <param name="view">
        /// Desired representation:
        /// <see cref="AddressView.EncodedOperand"/> to obtain the ISA operand value
        /// (with addressing-mode), or <see cref="AddressView.PhysicalOffset"/>
        /// to obtain the region-relative offset.
        /// </param>
        /// <param name="value">
        /// The translated 16-bit value. For <c>EncodedOperand</c>, this is the operand
        /// that the instruction would carry. For <c>PhysicalOffset</c>, this is the
        /// 0-based offset within the resolved <paramref name="region"/>.
        /// </param>
        /// <param name="region">Resolved memory region (ROM, RAM, MMIO) for <paramref name="vaddr"/>.</param>
        /// <returns><c>true</c> if the address is within a known region; otherwise <c>false</c>.</returns>
        public static bool TryTranslateVirtualAddress(uint vaddr, AddressView view, out ushort value, out MemoryRegion region)
        {
            // RAM
            if (MemoryMap.Ram.Contains(vaddr))
            {
                region = MemoryRegion.Ram;
                if (view == AddressView.PhysicalOffset)
                {
                    value = (ushort)(vaddr - MemoryMap.Ram.Start);                 // 0..0x7FF
                }
                else
                {
                    value = (ushort)(vaddr - MemoryMap.VirtualAddressBias);        // 0x2000..0x27FF
                }
                return true;
            }

            // MMIO
            if (MemoryMap.Mmio.Contains(vaddr))
            {
                region = MemoryRegion.Mmio;
                if (view == AddressView.PhysicalOffset)
                {
                    value = (ushort)(vaddr - MemoryMap.Mmio.Start);                // 0..0x7FF
                }
                else
                {
                    value = (ushort)(vaddr - MemoryMap.VirtualAddressBias);        // 0x2800..0x2FFF
                }
                return true;
            }

            // ROM
            if (MemoryMap.Rom.Contains(vaddr))
            {
                region = MemoryRegion.Rom;
                // For ROM, encoded operand and physical offset are identical
                // (ROM addressing mode is carried separately in the instruction).
                value = (ushort)(vaddr - MemoryMap.Rom.Start);                     // 0..0xFFFF
                return true;
            }

            region = default;
            value = 0;
            return false;
        }

        /// <summary>
        /// Defines the physical memory regions accessible by the S-CPU core.
        /// </summary>
        public enum MemoryRegion : byte
        {
            /// <summary>
            /// Read-only program memory (ROM).
            /// Address range: 0x0000-0xFFFF (64 KB).
            /// </summary>
            Rom,

            /// <summary>
            /// Random-access memory (RAM).
            /// Address range: 0x0000-0x07FF (2 KB).
            /// </summary>
            Ram,

            /// <summary>
            /// Memory-mapped I/O (MMIO).
            /// Address range: 0x0000-0x07FF (2 KB).
            /// </summary>
            Mmio
        }

        /// <summary>
        /// Selects how a virtual address should be translated.
        /// </summary>
        public enum AddressView
        {
            /// <summary>
            /// Encoded operand as used by the ISA.
            /// </summary>
            EncodedOperand,

            /// <summary>
            /// Physical offset within the target region (0-based),
            /// without the addressing-mode.
            /// </summary>
            PhysicalOffset
        }
    }
}
