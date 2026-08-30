using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Casting between bool/int/long and in compound expressions.
    /// Assumes S-Code: int=16-bit, long=32-bit; bool maps to int (true=1, false=0).
    /// </summary>
    public class CastTests
    {
        // --- bool <-> int ---

        [Fact]
        public async Task BoolInt_Casts()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool t = true, f = false;
                int it = (int)t;   // 1
                int iff = (int)f;  // 0
                assert(it == 1 && iff == 0);

                bool bt = (bool)5; // non-zero → true
                bool bf = (bool)0; // zero → false
                assert(bt == true && bf == false);
            ");
        }

        // --- int <-> long ---

        [Fact]
        public async Task IntLong_Casts_RoundTrips()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int  i = 0x1234;
                long L = (long)i;
                int  j = (int)L;
                assert(i == j);
            ");
        }

        // --- narrowing overflow semantics (documented behavior) ---

        [Fact(Skip = "Long not yet supported")]
        public async Task LongToInt_Narrowing_Wraps16Bits()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                // Assuming cast to int keeps the lower 16 bits (wrap/truncate).
                long big = 0x1002A;   // 65578 decimal = 0x0001_002A
                int  i   = (int)big;  // expect 0x002A
                assert(i == 0x002A);
            ");
        }

        // --- casts inside expressions & comparisons ---

        [Fact]
        public async Task Casts_In_Expressions_And_Relations()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool flag = (bool)(3 - 3);   // 0 → false
                assert(flag == false);

                long a = 1000;
                long b = 24;
                int sumLow16 = (int)(a + b); // 1024
                assert(sumLow16 == 1024);

                // mixed compare via cast
                int  x = 500;
                long y = 500;
                assert( ((long)x == y) == true );
                assert( ((long)(x+1) == y) == false );
            ");
        }
    }
}
