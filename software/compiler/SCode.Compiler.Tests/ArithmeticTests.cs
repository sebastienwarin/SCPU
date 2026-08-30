using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class ArithmeticTests
    {
        [Theory]
        [InlineData("1 + 2", 3)]
        [InlineData("7 - 5", 2)]
        [InlineData("3 * 4", 12)]
        [InlineData("8 / 2", 4)]
        [InlineData("9 % 4", 1)]
        public async Task BinaryOps(string expr, int expected)
            => await SCodeRunner.ExecuteCodeAsync($"assert(({expr}) == {expected});");

        [Theory]
        [InlineData("+1", 2)]
        [InlineData("-1", 0)]
        public async Task UnarySigns(string expr, int expected)
            => await SCodeRunner.ExecuteCodeAsync($"int a=1; int r = {expr}; assert((r+a) == {expected});");

        [Fact]
        public async Task IncDec_PrefixAndPostfix()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int a = 5;
                int b = ++a;     // a=6, b=6
                int c = a++;     // a=7, c=6
                int d = --a;     // a=6, d=6
                int e = a--;     // a=5, e=6
                assert(a==5 && b==6 && c==6 && d==6 && e==6);                
            ");
    }
}
