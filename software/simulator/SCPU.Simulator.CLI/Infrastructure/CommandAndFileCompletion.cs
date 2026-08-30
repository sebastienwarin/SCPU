namespace SCPU.Simulator.CLI.Infrastructure
{
    public sealed class CommandAndFileCompletion : IAutoCompleteHandler
    {
        // Provide a list of top-level commands for completion
        private static readonly string[] _words =
        [
            "help", "exit",
            "load", "reload", "run", "debug", "reset", "step", "until",
            "break add", "break delete", "break list", "break clear",
            "regs", "stack", "mem", "disasm", "source", "context", "ctx", "symbols",
            "watch add", "watch delete", "watch list", "watch clear",
            "assert reg", "assert mem", "assert pc", "assert led", "assert tty",
            "tty input", "tty status", "tty clear"
        ];

        public char[] Separators { get; set; } = [' '];

        public string[] GetSuggestions(string text, int index)
        {
            var tokens = Tokenize(text);
            var partial = CurrentPartial(text);

            if (LooksLikePath(partial) || IsFileyCommand(tokens))
                return CompletePath(partial);

            return [.. _words.Where(w => w.StartsWith(text, StringComparison.OrdinalIgnoreCase))];
        }

        private static List<string> Tokenize(string text)
        {
            var res = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQ = false;
            foreach (var c in text)
            {
                if (c == '"') { inQ = !inQ; continue; }
                if (char.IsWhiteSpace(c) && !inQ)
                { if (cur.Length > 0) { res.Add(cur.ToString()); cur.Clear(); } }
                else cur.Append(c);
            }
            if (cur.Length > 0) res.Add(cur.ToString());
            return res;
        }

        private static string CurrentPartial(string text)
        {
            int i = text.LastIndexOf(' ');
            return i < 0 ? text : text[(i + 1)..];
        }

        private static bool LooksLikePath(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // starts with ./ ../ .\ ..\  /  \  ~  or C:\  etc.
            if (s.StartsWith("./") || s.StartsWith(".\\") || s.StartsWith("../") || s.StartsWith("..\\"))
                return true;
            if (s.StartsWith("/") || s.StartsWith("\\") || s.StartsWith("~"))
                return true;
            if (s.Length >= 2 && char.IsLetter(s[0]) && s[1] == ':') // Windows drive
                return true;
            // contains a separator anywhere
            return s.Contains('/') || s.Contains('\\');
        }

        private static bool IsFileyCommand(IReadOnlyList<string> tokens)
        {
            if (tokens.Count == 0) return false;
            if (tokens[0].Equals("load", StringComparison.OrdinalIgnoreCase) && tokens.Count >= 2)
                return true;
            return false;
        }

        private static string ExpandHome(string p)
        {
            if (!p.StartsWith("~")) return p;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, p[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        private static string[] CompletePath(string partial)
        {
            var original = partial;
            var expanded = ExpandHome(partial);

            var sep = Path.DirectorySeparatorChar;
            expanded = expanded.Replace('/', sep).Replace('\\', sep);

            string baseDir, stub;
            try
            {
                if (string.IsNullOrEmpty(expanded))
                {
                    baseDir = Directory.GetCurrentDirectory();
                    stub = "";
                }
                else
                {
                    var candidate = expanded;

                    if (Directory.Exists(candidate))
                    {
                        baseDir = Path.GetFullPath(candidate);
                        stub = "";
                    }
                    else
                    {
                        baseDir = Path.GetDirectoryName(candidate) ?? Directory.GetCurrentDirectory();
                        if (string.IsNullOrEmpty(baseDir))
                            baseDir = Directory.GetCurrentDirectory();

                        baseDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), baseDir));
                        stub = Path.GetFileName(candidate) ?? "";
                    }
                }

                var entries = Directory.EnumerateFileSystemEntries(baseDir)
                    .Select(full =>
                    {
                        var name = Path.GetFileName(full);
                        string relBase;

                        if (string.IsNullOrEmpty(original) || original.StartsWith("~"))
                        {
                            relBase = Path.GetDirectoryName(expanded) ?? "";
                        }
                        else
                        {
                            relBase = Path.GetDirectoryName(original.Replace('/', sep).Replace('\\', sep)) ?? "";
                        }

                        var suggestion = string.IsNullOrEmpty(relBase) ? name : Path.Combine(relBase, name);

                        if (Directory.Exists(full) && !suggestion.EndsWith(sep))
                            suggestion += sep;

                        if (suggestion.Contains(' '))
                            suggestion = $"\"{suggestion}\"";

                        return (name, suggestion);
                    })
                    .Where(x => x.name.StartsWith(stub, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.suggestion)
                    .OrderBy(s => s.EndsWith(sep) ? 0 : 1)
                    .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return entries;
            }
            catch
            {
                return [];
            }
        }
    }
}
