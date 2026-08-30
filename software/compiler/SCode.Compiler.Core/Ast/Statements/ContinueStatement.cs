namespace SCode.Compiler.Ast.Statements
{
    public class ContinueStatement : Statement
    {
        private IContinuableFlow? continuableNode;

        protected override void OnPrepare()
        {
            continuableNode = GetFirstAncestor<IContinuableFlow>() ?? throw this.RaiseError("Continue is not allowed here");
        }

        protected override void OnBuild()
        {
            Context.InstructionBuilder.EmitJump(continuableNode!.ContinueLabel);
        }

        public override string ToString() => $"Continue";

        public interface IContinuableFlow
        {
            string ContinueLabel { get; }
        }
    }
}
