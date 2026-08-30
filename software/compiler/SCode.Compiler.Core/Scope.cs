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

        /// <summary>Identifiers declared in this very scope. Enclosing scopes are resolved by <see cref="TryGetIdentifier(string, out IdentifierInfo)"/>.</summary>
        private Dictionary<string, IdentifierInfo> ReservedIdentifiers { get; } = [];

        private Scope(CompilationContext compilationContext, Scope? parentScope = null)
        {
            Context = compilationContext;
            ParentScope = parentScope;
        }

        public Scope CreateChildScope()
        {
            return new Scope(Context, this);
        }

        public bool RegisterIdentifier(Identifier identifier, IdentifierInfo.IdentifierType type, TypeDescriptor? dataType = null, Node? sourceNode = null)
        {
            var key = identifier.Name;

            // Already declared in this very scope
            if (ReservedIdentifiers.ContainsKey(key))
            {
                return false;
            }

            // Declared in an enclosing scope : shadowing is allowed for some identifier kinds only
            if (TryGetIdentifier(key, out var inheritedValue) && !CanReplace(type, inheritedValue.Type))
            {
                return false;
            }

            // Add identifier to the current scope
            ReservedIdentifiers[key] = new IdentifierInfo(identifier.Name, type, dataType, sourceNode);

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

        public bool TryGetIdentifier(string key, out IdentifierInfo result)
        {
            for (var scope = this; scope != null; scope = scope.ParentScope)
            {
                if (scope.ReservedIdentifiers.TryGetValue(key, out result))
                {
                    return true;
                }
            }

            result = null!;
            return false;
        }

        public bool TryGetIdentifier(Identifier identifier, out IdentifierInfo result)
        {
            return TryGetIdentifier(identifier.Name, out result);
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
}
