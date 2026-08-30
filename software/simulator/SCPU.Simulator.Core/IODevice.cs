namespace SCPU.Simulator.Core
{
    /// <summary>
    /// Base class for memory-mapped I/O devices in the S-CPU.
    /// </summary>
    public abstract class IODevice
    {
        /// <summary>
        /// Gets or sets the value of a device register at the given address.
        /// </summary>
        /// <param name="address">The register offset (0-255) within the device.</param>
        /// <returns>The register value.</returns>
        public abstract ushort this[byte address] { get; set; }

        /// <summary>
        /// Reads a register for diagnostics without consuming input or changing device state.
        /// Stateless devices can rely on the default implementation.
        /// </summary>
        public virtual ushort Peek(byte address) => this[address];

        /// <summary>
        /// Resets the device to its default state.
        /// </summary>
        public virtual void Reset() { }
    }
}
