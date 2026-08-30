namespace SCode.Compiler.Ast.Statements
{
    public class BreakStatement : Statement
    {
        private IBreakableFlow? breakableNode;

        protected override void OnPrepare()
        {
            breakableNode = GetFirstAncestor<IBreakableFlow>() ?? throw this.RaiseError("Break is not allowed here");
        }

        protected override void OnBuild()
        {
            Context.InstructionBuilder.EmitJump(breakableNode!.BreakLabel);
        }

        public override string ToString() => $"Break";

        public interface IBreakableFlow
        {
            string BreakLabel { get; }
        }
    }
}
