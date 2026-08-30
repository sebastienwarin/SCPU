using SCode.Compiler.Instructions;

namespace SCode.Compiler
{
    public class TemporaryVariableManager
    {
        public TemporaryVariableScope? CurrentScope { get; private set; }

        public TemporaryVariableScope CreateScope()
        {
            CurrentScope = new TemporaryVariableScope(this);
            return CurrentScope;
        }

        public TemporaryVariable Create()
        {
            return CurrentScope?.Create() ?? throw new InvalidOperationException("No scope available");
        }

        public void Free(TemporaryVariableScope? parentScope)
        {
            CurrentScope = parentScope;
        }
    }

    public class TemporaryVariableScope(TemporaryVariableManager manager) : IDisposable
    {
        private readonly TemporaryVariableManager _manager = manager;
        private readonly TemporaryVariableScope? _parent = manager.CurrentScope;
        
        public int Index { get; private set; }

        public TemporaryVariable Create()
        {
            if (Index < TemporaryVariable.RegionSize)
            {
                return new TemporaryVariable(Index++);
            }
            else
            {
                throw new OutOfMemoryException("Temporary variable out of range");
            }
        }

        public void Dispose()
        {
            _manager.Free(_parent);
        }
    }

    public class TemporaryVariable(int index)
    {
        public const int RegionSize = 0xF0;

        public int Index { get; } = index;
        public string Address => Index > 0 ? $"({Registers.TemporaryVariables}+{Index})" : Registers.TemporaryVariables;

        public static implicit operator string(TemporaryVariable temporaryVariable)
        {
            return temporaryVariable.Address;
        }

        public static implicit operator ValueOrAddress(TemporaryVariable temporaryVariable)
        {
            return ValueOrAddress.Create(temporaryVariable.Address);
        }
    }
}
