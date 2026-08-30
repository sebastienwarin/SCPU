using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Initializers
{
    public class ArrayInitializerExpression : InitializerExpression
    {
        [ChildNode]
        public List<InitializerExpression> Values { get; set; }

        public bool HasOnlyLiteralValues()
        {
            foreach (var variableInitializer in Values)
            {
                if (variableInitializer is ValueInitializerExpression valueInitializer &&
                    valueInitializer.Value is not LiteralExpression)
                {
                    return false;
                }
                else if (variableInitializer is ArrayInitializerExpression arrayInitializer)
                {
                    return arrayInitializer.HasOnlyLiteralValues();
                }
            }
            return true;
        }

        public int CountValues()
        {
            return FlatternLiteralValues().Count;
        }

        public List<object> FlatternLiteralValues()
        {
            var values = new List<object>();
            foreach (var variableInitializer in Values)
            {
                if (variableInitializer is ValueInitializerExpression valueInitializer &&
                    valueInitializer.Value is LiteralExpression literalExpression)
                {
                    values.Add(literalExpression.Literal.Value);
                }
                else if (variableInitializer is ArrayInitializerExpression arrayInitializer)
                {
                    values.AddRange(arrayInitializer.FlatternLiteralValues());
                }
            }
            return values;
        }

        public int[] GetDimensions()
        {
            try
            {
                List<int> dimensions = [];
                GetArrayDimensions(this, dimensions, 0);
                return [.. dimensions];
            }
            catch (InvalidOperationException ex)
            {
                throw RaiseError(ex.Message, ex);
            }
        }

        public override TypeInfo GetResultType()
        {
            TypeInfo typeInfo = TypeInfo.Empty;
            foreach (var initValue in Values)
            {
                var initValueType = initValue.GetResultType();
                if (typeInfo == TypeInfo.Empty)
                {
                    typeInfo = initValueType;
                }
                else if (initValueType != typeInfo)
                {
                    throw RaiseError("Mismatched types in array initialization. All values in the initialization sequence must be of the same type.");
                }
            }
            return typeInfo;
        }

        public override string ToString()
        {
            return $"[{string.Join(", ", Values.Select(c => c.ToString()))}]";
        }

        private static void GetArrayDimensions(ArrayInitializerExpression current, List<int> dimensions, int depth)
        {
            if (current == null) return;

            // Ensure we are correctly tracking the depth
            if (dimensions.Count <= depth)
            {
                dimensions.Add(0);
            }

            // Determine if all elements are homogeneous
            var hasArray = current.Values.Any(i => i is ArrayInitializerExpression);
            var hasScalar = current.Values.Any(i => i is ValueInitializerExpression);
            if (!(hasArray ^ hasScalar))
            {
                throw new InvalidOperationException($"Mixed initializer types detected at dimension {depth + 1}: " +
                     $"Found both scalar values and nested arrays.");
            }

            // Check if all rows in this dimension have the same size
            int currentSize = current.Values.Count;
            if (dimensions[depth] == 0)
            {
                // First size encountered for this dimension
                dimensions[depth] = currentSize;
            }
            else if (dimensions[depth] != currentSize)
            {
                // Inconsistent size detected
                throw new InvalidOperationException($"Inconsistent array sizes detected at dimension {depth + 1}: " +
                    $"Expected {dimensions[depth]} elements, but found {currentSize}.");
            }

            // Iterate through the initializers
            foreach (var initializer in current.Values)
            {
                if (initializer is ArrayInitializerExpression nestedArray)
                {
                    // Recursive call for nested arrays
                    GetArrayDimensions(nestedArray, dimensions, depth + 1);
                }
                // If it is a ValueInitializerExpression, nothing more needs to be done
            }
        }
    }
}
