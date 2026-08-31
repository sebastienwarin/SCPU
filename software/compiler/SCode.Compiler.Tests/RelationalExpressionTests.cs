using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Relational and equality operators over literals, locals, globals, parameters,
    /// function returns and grouped/nested expressions.
    /// </summary>
    public class RelationalExpressionTests
    {
        // --- literals & grouping ---

        [Fact]
        public async Task Literals_And_Grouping()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                assert( (3 < 5) == true );
                assert( (5 > 3) == true );
                assert( (5 <= 5) == true );
                assert( (5 >= 6) == false );
                assert( (42 == 42) == true );
                assert( (42 != 24) == true );

                // Grouping: precedence correctness
                bool r1 = (1 + 2) * 3 < 10;      // 9 < 10 → true
                bool r2 = 4 * (2 + 1) == 12;     // 12 == 12 → true
                assert(r1 && r2);
            ");
        }

        // --- locals ---

        [Theory]
        [InlineData(2, 5, true)]
        [InlineData(7, 1, false)]
        [InlineData(5, 5, false)]
        public async Task Locals_Lt(int a, int b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = {a};
                int b = {b};
                assert((a < b) == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(2, 5, false)]
        [InlineData(7, 1, true)]
        [InlineData(5, 5, false)]
        public async Task Locals_Gt(int a, int b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = {a};
                int b = {b};
                assert((a > b) == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(5, 6, true)]
        [InlineData(6, 5, false)]
        public async Task Locals_Le(int a, int b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = {a};
                int b = {b};
                assert((a <= b) == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(4, 5, false)]
        [InlineData(6, 5, true)]
        public async Task Locals_Ge(int a, int b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = {a};
                int b = {b};
                assert((a >= b) == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(42, 42, true)]
        [InlineData(42, 24, false)]
        public async Task Locals_Eq(int a, int b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = {a};
                int b = {b};
                assert((a == b) == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(42, 42, false)]
        [InlineData(42, 24, true)]
        public async Task Locals_Ne(int a, int b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = {a};
                int b = {b};
                assert((a != b) == {expected.ToString().ToLower()});
            ");
        }

        // --- parameters + function returns ---

        [Theory]
        [InlineData(4, 3, 1, false)] // 8 < 4 = false
        [InlineData(2, 9, 2, true)]  // 4 < 11 = true
        public async Task FunctionParams_And_Returns(int x, int y, int z, bool expectedLt)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int id(int v) {{ return v*2; }}
                int sum(int a, int b) {{ return a + b; }}
                assert( (id({x}) < sum({y}, {z})) == {expectedLt.ToString().ToLower()} );
            ");
        }

        // --- globals used inside expressions ---

        [Fact]
        public async Task Globals_In_Relational()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int G = 10;
                bool less = (G - 2) < (G + 1);   // 8 < 11 → true
                bool eq   = (G * 2) == 20;
                bool ge   = (G + 5) >= 15;
                assert(less && eq && ge);
            ");
        }

        // --- mixing bools via implicit conversion to int (if defined in S-Code) ---

        [Fact]
        public async Task MixedBoolInt_Relations()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool t = true, f = false;
                // if bool→int: true=1, false=0
                assert((t == true) && (f == false));
                assert(((t ? 1 : 0) < 2) == true);
                assert(((f ? 1 : 0) >= 1) == false);
            ");
        }

        [Fact]
        public async Task Int_UsesSignedOrdering()
        {
            await SCodeRunner.ExecuteCodeAsync(@"
                int negative = -1;
                int minimum = -32768;
                int maximum = 32767;

                assert(negative < 2);
                assert(minimum < negative);
                assert(maximum > negative);
                assert(negative <= -1);
                assert(negative >= -1);
            ");
        }

        [Fact]
        public async Task UInt_UsesUnsignedOrdering()
        {
            await SCodeRunner.ExecuteCodeAsync(@"
                uint value = 0xFFFF;
                uint zero = 0;

                assert(value > 2);
                assert(value >= 0xFFFF);
                assert(zero < value);
                assert((value < 2) == false);

                uint count = 0;
                for (uint i = 0; i < 3; i++) {
                    count++;
                }
                assert(count == 3);
            ");
        }
    }
}
