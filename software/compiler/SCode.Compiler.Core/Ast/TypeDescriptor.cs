using SCode.Compiler.Type;

namespace SCode.Compiler.Ast
{
    public class TypeDescriptor : Node
    {
        public required string Name { get; set; }
        public int PointerLevel { get; set; }
        public bool IsBaseType { get; set; }

        public bool IsPointer => PointerLevel > 0;
        public TypeInfo TypeInfo => (TypeInfo)this;

        public override string ToString()
        {
            return Name + new string('*', PointerLevel);
        }
    }
}
