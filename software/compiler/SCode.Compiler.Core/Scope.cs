using SCode.Compiler.Ast;
using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Ast.Statements.VariableDeclaration;

namespace SCode.Compiler
{
    public class Scope
    {
        public CompilationContext Context { get; private set; }

        public string UniqueId { get; } = RandomGenerator.RandomString();

        public Scope? ParentScope { get; private set; }
        public bool IsGlobalScope => ParentScope == null;

        public event EventHandler<IdentifierRegisteredEventArgs>? NewIdentifierRegistered;
        public Dictionary<string, IdentifierInfo> ReservedIdentifiers { get; } = [];

        private Scope(CompilationContext compilationContext, Scope? parentScope = null)
        {
            Context = compilationContext;
            ParentScope = parentScope;
            if (ParentScope != null)
            {
                ReservedIdentifiers = new Dictionary<string, IdentifierInfo>(ParentScope.ReservedIdentifiers);
                ParentScope.NewIdentifierRegistered += (s, e) =>
                {
                    ReservedIdentifiers[e.Key] = e.Info;
                    NewIdentifierRegistered?.Invoke(s, e);
                };
            }
        }

        public Scope CreateChildScope()
        {
            return new Scope(Context, this);
        }

        public bool RegisterIdentifier(Identifier identifier, IdentifierInfo.IdentifierType type, TypeDescriptor? dataType = null, Node? sourceNode = null)
        {
            var key = identifier.Name;

            // Check existing identifiers
            if (ReservedIdentifiers.TryGetValue(key, out var existingValue) &&
                (IsGlobalScope || existingValue.SourceNode?.CurrentScope == sourceNode?.CurrentScope || !CanReplace(type, existingValue.Type)))
            {
                return false;
            }
            else
            {
                // Add identifier to the current scope and child's scopes
                ReservedIdentifiers[key] = new IdentifierInfo(identifier.Name, type, dataType, sourceNode);
                NewIdentifierRegistered?.Invoke(this, new IdentifierRegisteredEventArgs
                {
                    Key = key,
                    Info = ReservedIdentifiers[key]
                });

                // For local variable in function
                if (type == IdentifierInfo.IdentifierType.Variable &&
                    sourceNode is VariableDeclarator variableDeclarator &&
                    !variableDeclarator.IsGlobalOrStatic)
                {
                    var functionDeclaration = variableDeclarator.GetFirstAncestor<FunctionDeclarationStatement>();
                    if (functionDeclaration != null)
                    {
                        // Calcul the variable offset from the FP
                        variableDeclarator.Offset = functionDeclaration.LocalVariables.Sum(x => x.Size) + 1;
                        // Register the local variable
                        functionDeclaration.LocalVariables.Add(variableDeclarator);
                    }
                    else
                    {
                        throw new InvalidOperationException("A non-global variable must be a child of a function statement");
                    }
                }

                // Done
                return true;
            }
        }

        public bool TryGetIdentifier(string key, out IdentifierInfo result)
        {
            return ReservedIdentifiers.TryGetValue(key, out result);
        }

        public bool TryGetIdentifier(Identifier identifier, out IdentifierInfo result)
        {
            return ReservedIdentifiers.TryGetValue(identifier.Name, out result);
        }

        public static Scope CreateGlobalScope(CompilationContext compilationContext)
        {
            return new Scope(compilationContext);
        }

        private static bool CanReplace(IdentifierInfo.IdentifierType identifierType, IdentifierInfo.IdentifierType existingIdentifierType)
        {
            if (identifierType == IdentifierInfo.IdentifierType.Struct)
            {
                // A struct identifier is always reserved
                return false;
            }
            else if (identifierType == IdentifierInfo.IdentifierType.Parameter && existingIdentifierType != IdentifierInfo.IdentifierType.Parameter)
            {
                // A parameter replace all existing identifier except parameter
                return true;
            }
            else if ((identifierType == IdentifierInfo.IdentifierType.Variable || identifierType == IdentifierInfo.IdentifierType.Constant) &&
                existingIdentifierType != IdentifierInfo.IdentifierType.Parameter)
            {
                // A variable or constant replace all existing identifier except parameter
                return true;
            }
            else
            {
                // Otherwise, the identifier is already declared and can not be replace
                return false;
            }
        }
    }

    public class IdentifierRegisteredEventArgs : EventArgs
    {
        public string Key { get; set; }
        public IdentifierInfo Info { get; set; }
    }
}
