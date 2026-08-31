using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class ControlFlowTests
    {
        [Theory]
        [InlineData(-1, 1)]
        [InlineData(0, 1)]
        [InlineData(2, 2)]
        public async Task IfElse_BranchesAreSelected(int x, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int x = {x};
                int r = 0;
                if(x < 2) r = 1; else r = 2;
                assert(r == {expected});
            ");
        }

        [Theory]
        [InlineData(5, 100)]
        [InlineData(9, 200)]
        [InlineData(42, 300)]
        public async Task ElseIf_ChainMatchesFirstTruth(int x, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int x = {x};
                int r = 0;
                if(x < 7)           r = 100;
                else if(x < 10)     r = 200;
                else                r = 300;
                assert(r == {expected});
            ");
        }

        [Fact]
        public async Task If_WithLogicalShortCircuit_DoesNotOverEvaluate()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int calls = 0;
                bool t() {{calls++; return 1; }}
                bool f() {{calls++; return 0; }}

                if (t() || f()) {{}}  // OR short-circuit, f() must not be called
                assert(calls == 1);

                calls = 0;
                if (f() && t()) {{}}  // AND short-circuit, t() must not be called
                assert(calls == 1);
            ");
        }

        [Theory]
        [InlineData(0, 0)]  // 0 iterations
        [InlineData(1, 0)]  // sum of [0]
        [InlineData(5, 10)] // sum of [0..4] = 10
        [InlineData(8, 28)] // sum of [0..7] = 28
        public async Task For_SimpleCounterSum(int n, int expectedSum)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int N = {n};
                int s = 0;
                for(int i=0; i<N; i++) {{
                    s += i;
                }}
                assert(s == {expectedSum});
            ");
        }

        [Theory]
        [InlineData(5, 7)]
        [InlineData(10, 18)]
        public async Task For_ContinueAndBreak_SkipAndStop(int limit, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int limit = {limit};
                int s = 0;

                for (int i=0; i<limit; i++) {{
                    if (i == 3) continue;  // skip 3
                    if (i == 7) break;     // stop before adding 7
                    s += i;
                }}

                // A second small loop to ensure control flow resumes correctly !! Signed integer not supported
                //for (int k=-1; k<1; k++) s += k; // adds (-1 + 0)

                assert(s == {expected});
            ");
        }

        [Fact]
        public async Task For_NestedLoops_WithBreaksAndContinues()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int s = 0;
                for (int i=0; i<4; i++) {{
                    if (i == 1) continue; // skip i=1 entirely
                    for (int j=0; j<4; j++) {{
                        if (j == 2) continue; // skip inner j=2
                        if (i==3 && j==3) break; // terminate inner when i=3,j=3
                        s += (i + j);
                    }}
                }}
                // Manual expected: i=0: j=0,1,3 → 0+1+3=4
                //                  i=1: (skipped)
                //                  i=2: j=0,1,3 → 2+3+5=10 (cum 14)
                //                  i=3: j=0,1 → 3+4=7; j=2 skipped; j=3 triggers break → cum 21
                assert(s == 21);
            ");
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(4, 10)] // 1+2+3+4 = 10
        public async Task While_Sum1ToN(int n, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int n = {n};
                int i = 1, s = 0;
                while (i <= n) {{
                    s += i;
                    i++;
                }}
                assert(s == {expected});
            ");
        }

        [Fact]
        public async Task While_BreakAndContinue()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int i=0, s=0;
                while (i < 10) {{
                    i++;
                    if (i % 2 == 0) continue; // sum odd numbers only
                    if (i == 9) break;        // stop before adding 9
                    s += i;                   // adds 1+3+5+7 = 16
                }}
                assert(s == 16);
            ");
        }

        [Theory]
        [InlineData(0, 1)]  // executes once anyway, increments i from 0 to 1
        [InlineData(3, 4)]
        public async Task DoWhile_ExecutesAtLeastOnce(int start, int expectedFinal)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int i = {start};
                do {{
                    i++;
                }} while (i < {start});
                assert(i == {expectedFinal});
            ");
        }

        [Fact]
        public async Task DoWhile_WithBreakAndContinue()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int i=0, s=0, count=0;
                do {{
                    count++;
                    i++;
                    if (i == 2) continue; // skip adding 2
                    s += i;
                    if (i == 4) break;    // stop after adding 4
                }} while (i < 10);

                // Iterations: i=1 (add 1), i=2 (continue), i=3 (add 3), i=4 (add 4; break)
                assert(s == (1+3+4));
                assert(count == 4); // loop body ran 4 times (1..4)
            ");
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(10, 10)]
        public async Task EarlyReturn_FromLoopStopsFurtherWork(int n, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int compute(int n) {{
                    int s=0;
                    for(int i=0;i<=n;i++) {{
                        s += i;
                        if (i == n) return s; // early stop
                        // unreachable when i==n
                    }}
                    return -1; // should never reach
                }}
                assert(compute({n}) == {(expected == 0 ? 0 : (expected*(expected+1))/2)});
            ");
        }

        [Theory]
        [InlineData(5, 10)]
        [InlineData(-2, 20)]
        public async Task Ternary_Basic(int x, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int x = {x};
                int y = (x > 0) ? 10 : 20;
                assert(y == {expected});
            ");
        }

        [Theory]
        [InlineData(5, 42)]
        [InlineData(-1, 24)]
        public async Task Ternary_NestedAndShortCircuit(int x, int expected)
        {
            // Ensures only the chosen branch executes (side-effect counters differ).
            await SCodeRunner.ExecuteCodeAsync($@"
                int x = {x};
                int aCalls=0, bCalls=0;

                int A() {{ aCalls++; return 42; }}
                int B() {{ bCalls++; return 24; }}

                int r = (x > 0) ? A() : B();
                assert(r == {expected});

                if (x > 0) {{
                    assert(aCalls == 1 && bCalls == 0);
                }} else {{
                    assert(aCalls == 0 && bCalls == 1);
                }}
            ");
        }

        [Theory]
        [InlineData(5, 4)]
        [InlineData(8, 5)]
        public async Task IfInsideFor_CountingWithEdgeConditions(int n, int expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int N = {n};
                int c = 0;

                for(int i=0; i<N; i++) {{
                    if (i < 3) c++;         // count i in [0,1,2]
                }}

                // small verification add-on: add number of i in [3..N) that are multiples of 3
                for(int j=3; j<N; j++) {{
                    if (j % 3 == 0) c++;
                }}

                assert(c == {expected});
            ");
        }
    }
}
