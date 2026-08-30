namespace SCPU.Assembler
{
    /// <summary>
    /// Represents the result of an S-CPU assembly operation.
    /// Encapsulates the assembled binary, annotated words, and auxiliary metadata (labels, mapping, etc).
    /// </summary>
    public class AssemblyResult
    {
        /// <summary>
        /// The assembled machine code as a binary array, ready to be written to a file or loaded by a simulator.
        /// </summary>
        public byte[] Binary { get; set; } = [];

        /// <summary>
        /// Resolved constant names and their final values (preprocessed <c>#const</c> expressions).
        /// Useful for symbol substitution in annotated outputs or tooling.
        /// </summary>
        public Dictionary<string, int> Constants { get; set; } = [];

        /// <summary>
        /// The list of final (source, word) pairs representing each assembled instruction or data word.
        /// Useful for annotated output, debugging, or mapping back to source lines.
        /// </summary>
        public List<(object Source, ushort Word)> FinalWords { get; set; } = [];

        /// <summary>
        /// The resolved label addresses after assembly, mapping each label name to its absolute address.
        /// </summary>
        public Dictionary<string, uint> Labels { get; set; } = [];
    }
}
