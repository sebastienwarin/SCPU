using SCode.Compiler.Ast.Expressions;
using SCode.Compiler.Ast.Expressions.Initializers;
using SCode.Compiler.Ast.Expressions.Unary;
using SCode.Compiler.Ast.Literals;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Statements.VariableDeclaration
{
    public class VariableDeclarator : Node
    {
        public VariableDeclarationStatement Declaration => Parent as VariableDeclarationStatement;

        public FunctionDeclarationStatement? FunctionDeclaration => GetFirstAncestor<FunctionDeclarationStatement>();

        [ChildNode]
        public Identifier Identifier { get; set; }

        [ChildNode]
        public InitializerExpression? Initializer { get; set; }

        [ChildNode]
        public ArraySpecifier? ArraySpecifier { get; set; }

        public int Offset { get; set; }

        public int Size => GetTotalSize();
        public bool IsArray => ArraySpecifier != null && ArraySpecifier.Count > 0;
        public bool IsGlobalOrStatic => Declaration.IsStatic || FunctionDeclaration == null;

        protected override void OnPrepare()
        {
            // Reserve identifier & check type
            if (!CurrentScope.RegisterIdentifier(Identifier,
                Declaration.IsConst ? IdentifierInfo.IdentifierType.Constant : IdentifierInfo.IdentifierType.Variable,
                Declaration.Type, this))
            {
                throw RaiseError($"The {(Declaration.IsConst ? "constant" : "variable")} '{Identifier}' is already defined in the current scope.");
            }
            else if (Initializer != null &&
                (!Declaration.Type.CanAssignTo(Initializer.GetResultType()) &&
                !(Declaration.Type.TypeInfo == TypeInfo.String && Initializer is ValueInitializerExpression initializerExpression && initializerExpression.Value is AddressOfExpression)))
            {
                throw RaiseError($"Cannot initialize {(Declaration.IsConst ? "constant" : "variable")} '{Identifier}' of type '{Declaration.Type}' with a value '{Initializer}'.");
            }

            // Prepare children
            PrepareChildren();

            // Check constant initializer
            if (Declaration.IsConst)
            {
                if (Initializer == null)
                {
                    throw RaiseError($"The constant '{Identifier}' must be initialized.");
                }
                else if (Initializer is ValueInitializerExpression valueInitializer && valueInitializer.Value is not LiteralExpression)
                {
                    throw RaiseError($"The constant '{Identifier}' must be initialized with a literal value.");
                }
                else if (Initializer is ArrayInitializerExpression arrayInitializer && !arrayInitializer.HasOnlyLiteralValues())
                {
                    throw RaiseError($"Array initialization values must be literals for {Identifier}.");
                }
            }

            // Check array
            if (IsArray)
            {
                var arrayInitializer = Initializer as ArrayInitializerExpression;
                if (ArraySpecifier!.Sizes.Length == 0 && (arrayInitializer == null || arrayInitializer.Values.Count == 0))
                {
                    throw RaiseError($"Array size must be specified either in the declaration or with an initializer  for {Identifier}.");
                }
                else if (arrayInitializer != null)
                {
                    var dimensions = arrayInitializer.GetDimensions();
                    if (!arrayInitializer.HasOnlyLiteralValues())
                    {
                        throw RaiseError($"Array initialization values must be literals for {Identifier}.");
                    }
                    else if (dimensions.Length != ArraySpecifier!.Count)
                    {
                        throw RaiseError($"Array dimensions mismatch for {Identifier}.");
                    }
                    else if (ArraySpecifier!.Sizes.Length > 0 && !ArraySpecifier!.Sizes.SequenceEqual(dimensions))
                    {
                        throw RaiseError($"Array initializer has too many elements for {Identifier}.");
                    }
                    else if (ArraySpecifier!.Sizes.Length == 0)
                    {
                        ArraySpecifier!.Sizes = dimensions;
                    }
                }
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            int totalSize = GetTotalSize();

            if (!CurrentScope.TryGetIdentifier(Identifier, out var identifierInfo))
            {
                throw RaiseError($"Undefined identifier '{Identifier}'");
            }

            string staticInitFlag = $"{identifierInfo.UniqueName}_Initialized";

            // Memory reservation for global or static variable (exclude const and local variable)
            if (!Declaration.IsConst && IsGlobalOrStatic)
            {
                builder.AssemblyBuilder.AddMemoryReservation(identifierInfo.UniqueName, totalSize);
                // For static variable, reserved a boolean variable for init flag
                if (Declaration.IsStatic)
                {
                    builder.AssemblyBuilder.AddMemoryReservation(staticInitFlag, TypeInfo.Bool.Size);
                }
            }

            // Variable initializer
            if (Initializer != null)
            {
                bool hasProgramData = false;
                string resourceKey = Declaration.IsConst ? Identifier : $"__data_{identifierInfo.UniqueName}";

                bool isLiteralString = false;
                var literalStringItems = new List<string>();

                ValueInitializerExpression? valueInitializer = Initializer as ValueInitializerExpression;

                // Declare Program data
                if (valueInitializer?.Value is LiteralExpression literalExpression &&
                    (literalExpression.Literal is not LiteralString str || str.Value != null))
                {
                    hasProgramData = true;

                    // Special case for literal string
                    if (literalExpression.Literal is LiteralString)
                    {
                        isLiteralString = true;
                        if (Declaration.IsConst)
                        {
                            var dataKey = $"__data_{identifierInfo.UniqueName}";
                            builder.DeclareProgramData(dataKey, literalExpression.Literal.Value, true);
                            builder.AssemblyBuilder.AddData(Instructions.BankType.ProgramData, resourceKey, dataKey, true);
                        }
                        else
                        {
                            resourceKey = builder.DeclareProgramData(resourceKey, literalExpression.Literal.Value);
                        }
                    }
                    else // For other Literal value
                    {
                        resourceKey = builder.DeclareProgramData(resourceKey, literalExpression.Literal.Value, Declaration.IsConst);
                    }
                }
                else if (Initializer is ArrayInitializerExpression arrayInitializer)
                {
                    var values = arrayInitializer.FlatternLiteralValues();
                    if (values.Count > 0)
                    {
                        hasProgramData = true;

                        // Special case for string array 
                        if (Declaration.Type.TypeInfo.TypeCode == SCodeType.String && IsArray)
                        {
                            isLiteralString = true;
                            for (int i = 0; i < values.Count; i++)
                            {
                                // Declare each string element in Program Data
                                literalStringItems.Add(builder.DeclareProgramData($"{resourceKey}__item{i}", values[i], true));
                            }

                            if (Declaration.IsConst)
                            {
                                // Declare array of pointers referencing each string for const only
                                builder.AssemblyBuilder.AddData(Instructions.BankType.ProgramData, resourceKey, string.Join(",", literalStringItems), true);
                            }
                        }
                        else
                        {
                            // Regular array : declare all values in one Program Data block
                            resourceKey = builder.DeclareProgramData(resourceKey, values.ToArray(), Declaration.IsConst);
                        }
                    }
                }

                // Do nothing else for constant
                if (Declaration.IsConst) return;

                // Load values from the ROM
                if (hasProgramData)
                {
                    // Global or static variables
                    if (IsGlobalOrStatic)
                    {
                        var bypassInitializerLabel = RandomGenerator.RandomStringLabel("bypass_static_init");

                        // Bypass initalizer if already initialized for static variable
                        if (Declaration.IsStatic)
                        {
                            builder.EmitLoadA(staticInitFlag);
                            builder.EmitJumpIfNotZero(bypassInitializerLabel);
                            builder.EmitMove(1, staticInitFlag);
                        }

                        // Emit load data
                        if (isLiteralString)
                        {
                            // For string array
                            if (literalStringItems.Count > 0)
                            {
                                for (int i = 0; i < literalStringItems.Count; i++)
                                {
                                    builder.EmitMove(literalStringItems[i].AsImmediateValue(), identifierInfo.UniqueName + $"+{i}");
                                }
                            }
                            else // For string
                            {
                                builder.EmitMove(resourceKey.AsImmediateValue(), identifierInfo.UniqueName);
                            }
                        }
                        else
                        {
                            // For other variable
                            for (int i = 0; i < totalSize; i++)
                            {
                                builder.EmitMove($"{resourceKey}+{i}", identifierInfo.UniqueName + $"+{i}");
                            }
                        }

                        // Set bypass label for static variable only
                        if (Declaration.IsStatic)
                        {
                            builder.SetLabel(bypassInitializerLabel);
                        }
                    }
                    else
                    {
                        // Local variables (Stack)
                        var functionDeclaration = GetFirstAncestor<FunctionDeclarationStatement>();
                        if (functionDeclaration != null)
                        {
                            functionDeclaration.LoadIdentifierAddress(identifierInfo, Registers.RPeek);

                            if (isLiteralString)
                            {
                                // For string array
                                if (literalStringItems.Count > 0)
                                {
                                    for (int i = 0; i < literalStringItems.Count; i++)
                                    {
                                        builder.EmitMove(literalStringItems[i].AsImmediateValue(), Registers.RPeek.AsIndirectAddress());
                                        builder.EmitDecrement(Registers.RPeek);
                                        builder.EmitStoreA(Registers.RPeek);
                                    }
                                }
                                else // For string
                                {
                                    builder.EmitMove(resourceKey.AsImmediateValue(), Registers.RPeek.AsIndirectAddress());
                                }
                            }
                            else
                            {
                                // For other variable
                                builder.EmitMove(resourceKey, Registers.RPeek.AsIndirectAddress());
                                for (int i = 1; i < totalSize; i++)
                                {
                                    builder.EmitDecrement(Registers.RPeek);
                                    builder.EmitStoreA(Registers.RPeek);
                                    builder.EmitMove($"{resourceKey}+{i}", Registers.RPeek.AsIndirectAddress());
                                }
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
                else if (valueInitializer != null)
                {
                    // For global or static variables
                    if (IsGlobalOrStatic)
                    {
                        var bypassInitializerLabel = RandomGenerator.RandomStringLabel("bypass_static_init");

                        // Bypass initalizer if already initialized for static variable
                        if (Declaration.IsStatic)
                        {
                            builder.EmitLoadA(staticInitFlag);
                            builder.EmitJumpIfNotZero(bypassInitializerLabel);
                            builder.EmitMove(1, staticInitFlag);
                        }

                        // Build initializer expression
                        valueInitializer.Value.Build();

                        // Store value to the variable
                        builder.EmitStoreA(identifierInfo.UniqueName);

                        // Set bypass label for static variable only
                        if (Declaration.IsStatic)
                        {
                            builder.SetLabel(bypassInitializerLabel);
                        }
                    }
                    else // For local variable
                    {
                        // Build initializer expression
                        valueInitializer.Value.Build();

                        // Store value to the variable on the stack
                        var functionDeclaration = GetFirstAncestor<FunctionDeclarationStatement>();
                        if (functionDeclaration != null)
                        {
                            // Save value to a temp variable
                            var tempVar = Context.TemporaryVariables.Create();
                            builder.EmitStoreA(tempVar);

                            // Store the value to the local variable address
                            functionDeclaration.LoadIdentifierAddress(identifierInfo, Registers.RPeek);
                            builder.EmitMove(tempVar, Registers.RPeek.AsIndirectAddress());
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
            }
        }

        private int GetTotalSize()
        {
            // Variable sizes
            int itemSize = ((TypeInfo)Declaration.Type).Size;
            int arraySize = IsArray ? ArraySpecifier!.TotalSize : 1;

            // Determine array size from the initializer
            if (IsArray && Initializer is ArrayInitializerExpression arrayInitializer)
            {
                arraySize = arrayInitializer.CountValues();
            }

            return (itemSize * arraySize);
        }

        public override string ToString() => $"{Identifier}{ArraySpecifier}{(Initializer != null ? " = " : "")}{Initializer}";
    }
}
