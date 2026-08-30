namespace SCPU.Simulator.Core
{
    /// <summary>
    /// S-CPU pipeline step.
    /// </summary>
    public enum Step
    {
        /// <summary>
        /// First step (fetch).
        /// </summary>
        S0 = 0,

        /// <summary>
        /// Second step (execute).
        /// </summary>
        S1 = 1
    }

    /// <summary>
    /// Identifiers for memory-mapped I/O devices.
    /// </summary>
    public enum DeviceId
    {
        /// <summary>Device #0</summary>
        Device0,
        /// <summary>Device #1</summary>
        Device1,
        /// <summary>Device #2</summary>
        Device2,
        /// <summary>Device #3</summary>
        Device3,
        /// <summary>Device #4</summary>
        Device4,
        /// <summary>Device #5</summary>
        Device5,
        /// <summary>Device #6</summary>
        Device6,
        /// <summary>S-CPU reserved device</summary>
        Device7
    }
}
