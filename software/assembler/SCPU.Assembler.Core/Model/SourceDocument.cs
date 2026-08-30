namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents an abstract source document, which can be either:
    /// <list type="bullet">
    /// <item><description>A <see cref="FileSourceDocument"/> backed by a file on disk.</description></item>
    /// <item><description>An <see cref="InlineSourceDocument"/> backed by in-memory text.</description></item>
    /// </list>
    /// </summary>
    public abstract class SourceDocument
    {
        /// <summary>
        /// A stable identifier for diagnostics, logs, and error messages.
        /// <list type="bullet">
        /// <item><description>For <see cref="FileSourceDocument"/> sources: the absolute file path.</description></item>
        /// <item><description>For <see cref="InlineSourceDocument"/> sources: a virtual or user-specified name.</description></item>
        /// </list>
        /// </summary>
        public abstract string Identifier { get; }

        /// <summary>
        /// The base directory used to resolve relative <c>#include</c> directives.
        /// <list type="bullet">
        /// <item><description>For <see cref="FileSourceDocument"/> sources: the containing directory.</description></item>
        /// <item><description>For <see cref="InlineSourceDocument"/> sources: null unless explicitly provided.</description></item>
        /// </list>
        /// </summary>
        public virtual string? BaseDirectory => null;

        /// <summary>
        /// Reads the full source text as UTF-8.
        /// <list type="bullet">
        /// <item><description>For <see cref="FileSourceDocument"/> sources: loads from disk.</description></item>
        /// <item><description>For <see cref="InlineSourceDocument"/> sources: returns the in-memory string.</description></item>
        /// </list>
        /// </summary>
        public abstract Task<string> ReadAllTextAsync(CancellationToken ct = default);

        /// <summary>
        /// Creates a file-backed <see cref="SourceDocument"/>.
        /// </summary>
        /// <param name="path">The file to wrap.</param>
        /// <returns>A <see cref="FileSourceDocument"/> source if the file exists.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
        public static SourceDocument FromFile(FileInfo path)
        {
            ArgumentNullException.ThrowIfNull(path);
            if (!path.Exists)
                throw new FileNotFoundException($"Source file not found: {path.FullName}", path.FullName);

            return new FileSourceDocument(path.FullName);
        }

        /// <summary>
        /// Creates an inline <see cref="SourceDocument"/>.
        /// </summary>
        /// <param name="text">The source code text (must not be null or empty).</param>
        /// <param name="virtualName">
        /// Optional logical name for diagnostics and logs (defaults to "Inline").
        /// Example: "REPL", "snippet.asm".
        /// </param>
        /// <param name="baseDirectory">
        /// Optional base directory for resolving relative includes.
        /// If null, includes must be absolute or resolved via external search paths.
        /// </param>
        /// <returns>An <see cref="InlineSourceDocument"/> source containing the provided text.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty.</exception>
        public static SourceDocument FromInline(string text, string? virtualName = null, string? baseDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Inline source text cannot be empty.", nameof(text));

            return new InlineSourceDocument(text, virtualName ?? "Inline", baseDirectory);
        }
    }

    /// <summary>
    /// File-backed assembly source (loads text directly from disk).
    /// </summary>
    public sealed class FileSourceDocument : SourceDocument
    {
        private readonly string _fullPath;

        /// <summary>
        /// Creates a file-backed source.
        /// </summary>
        /// <param name="fullPath">Absolute file path. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="fullPath"/> is null or empty.</exception>
        public FileSourceDocument(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("Path cannot be null/empty.", nameof(fullPath));

            _fullPath = Path.GetFullPath(fullPath);
        }

        /// <inheritdoc/>
        public override string Identifier => _fullPath;

        /// <inheritdoc/>
        public override string? BaseDirectory => Path.GetDirectoryName(_fullPath);

        /// <inheritdoc/>
        public override Task<string> ReadAllTextAsync(CancellationToken ct = default) => File.ReadAllTextAsync(_fullPath, ct);
    }

    /// <summary>
    /// Inline assembly source stored in memory.
    /// </summary>
    public sealed class InlineSourceDocument : SourceDocument
    {
        private readonly string _text;
        private readonly string _name;
        private readonly string? _baseDirectory;

        /// <summary>
        /// Creates an inline source from a string.
        /// </summary>
        /// <param name="text">Assembly source text (must not be null or empty).</param>
        /// <param name="virtualName">Optional logical name (defaults to "Inline").</param>
        /// <param name="baseDirectory">
        /// Optional base directory for resolving relative includes.
        /// If null, includes must be absolute or resolved via external search paths.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="text"/> is empty.</exception>
        public InlineSourceDocument(string text, string virtualName, string? baseDirectory = null)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            if (_text.Length == 0)
                throw new ArgumentException("Inline source text cannot be empty.", nameof(text));

            _name = string.IsNullOrWhiteSpace(virtualName) ? "Inline" : virtualName.Trim();
            _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? null : baseDirectory;
        }

        /// <inheritdoc/>
        public override string Identifier => _name;

        /// <inheritdoc/>
        public override string? BaseDirectory => _baseDirectory;

        /// <inheritdoc/>
        public override Task<string> ReadAllTextAsync(CancellationToken ct = default) => Task.FromResult(_text);
    }
}
