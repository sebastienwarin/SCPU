namespace SCode.Compiler.Type
{
    /// <summary>
    /// Provides utility methods for type conversion and inference in the S-Code language.
    /// Handles implicit/explicit casts, pointer conversions, and type deduction for binary expressions.
    /// </summary>
    public static class TypeHelper
    {
        private enum ConversionType { None, Implicit, Explicit }

        /// <summary>
        /// Defines the conversion compatibility matrix between primitive S-Code types.
        /// Each cell indicates whether a conversion is implicit, explicit, or not allowed.
        /// </summary>
        private static readonly ConversionType[,] TypeConversion = new ConversionType[,]
        {
            //            To:   Empty  Int      UInt     Char     Decimal  Long     Bool     String   Custom
            /* From Empty   */ { ConversionType.None, ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None },
            /* From Int     */ { ConversionType.None, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.None,     ConversionType.None },
            /* From UInt    */ { ConversionType.None, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.None,     ConversionType.None },
            /* From Char    */ { ConversionType.None, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.None,     ConversionType.Explicit, ConversionType.None },
            /* From Decimal */ { ConversionType.None, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.None },
            /* From Long    */ { ConversionType.None, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.None },
            /* From Bool    */ { ConversionType.None, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Implicit, ConversionType.Explicit, ConversionType.None },
            /* From String  */ { ConversionType.None, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Explicit, ConversionType.Implicit, ConversionType.None },
            /* From Custom  */ { ConversionType.None, ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None,     ConversionType.None },
        };

        /// <summary>
        /// Defines the resulting type for binary operations between two operands.
        /// Each cell represents the resulting S-Code type when combining the corresponding left and right types.
        /// </summary>
        private static readonly SCodeType?[,] ResultTypes = new SCodeType?[,]
        {
            //          Right:   Empty  Int                UInt               Char               Decimal            Long               Bool             String             Custom
            /* Left: Empty   */ { null,  null,              null,              null,              null,              null,              null,            null,              null             },
            /* Left: Int     */ { null,  SCodeType.Int,     SCodeType.Int,     SCodeType.Int,     SCodeType.Decimal, SCodeType.Long,    null,            null,              null             },
            /* Left: UInt    */ { null,  SCodeType.Int,     SCodeType.UInt,    SCodeType.UInt,    SCodeType.Decimal, SCodeType.Long,    null,            null,              null             },
            /* Left: Char    */ { null,  SCodeType.Int,     SCodeType.UInt,    SCodeType.Char,    SCodeType.Decimal, SCodeType.Long,    null,            null,              null             },
            /* Left: Decimal */ { null,  SCodeType.Decimal, SCodeType.Decimal, SCodeType.Decimal, SCodeType.Decimal, SCodeType.Decimal, null,            null,              null             },
            /* Left: Long    */ { null,  SCodeType.Long,    SCodeType.Long,    SCodeType.Long,    SCodeType.Decimal, SCodeType.Long,    null,            null,              null             },
            /* Left: Bool    */ { null,  null,              null,              null,              null,              null,              SCodeType.Bool,  null,              null             },
            /* Left: String  */ { null,  null,              null,              null,              null,              null,              null,            SCodeType.String,  null             },
            /* Left: Custom  */ { null,  null,              null,              null,              null,              null,              null,            null,              SCodeType.Custom },
        };

        /// <summary>
        /// Determines if a value of type <paramref name="fromType"/> can be converted to <paramref name="toType"/>.
        /// Takes into account implicit and explicit casts, as well as pointer conversions.
        /// </summary>
        public static bool CanConvert(TypeInfo fromType, TypeInfo toType, bool isExplicit = false)
        {
            // Lookup the general conversion rule from the type conversion matrix
            var conversionType = TypeConversion[(int)fromType.TypeCode, (int)toType.TypeCode];

            return
                // 1. Exact same type → always convertible
                (fromType == toType) ||

                // 2. Implicit pointer-to-int cast (e.g. pointer to address)
                (fromType.IsPointer && toType.TypeCode == SCodeType.Int) ||

                // 3. Implicit int-to-pointer cast (e.g. assigning numeric address to pointer)
                (fromType.TypeCode == SCodeType.Int && !fromType.IsPointer && toType.IsPointer) ||

                // 4. String-from/to-char* cast
                (fromType.TypeCode == SCodeType.String && toType.TypeCode == SCodeType.Char && !fromType.IsPointer && toType.IsPointer) ||
                (fromType.TypeCode == SCodeType.Char && toType.TypeCode == SCodeType.String && fromType.IsPointer && !toType.IsPointer) ||

                // 5. Implicit conversion allowed (e.g. int → bool, char → int, etc.) with same pointer depth
                (conversionType == ConversionType.Implicit && fromType.PointerLevel == toType.PointerLevel) ||

                // 6. Explicit cast to string or pointer type (e.g. (char*)someInt) 
                (isExplicit && (toType.IsPointer || toType.TypeCode == SCodeType.String) && CanConvert(fromType, TypeInfo.Int)) ||

                // 7. Explicit cast allowed according to the conversion matrix (same pointer level)
                (isExplicit && conversionType == ConversionType.Explicit && fromType.PointerLevel == toType.PointerLevel);
        }

        /// <summary>
        /// Attempts to determine the resulting type of a binary operation between two operands.
        /// </summary>
        /// <param name="left">Left operand type.</param>
        /// <param name="right">Right operand type.</param>
        /// <param name="resultType">Receives the resulting type if the operation is valid.</param>
        /// <returns><see langword="true"/> if the combination is valid and produces a result type; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetBinaryResultType(TypeInfo left, TypeInfo right, out TypeInfo resultType)
        {
            var resultTypeCode = ResultTypes[(int)left.TypeCode, (int)right.TypeCode];
            if (resultTypeCode.HasValue)
            {
                resultType = TypeInfo.FromTypeCode(resultTypeCode.Value);
                return true;
            }
            else if (((right.TypeCode == SCodeType.Int || right.TypeCode == SCodeType.UInt) && (left.IsPointer || left.TypeCode == SCodeType.String)) ||
                    ((left.TypeCode == SCodeType.Int || left.TypeCode == SCodeType.UInt) && (right.IsPointer || right.TypeCode == SCodeType.String)))
            {
                resultType = TypeInfo.Int;
                return true;
            }
            else
            {
                resultType = TypeInfo.Empty;
                return false;
            }
        }
    }
}
