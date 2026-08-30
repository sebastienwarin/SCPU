using SCode.Compiler.Ast;

namespace SCode.Compiler.Exceptions
{
    public class NodeCompilerException : CompilerException
    {
        public Node? Node { get; set; }

        public override string Message
        {
            get
            {
                if (Node != null)
                {
                    return $"{base.Message}\n\tat {Node.Source}, node: {Node.GetType().Name}";
                }
                
                return base.Message ?? "Unknown compiler error";
            }
        }

        public NodeCompilerException(string? message, Node? node = null, Exception? innerException = null)
            : base(message, innerException)
        {
            this.Node = node;
        }
    }

    public class CompilerException(string? message, Exception? innerException) : Exception(message, innerException)
    {
    }
}
