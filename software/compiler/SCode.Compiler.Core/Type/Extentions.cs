using SCode.Compiler.Ast;

namespace SCode.Compiler.Type
{
    public static class Extentions
    {
        public static bool CanAssignTo(this TypeDescriptor toType, TypeInfo fromType, bool isExplicit = false)
        {
            return TypeHelper.CanConvert(fromType, toType, isExplicit);
        }

        public static bool CanAssignTo(this TypeInfo toType, TypeInfo fromType, bool isExplicit = false)
        {
            return TypeHelper.CanConvert(fromType, toType, isExplicit);
        }
    }
}
