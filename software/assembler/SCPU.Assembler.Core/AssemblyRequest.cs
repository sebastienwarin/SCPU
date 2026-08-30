using SCPU.Assembler.Model;

namespace SCPU.Assembler
{
    /// <summary>
    /// Encapsulates all options for a single S-CPU assembly run.
    /// Provide exactly one source (file or inline).
    /// </summary>
    public sealed class AssemblyRequest
    {
        /// <summary>
        /// The input assembly source.
        /// Provide exactly one of <see cref="SourceDocument.FileSourceDocument"/> or <see cref="SourceDocument.InlineSourceDocument"/>.
        /// </summary>
        public required SourceDocument Source { get; init; }

        /// <summary>
        /// Compile-time symbol definitions injected before parsing,
        /// functionally equivalent to top-level <c>#const</c> declarations.
        /// Keys are symbol names; values are expressions (e.g., "1", "0x1234", "9600").
        /// </summary>
        /// <remarks>
        /// Conflict behavior with in-source <c>#const</c> can be controlled via <see cref="DefineConflictPolicy"/>.
        /// </remarks>
        public IReadOnlyDictionary<string, string>? Defines { get; init; }

        /// <summary>
        /// Determines how in-source <c>#const</c> declarations are handled when a symbol
        /// has already been defined earlier (e.g., via CLI/API <see cref="Defines"/> injected before parsing).
        /// Default is <see cref="DefineConflictPolicy.KeepExisting"/> so that pre-seeded defines win.
        /// </summary>
        public DefineConflictPolicy ConflictPolicy { get; init; } = DefineConflictPolicy.KeepExisting;
    }

    /// <summary>
    /// Policy applied when the parser encounters a <c>#const</c> whose symbol is already defined
    /// (typically because CLI/API defines were pre-seeded before parsing).
    /// </summary>
    public enum DefineConflictPolicy
    {
        /// <summary>
        /// Keep the existing value and ignore the new <c>#const</c> declaration.
        /// This makes pre-seeded CLI/API defines win over in-source redefinitions.
        /// </summary>
        KeepExisting,

        /// <summary>
        /// Overwrite the existing value with the new one from the in-source <c>#const</c>.
        /// </summary>
        Overwrite,

        /// <summary>
        /// Treat any attempt to redefine an existing symbol as an error.
        /// </summary>
        ErrorOnConflict
    }
}
