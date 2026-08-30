using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class GotoAndLabelsTests
    {
        [Fact]
        public async Task GotoBasic()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int x=0;
                goto L2;
                x=99;
            L2:
                x=42;
                goto end;
                x=11;
            end:
                assert(x==42);
            ");
    }
}
