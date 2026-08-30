using SCPU.Assembler.Exporters;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace SCPU.Assembler.CLI
{
    /// <summary>
    /// Provides a central definition of the command-line interface (CLI) 
    /// for the S-CPU assembler. 
    /// Defines all arguments, options, their parsers, and bindings to strongly-typed options.
    /// </summary>
    public static class CommandLineDefinition
    {
        /// <summary>
        /// Required argument representing the input assembly file to process.
        /// </summary>
        public static readonly Argument<FileInfo> FileArg = new("file")
        {
            Description = "File to assemble"
        };

        /// <summary>
        /// Optional output file. If omitted, results may only be printed to console.
        /// </summary>
        public static readonly Option<FileInfo> OutputOpt = new("--output", ["-o"])
        {
            Description = "Write output to the specified file"
        };

        /// <summary>
        /// Selects the output format (Annotated, Binary, Verilog, IntelHex, etc.).
        /// Defaults to Annotated if not specified.
        /// </summary>
        public static readonly Option<OutputFormat> FormatOpt = new("--format", ["-f"])
        {
            Description = "The output format",
            DefaultValueFactory = _ => OutputFormat.Annotated
        };

        /// <summary>
        /// Defines constants for conditional assembly.
        /// </summary>
        public static readonly Option<Dictionary<string, string>> DefinesOpt = new("--define", ["-d"])
        {
            Description = "Define constants. Usage: -d KEY or -d KEY=VALUE. If value is omitted, it defaults to 'true'.",
            CustomParser = ParseDefines,
            Arity = ArgumentArity.ZeroOrMore
        };

        /// <summary>
        /// If set, prints the final assembled output directly to the console.
        /// </summary>
        public static readonly Option<bool> PrintOpt = new("--print", ["-p"])
        {
            Description = "Print the output to the screen."
        };

        /// <summary>
        /// If set, suppress console logging output.
        /// </summary>
        public static readonly Option<bool> QuietOpt = new("--quiet", ["-q"])
        {
            Description = "Suppress console logging output."
        };

        /// <summary>
        /// Option that specifies a target URL for HTTP POST upload.
        /// </summary>
        public static readonly Option<Uri?> PostUrlOpt = new("--post", ["-u"])
        {
            Description = "POST the assembled payload to the specified URL (e.g. http://slink.local/upload).",
            CustomParser = static (arg) => arg.Tokens.Any() ? new Uri(arg.Tokens[0].Value) : null
        };

        /// <summary>
        /// Builds and returns the root CLI command, including all supported options and arguments.
        /// </summary>
        public static RootCommand BuildRootCommand()
        {
            FileArg.AcceptExistingOnly();

            return new RootCommand("S-CPU Assembler")
            {
                FileArg, OutputOpt, FormatOpt, DefinesOpt, PrintOpt, QuietOpt, PostUrlOpt
            };
        }

        /// <summary>
        /// Maps the raw parse result into a strongly-typed <see cref="CommandLineOptions"/> record.
        /// </summary>
        public static CommandLineOptions Bind(ParseResult pr)
        {
            return new CommandLineOptions(
                File: pr.GetRequiredValue(FileArg),
                Output: pr.GetValue(OutputOpt),
                Format: pr.GetValue(FormatOpt),
                Defines: pr.GetValue(DefinesOpt),
                Print: pr.GetValue(PrintOpt),
                Quiet: pr.GetValue(QuietOpt),
                PostUrl: pr.GetValue(PostUrlOpt)
            );
        }

        /// <summary>
        /// Custom parser for -d/--define arguments.
        /// Accepts tokens in the form "KEY" or "KEY=VALUE".
        /// If VALUE is omitted, defaults to "true".
        /// </summary>
        private static Dictionary<string, string> ParseDefines(ArgumentResult result)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in result.Tokens)
            {
                var raw = token.Value?.Trim();
                if (string.IsNullOrEmpty(raw)) continue;

                var parts = raw.Split('=', 2);
                var key = parts[0].Trim();

                if (string.IsNullOrEmpty(key))
                {
                    // Invalid key, ignore gracefully
                    return dict;
                }

                var value = parts.Length == 2 ? parts[1] : bool.TrueString;
                dict[key] = value;
            }

            return dict;
        }
    }

    /// <summary>
    /// Represents the strongly-typed, parsed command-line options for the assembler.
    /// </summary>
    /// <param name="File">The input file to assemble (required).</param>
    /// <param name="Output">Optional output file destination.</param>
    /// <param name="Format">The chosen output format.</param>
    /// <param name="Defines">Dictionary of constants defined via -d/--define.</param>
    /// <param name="Print">Indicates whether to print the output to console.</param>
    /// <param name="Quiet">Indicates whether to suppress console logging output.</param>
    /// <param name="PostUrl">Optional HTTP endpoint URL. If specified, the assembled payload will be POSTed.</param>
    public sealed record CommandLineOptions(
        FileInfo File,
        FileInfo? Output,
        OutputFormat Format,
        Dictionary<string, string>? Defines,
        bool Print,
        bool Quiet,
        Uri? PostUrl
    );
}
