using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Tests for assembler-level constants declared via "#const".
    /// These constants are validated by the compiler but resolved during assembly,
    /// so runtime tests focus on their substitution effects rather than memory access.
    /// </summary>
    public class AssemblyConstantTests
    {
        // --- 1. Simple constant substitution ---

        [Fact]
        public async Task Basic_ConstantSubstitution_Works()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int VALUE = 42;
                int x = VALUE;
                assert(x == 42);
            ");
        }

        [Fact]
        public async Task Arithmetic_ConstantExpression_Works()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int A = 5;
                #const int B = A * 3 + 2;
                int r = B;
                assert(r == 17);
            ");
        }

        [Fact]
        public async Task Symbolic_Constant_With_PredefinedAssemblerSymbol()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int IODEV = 0x2800;
                #const int LED   = IODEV + 0x002;
                int addr = LED;
                assert(addr == 0x2802);
            ");
        }

        // --- 2. Chained constants and expression references ---

        [Fact]
        public async Task Chained_Constants_AreResolved()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int BASE = 10;
                #const int OFFSET = BASE + 2;
                #const int FINAL = OFFSET * 3;
                int x = FINAL;
                assert(x == 36);
            ");
        }

        // --- 3. Using #const in runtime code (indirect substitution) ---

        [Fact]
        public async Task UseIn_IfCondition_And_Loop()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int LIMIT = 3;
                int s = 0;
                for(int i=0;i<LIMIT;i++) {{
                    s += i;
                }}
                assert(s == 3);
            ");
        }

        [Fact]
        public async Task UseIn_ArrayIndex_AndInitialization()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int IDX = 2;
                int values[] = {{ 1, 2, 3, 4 }};
                assert(values[IDX] == 3);
                values[IDX] = 9;
                assert(values[2] == 9);
            ");
        }

        [Fact]
        public async Task UseIn_FunctionCall()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int BASE = 5;
                int mul(int x) {{ return x * 2; }}
                int r = mul(BASE);
                assert(r == 10);
            ");
        }

        // --- 4. Mixing #const and runtime const/vars ---

        [Fact]
        public async Task Combine_With_RuntimeConst_And_Variable()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int SCALE = 4;
                const int OFFSET = 3;
                int x = 5;
                int r = x * SCALE + OFFSET;
                assert(r == 23);
            ");
        }

        // --- 5. Type correctness enforcement (compile-time only) ---

        [Fact]
        public async Task BoolConstant_And_IntConstant_CanCoexist()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const bool FLAG = true;
                #const int  COUNT = 10;
                if (FLAG) assert(COUNT == 10);
            ");
        }

        // This one cannot be executed at runtime, but we can simulate compile error detection.
        [Fact]
        public async Task Invalid_TypeMismatch_ShouldFail_AtCompileTime()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    #const bool FLAG = 1 + true; // invalid: bool + int
                    int x = 0;
                ");
            });
        }

        // --- 6. Cross-referencing between #const in multiple files (if supported) ---

        [Fact]
        public async Task CrossReference_MultipleConstants()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const int A = 1;
                #const int B = A + 2;
                #const int C = B * 5;
                int x = C;
                assert(x == 15);
            ");
        }

        // --- 7. Use in char and bool types ---

        [Fact]
        public async Task Char_And_Bool_AssemblyConstants()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                #const char LETTER = 'Z';
                #const bool ENABLED = true;
                char c = LETTER;
                bool f = ENABLED;
                assert(c == 'Z');
                assert(f == true);
            ");
        }
    }
}
