using SCode.Compiler.Ast;
using SCode.Compiler.Ast.Statements;

namespace SCode.Compiler.Type
{
    public struct TypeInfo
    {
        public static readonly TypeInfo Empty = new() { Name = "Void" };
        public static readonly TypeInfo Int = FromTypeCode(SCodeType.Int);
        public static readonly TypeInfo Char = FromTypeCode(SCodeType.Char);
        public static readonly TypeInfo Decimal = FromTypeCode(SCodeType.Decimal);
        public static readonly TypeInfo Long = FromTypeCode(SCodeType.Long);
        public static readonly TypeInfo Bool = FromTypeCode(SCodeType.Bool);
        public static readonly TypeInfo String = FromTypeCode(SCodeType.String);

        public string Name { get; private set; }
        public int PointerLevel { get; set; }
        public bool IsBaseType { get; private set; }
        public StructDeclarationStatement? Declaration { get; set; }

        public readonly SCodeType TypeCode => GetTypeCodeFromName(Name);
        public readonly bool IsPointer => PointerLevel > 0;
        public readonly int Size
        {
            get
            {
                return IsPointer ? 1 : TypeCode switch
                {
                    SCodeType.Custom => Declaration?.Size ?? throw new NullReferenceException($"Struct declaration undefined for type {Name}"),
                    SCodeType.Empty => 0,
                    SCodeType.Long => 2,
                    _ => 1
                };
            }
        }

        public override readonly string ToString() => Name + new string('*', PointerLevel);

        public static bool operator ==(TypeInfo left, TypeInfo right) => left.Equals(right);
        public static bool operator !=(TypeInfo left, TypeInfo right) => !left.Equals(right);

        public override bool Equals(object obj)
        {
            if (obj is TypeInfo other)
            {
                return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
                       PointerLevel == other.PointerLevel;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = Name?.ToLower()?.GetHashCode() ?? 0;
            hash = hash * 31 + PointerLevel.GetHashCode();
            return hash;
        }

        public static TypeInfo FromTypeCode(SCodeType typeCode)
        {
            return typeCode switch
            {
                SCodeType.Int => new TypeInfo { Name = "Int", IsBaseType = true },
                SCodeType.Char => new TypeInfo { Name = "Char", IsBaseType = true },
                SCodeType.Decimal => new TypeInfo { Name = "Decimal", IsBaseType = true },
                SCodeType.Long => new TypeInfo { Name = "Long", IsBaseType = true },
                SCodeType.Bool => new TypeInfo { Name = "Bool", IsBaseType = true },
                SCodeType.String => new TypeInfo { Name = "String", IsBaseType = true },
                _ => throw new NotSupportedException(),
            };
        }

        public static TypeInfo FromSystemType(System.Type type)
        {
            return FromTypeCode(GetTypeCodeFromName(type.Name));
        }

        public static SCodeType GetTypeCodeFromName(string name)
        {
            return name.ToLower() switch
            {
                "void" => SCodeType.Empty,
                "int" => SCodeType.Int,
                "int16" => SCodeType.Int,
                "long" => SCodeType.Long,
                "int32" => SCodeType.Long,
                "char" => SCodeType.Char,
                "bool" => SCodeType.Bool,
                "boolean" => SCodeType.Bool,
                "string" => SCodeType.String,
                "decimal" => SCodeType.Decimal,
                "double" => SCodeType.Decimal,
                "float" => SCodeType.Decimal,
                _ => SCodeType.Custom
            };
        }

        public static implicit operator TypeInfo(TypeDescriptor type)
        {
            if (type != null)
            {
                var typeInfo = new TypeInfo
                {
                    Name = type.Name,
                    PointerLevel = type.PointerLevel,
                    IsBaseType = type.IsBaseType
                };
                if (!type.IsBaseType)
                {
                    if (type.CurrentScope.TryGetIdentifier(type.Name, out var identifierInfo) &&
                        identifierInfo.Type == IdentifierInfo.IdentifierType.Struct &&
                        identifierInfo.SourceNode is StructDeclarationStatement @struct)
                    {
                        typeInfo.Declaration = @struct;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Struct declaration undefined in the current scope for {type.Name}.");
                    }
                }
                return typeInfo;
            }
            else
            {
                return Empty;
            }
        }
    }
}
