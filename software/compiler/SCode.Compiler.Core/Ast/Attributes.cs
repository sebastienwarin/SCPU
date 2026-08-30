namespace SCode.Compiler.Ast
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NestedScopeAttribute : Attribute
    {
        public bool OnlyChild { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ChildNodeAttribute : Attribute
    {
    }
}
