using SCode.Compiler.Tests.Support;
using SCPU.Architecture;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Smoke tests and core invariants (return, allocation, simple expressions, scope).
    /// </summary>
    public class BasicTests
    {
        [Theory]
        [InlineData((ushort)0)]
        [InlineData((ushort)1)]
        [InlineData((ushort)42)]
        [InlineData((ushort)0xABC)]
        public async Task ReturnValueEndsInAccumulator(ushort value)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($"return {value};");
            Assert.Equal(value, cpu.AccumulatorRegister);
        }

        [Theory]
        [InlineData((ushort)1)]
        [InlineData((ushort)42)]
        public async Task GlobalAllocationWritesFirstUserCell(ushort value)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($"int a = {value};");
            Assert.Equal(value, cpu.LookupValue(MemoryMap.UserPage.Start));
        }

        [Theory]
        [InlineData("1 + 2", 3)]
        [InlineData("42 - 4", 38)]
        [InlineData("2 * 4", 8)]
        [InlineData("8 / 2", 4)]
        [InlineData("10 % 2", 0)]
        public async Task SimpleBinaryExpressionsAreCorrect(string expr, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($"assert(({expr}) == {expected});");
        }

        [Fact]
        public async Task GlobalVsLocalScopeAndByValueSemantics()
        {
            await SCodeRunner.ExecuteCodeAsync(@"
                int x = 7;
                int y = 3;
                demo(x++, y);
                // x++ passed by value ⇒ global x increments once
                assert(x == 8);

                void demo(int x, int y) {
                  x++;                   // local x
                  assert(x == 8);        // local became 8
                  assert(y == 3);
                }
            ");
        }
    }
}
