using SCode.Compiler.Exceptions;
using System.Reflection;

namespace SCode.Compiler.Ast
{
    public abstract class Node
    {
        private Scope? scope = null;
        public Scope CurrentScope => scope ?? GetScope();

        public CompilationContext Context { get; private set; }
        public SourceRange Source { get; set; }
        public Node Parent { get; protected set; }

        protected virtual void OnPrepare()
        {
            PrepareChildren();
        }
        protected virtual void OnBuild()
        {
            BuildChildren();
        }

        public virtual void Prepare()
        {
            OnPrepare();
        }
        public virtual void Build()
        {
            OnBuild();
        }

        public void PrepareChildren()
        {
            ForEachChildren((node) => node?.Prepare());
        }
        public void BuildChildren()
        {
            ForEachChildren((node) => node?.Build());
        }

        public TNode? GetFirstAncestor<TNode>(Predicate<Node>? predicate = null) where TNode : class
        {
            Node parent = this.Parent;
            while (parent != null && (parent is not TNode || (predicate != null && !predicate.Invoke(parent))))
            {
                parent = parent.Parent;
            }
            return parent as TNode;
        }

        public Scope GetScope()
        {
            Node parent = this.Parent;
            while (parent != null && parent.scope == null)
            {
                parent = parent.Parent;
            }
            return parent?.scope ?? Context.GlobalScope;
        }

        protected virtual void SetContext(CompilationContext context)
        {
            Context = context;
            ForEachChildren((node) => node?.SetContext(context));
        }

        protected virtual void Visit()
        {
            var attribute = this.GetType().GetCustomAttribute<NestedScopeAttribute>();
            if (attribute != null && !attribute.OnlyChild && scope == null)
            {
                scope = CurrentScope.CreateChildScope();
            }

            var childScope = attribute != null && attribute.OnlyChild ? CurrentScope.CreateChildScope() : null;
            ForEachChildren((node) =>
            {
                if (node != null)
                {
                    node.Parent = this;
                    node.scope = childScope;
                    node.Visit();
                }
            });
        }

        public static void Prepare(params Node[] nodes)
        {
            nodes?.ToList().ForEach((node) => node?.Prepare());
        }

        public static void Build(params Node[] nodes)
        {
            nodes?.ToList().ForEach((node) => node?.Build());
        }

        internal NodeCompilerException RaiseError(string message, Exception? exception = null)
        {
            var error = new NodeCompilerException(message, this, innerException: exception);
            Context.Errors.Add(error);
            return error;
        }

        private void ForEachChildren(Action<Node> action)
        {
            foreach (var property in GetChildNodeProperties())
            {
                var value = property.GetValue(this);
                if (value is Node childNode)
                {
                    action(childNode);
                }
                else if (value is IEnumerable<Node> childNodes)
                {
                    childNodes?.ForEach(childNode => action(childNode));
                }
            }
        }

        private IEnumerable<PropertyInfo> GetChildNodeProperties()
        {
            return GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(prop => Attribute.IsDefined(prop, typeof(ChildNodeAttribute)));
        }
    }
}
