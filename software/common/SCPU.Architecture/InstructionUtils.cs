using System;
using System.Collections.Generic;

namespace SCPU.Architecture
{
    /// <summary>
    /// Provides low-level utilities for decoding S-CPU instructions and analyzing ROM contents.
    /// This helper is shared across compiler, assembler, and simulator layers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These helpers do not depend on the simulator runtime state and are safe to use
    /// in any context where instruction words (<c>ushort</c>) or ROM images are available.
    /// </para>
    /// </remarks>
    public static class InstructionUtils
    {
        /// <summary>
        /// Decodes the addressing mode from a 16-bit instruction word.
        /// Bit 13 == 0 for ROM addressing; otherwise bits [13:11] encode the 3-bit addressing mode.
        /// </summary>
        /// <param name="instruction">Raw 16-bit instruction word.</param>
        /// <returns>The decoded <see cref="AddressingMode"/> value.</returns>
        public static AddressingMode GetAddressingMode(ushort instruction)
        {
            return (((instruction >> 13) & 1) == 0)
                ? AddressingMode.ROM
                : (AddressingMode)((instruction >> 11) & 0x7);
        }

        /// <summary>
        /// Detects both addresses of consecutive self-loop JCCs emitted by the HALT macro.
        /// Works for Immediate and ROM addressing modes.
        /// </summary>
        public static HashSet<ushort> DetectHaltAddresses(ReadOnlySpan<ushort> rom)
        {
            var halts = new HashSet<ushort>();
            for (int i = 1; i < rom.Length; i++)
            {
                if (IsJccSelf(rom[i - 1], rom, (ushort)(i - 1)) &&
                    IsJccSelf(rom[i], rom, (ushort)i))
                {
                    // Depending on the carry state on entry, execution can stop on either
                    // instruction. Registering only the second leaves the first self-loop
                    // running forever when carry is clear.
                    halts.Add((ushort)(i - 1));
                    halts.Add((ushort)i);
                }
            }
            return halts;
        }

        /// <summary>
        /// True if IR is a JCC and its resolved target equals <paramref name="addr"/>.
        /// Immediate: IR[10:0]. ROM: ROM[ IR[11:0] ].
        /// </summary>
        public static bool IsJccSelf(ushort ir, ReadOnlySpan<ushort> rom, ushort addr)
        {
            if ((Instruction)(ir >> 14) != Instruction.JCC)
                return false;

            return TryResolveJccTarget(ir, rom, out ushort target) && target == addr;
        }

        /// <summary>
        /// Resolves the effective jump target of a <see cref="Instruction.JCC"/>.
        /// </summary>
        /// <param name="ir">Raw 16-bit instruction word.</param>
        /// <param name="rom">ROM memory view (for ROM addressing resolution).</param>
        /// <param name="target">Receives the decoded target address.</param>
        /// <returns><see langword="true"/> if the target could be statically resolved; otherwise <see langword="false"/>.</returns>
        public static bool TryResolveJccTarget(ushort ir, ReadOnlySpan<ushort> rom, out ushort target)
        {
            var mode = GetAddressingMode(ir);
            switch (mode)
            {
                case AddressingMode.Immediate:
                    target = (ushort)(ir & 0x07FF);
                    return true;

                case AddressingMode.ROM:
                    ushort ptr = (ushort)(ir & 0x0FFF);
                    target = (ptr < rom.Length) ? rom[ptr] : (ushort)0;
                    return true;

                default:
                    target = 0;
                    return false; // Indirect/MMIO not statically resolvable
            }
        }
    }
}
