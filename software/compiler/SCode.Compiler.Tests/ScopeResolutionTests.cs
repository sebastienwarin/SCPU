using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Tests for identifier resolution across nested scopes: a local declaration must always
    /// shadow an enclosing one, whatever the declaration order in the source file.
    /// </summary>
    public class ScopeResolutionTests
    {
        [Fact]
        public async Task Local_ShadowsGlobal_DeclaredAfterTheFunction()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int f(int n) {{
                    int i = 0;
                    for (i = 0; i < n; i++) {{ }}
                    return i;
                }}

                int i = 99;
                assert(f(3) == 3);
                assert(i == 99);
            ");
        }

        [Fact]
        public async Task LoopVariable_ShadowsGlobal_DeclaredAfterTheFunction()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                void fill(int* dst, int count) {{
                    for (int i = 0; i < count; i++) {{
                        *(dst + i) = i + 1;
                    }}
                }}

                int buffer[3];
                fill(&buffer, 3);

                for (int i = 0; i < 3; i++) {{
                    assert(buffer[i] == i + 1);
                }}
            ");
        }

        [Fact]
        public async Task Parameter_ShadowsGlobal_DeclaredAfterTheFunction()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int twice(int value) {{
                    return value * 2;
                }}

                int value = 7;
                assert(twice(21) == 42);
                assert(value == 7);
            ");
        }

        [Fact]
        public async Task Global_IsVisibleFromFunction_WhenNotShadowed()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int counter = 0;

                void bump() {{
                    counter = counter + 1;
                }}

                bump();
                bump();
                assert(counter == 2);
            ");
        }

        [Fact]
        public async Task SiblingLoops_ReusingTheSameVariableName_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    void foo() {{
                        for (int i = 0; i < 2; i++) {{ }}
                        for (int i = 0; i < 2; i++) {{ }}
                    }}
                    foo();
                ");
            });
        }

        [Fact]
        public async Task Local_CannotShadowAParameter()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    void foo(int x) {{
                        int x = 1;
                    }}
                    foo(0);
                ");
            });
        }
    }
}
