using SCode.Compiler.Ast;
using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Ast.Statements.VariableDeclaration;
using System.ComponentModel;
using static SCode.Compiler.IdentifierInfo;

namespace SCode.Compiler
{
    public static class Extentions
    {
        public static bool IsLocalVariableOrParameter(this IdentifierInfo identifierInfo, out FunctionDeclarationStatement? functionDeclaration)
        {
            functionDeclaration = identifierInfo?.SourceNode?.GetFirstAncestor<FunctionDeclarationStatement>();
            return functionDeclaration != null &&
               (identifierInfo!.Type == IdentifierType.Parameter ||
               (identifierInfo!.Type == IdentifierType.Variable && identifierInfo!.SourceNode is VariableDeclarator variable && !variable.IsGlobalOrStatic));
        }

        public static void PrepareNodes(this IEnumerable<Node> nodes)
        {
            nodes.ForEach(node => node?.Prepare());
        }

        public static void BuildNodes(this IEnumerable<Node> nodes)
        {
            nodes.ForEach(node => node?.Build());
        }

        public static void ForEach(this IEnumerable<Node> nodes, Action<Node> action)
        {
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    action(node);
                }
            }
        }

        public static IEnumerable<TNode> RemoveNull<TNode>(this IEnumerable<TNode> nodes) where TNode : Node
        {
            return nodes.Where(node => node != null);
        }

        public static string ToName(this Enum value)
        {
            var attribute = value.GetAttribute<DescriptionAttribute>();
            return attribute == null ? value.ToString() : attribute.Description;
        }

        public static T? GetAttribute<T>(this Enum value) where T : Attribute
        {
            var type = value.GetType();
            var memberInfo = type.GetMember(value.ToString());
            var attributes = memberInfo[0].GetCustomAttributes(typeof(T), false);
            return attributes.Length > 0 ? (T)attributes[0] : null;
        }

        public static string RemoveCharsContainer(this string str)
        {
            return str[1..^1];
        }
        public static string Escape(this char chr)
        {
            return chr switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ => chr.ToString(),
            };
        }
        public static string Unescape(this string str)
        {
            return System.Text.RegularExpressions.Regex.Unescape(str);
        }

        public static string AppendFileExtension(this string file, string extension)
        {
            return !file.EndsWith(extension) ? file + extension : file;
        }

        public static string AsIndirectAddress(this string address)
        {
            return $"@({address})";
        }

        public static string AsImmediateValue(this string value)
        {
            return $"#({value})";
        }
    }
}
