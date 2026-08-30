namespace SCPU.Architecture
{
    /// <summary>
    /// Half-open virtual memory interval [Start, EndExclusive).
    /// Keeps math simple and prevents off-by-one mistakes.
    /// </summary>
    public readonly struct MemorySpan
    {
        /// <summary>
        /// Virtual start address (inclusive).
        /// </summary>
        public uint Start { get; }

        /// <summary>
        /// Length in addressable words.
        /// </summary>
        public uint Length { get; }

        /// <summary>
        /// Virtual end address (exclusive) = Start + Length.
        /// </summary>
        public uint EndExclusive => checked(Start + Length);

        /// <summary>
        /// Virtual end address (inclusive) = EndExclusive - 1.
        /// </summary>
        public uint EndInclusive => EndExclusive - 1;

        /// <summary>
        /// Create a half-open span [start, start+length).
        /// </summary>
        public MemorySpan(uint start, uint length)
        {
            Start = start;
            Length = length;
            _ = EndExclusive; // force checked overflow at construction
        }

        /// <summary>
        /// Returns true if the virtual address is within the span.
        /// </summary>
        public bool Contains(uint vaddr) => vaddr >= Start && vaddr < EndExclusive;

        /// <summary>
        /// Create a sub-span at offset with the given length.
        /// </summary>
        public MemorySpan Slice(uint offset, uint length)
            => new MemorySpan(checked(Start + offset), length);

        public override string ToString() => $"[0x{Start:X}, 0x{EndInclusive:X}]";
    }
}
