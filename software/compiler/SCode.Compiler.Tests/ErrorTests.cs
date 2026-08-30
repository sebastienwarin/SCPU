using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class ErrorTests
    {
        [Theory]
        [InlineData("test2 = -a;")] // Undefined identifier
        public async Task SyntaxOrSemanticErrors(string badCode)
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
                await SCodeRunner.ExecuteCodeAsync(badCode));
        }
    }
}
