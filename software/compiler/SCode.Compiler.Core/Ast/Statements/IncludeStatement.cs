namespace SCode.Compiler.Ast.Statements
{
    public class IncludeStatement : Statement
    {
        private const string LibFolderName = "lib";

        public static readonly string[] SCodeFileExtensions = [".scode", ".sc"];
        public static readonly string[] AsmFileExtensions = [".asm", ".s", ".inc"];

        /// <summary>
        /// Filename as written in the include directive (may be relative or without extension).
        /// </summary>
        public string Filename { get; set; } = string.Empty;

        /// <summary>
        /// True if the included file is an Assembly source, false if it's a SCode file.
        /// </summary>
        public bool IsAssemblyFile { get; private set; }

        /// <summary>
        /// Resolve the included file path.
        /// </summary>
        public FileInfo FileInfo
        {
            get
            {
                var baseDir = Source?.SourceFile?.Directory?.FullName ?? Directory.GetCurrentDirectory();
                var programRoot = this.Context?.Program?.Source?.SourceFile?.Directory?.FullName ?? baseDir;
                var extProvided = !string.IsNullOrWhiteSpace(Path.GetExtension(Filename));

                // With explicit extension: resolve as-is (no lib search, no probing)
                if (extProvided)
                {
                    var directWithExt = Path.IsPathRooted(Filename)
                        ? Filename
                        : Path.GetFullPath(Path.Combine(baseDir, Filename));

                    if (File.Exists(directWithExt))
                        DetermineTypeFromExtension(directWithExt);

                    return new FileInfo(directWithExt);
                }
                else // No extension: probe only S-Code files in baseDir, then baseDir/lib, programRoot and finally programRoot/lib
                {
                    // 1) from source directory
                    var file = ResolveSCodeFile(baseDir, Filename);
                    if (file is not null)
                    {
                        return file;
                    }

                    // 2) from program root directory
                    file = ResolveSCodeFile(programRoot, Filename);
                    if (file is not null)
                    {
                        return file;
                    }

                    // Not found -> return the expected path (for diagnostics)
                    var expected = Path.IsPathRooted(Filename) ? Filename : Path.GetFullPath(Path.Combine(baseDir, Filename));
                    return new FileInfo(expected);
                }
            }
        }

        private static FileInfo? ResolveSCodeFile(string baseDir, string filename)
        {
            // 1) baseDir/<path>.scode|.sc
            var found = CandidatePaths(baseDir, filename, SCodeFileExtensions).FirstOrDefault(File.Exists);
            if (found is not null)
            {
                return new FileInfo(found);
            }

            // 2) baseDir/lib/<path>.scode|.sc  (no recursive search)
            var libRoot = Path.Combine(baseDir, LibFolderName);
            if (Directory.Exists(libRoot))
            {
                found = CandidatePaths(libRoot, filename, SCodeFileExtensions).FirstOrDefault(File.Exists);
                if (found is not null)
                {
                    return new FileInfo(found);
                }
            }

            // Not found
            return null;
        }

        private static IEnumerable<string> CandidatePaths(string root, string baseNameNoExt, IEnumerable<string> exts)
        {
            foreach (var ext in exts)
            {
                yield return Path.GetFullPath(Path.Combine(root, baseNameNoExt + ext));
            }
        }

        private void DetermineTypeFromExtension(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            IsAssemblyFile = AsmFileExtensions.Contains(ext);
        }

        public override string ToString() => $"Include {FileInfo.FullName} (SCode={!IsAssemblyFile})";
    }
}
