using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class RecursionTests
    {
        [Theory]
        [InlineData(4, 24)]
        [InlineData(5, 120)]
        [InlineData(6, 720)]
        public async Task Factorial(int value, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync(@$"
                int fact(int n){{ return (n<=1)?1:n*fact(n-1); }}
                return fact({value});
            ");
            Assert.Equal(expected, cpu.AccumulatorRegister);
        }
    }
}