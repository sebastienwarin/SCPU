using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class AssignmentTests
    {
        [Theory]
        [InlineData("+=2", 4)]
        [InlineData("-=2", 0)]
        [InlineData("*=2", 4)]
        [InlineData("/=2", 1)]
        [InlineData("%=2", 0)]
        [InlineData("&=3", 2)]
        [InlineData("|=1", 3)]
        [InlineData("^=3", 1)]
        [InlineData("<<=1", 4)]
        [InlineData(">>=1", 1)]
        public async Task CompoundAssignments(string op, int expected)
            => await SCodeRunner.ExecuteCodeAsync($@"
                int a = 2;
                a{op};
                assert(a == {expected});
            ");
    }
}
