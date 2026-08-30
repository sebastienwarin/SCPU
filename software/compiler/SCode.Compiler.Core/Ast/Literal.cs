namespace SCode.Compiler.Ast
{
    public abstract class Literal<TValue> : Literal
    {
        public new TValue Value
        { 
            get { return (TValue)base.Value; }
            set { base.Value = (object)value; }
        }
    }

    public abstract class Literal : Node
    {
        public object Value { get; set; }
        public override string ToString() => Value.ToString();
    }
}
