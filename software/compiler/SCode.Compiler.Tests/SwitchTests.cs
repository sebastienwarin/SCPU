using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class SwitchTests
    {
        [Theory]
        [InlineData(1, 10)]
        [InlineData(2, 21)]
        [InlineData(3, 1)]
        [InlineData(123, 99)]
        public async Task SwitchWithDefaultAndFallthrough(ushort x, ushort result)
            => await SCodeRunner.ExecuteCodeAsync($@"
                int x={x}, r=0;
                switch(x) {{
                    case 1: r=10; break;
                    case 2: r=20;
                    case 3: r+=1; break;
                    default: r=99;
                }}
                assert(r=={result});
            ");
    }
}
