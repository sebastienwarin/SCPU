using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class FunctionTests
    {
        [Fact]
        public async Task ParamsReturnAndScope()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int g=7;
                int add(int a,int b){ int g=1; return a+b+g; } // local shadows global
                int r = add(2,3);
                assert(r==6 && g==7);
            ");

        [Fact]
        public async Task Shadowing()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int x=1;
                void foo(){ int x=2; assert(x==2); }
                foo();
                assert(x==1);
            ");

        [Fact]
        public async Task Equality_FunctionOnLeft_ComplexRhs_ConstStatic_LocalStatic()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int K = 7;
                static int S = 2;

                int run(int x) {{
                    static int acc = 0;   // persists only inside run()
                    acc += x;
                    return (acc * S) + K;
                }}

                assert( run(1) == (1*2 + 7) );   // 2*1+7 = 9
            ");
        }

        [Fact]
        public async Task Equality_FunctionOnRight_ComplexLhs_ConstStatic_LocalStatic()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int K = 7;
                static int S = 2;

                int run(int x) {{
                    static int acc = 0;
                    acc += x;
                    return (acc * S) + K;
                }}

                // Flip sides: LHS now complex; RHS is function call
                assert( ((1+0) * (S)) + K == run(1) ); // 1*2+7 vs run(1)=9
            ");
        }

        [Fact]
        public async Task Inequality_FunctionAndExpression_BothSidesUseTemps()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int K = 3;
                static int S = 5;

                int run(int x) {{
                    static int acc = 0;
                    acc += x;                 // run(2) returns (2*5)+3 = 13
                    return (acc * S) + K;
                }}

                // LHS and RHS both produce temporaries; ensure no clobbering.
                bool ok = (run(2) != ((1+2)*(S-2) + (K-1))); // 13 != (3 * 3 + 2)=11 → true
                assert(ok == true);
            ");
        }

        [Fact]
        public async Task LessThan_WithFunctionOnOneSide_AndNestedTempsOnTheOther()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                static int S = 4;
                const int K = 10;

                int g(int a, int b) {{ return a + b; }}

                int run(int x) {{
                    static int acc = 0;
                    acc += x; // run(3) = (3*4)+10 = 22
                    return (acc * S) + K;
                }}

                // RHS builds multiple temps: ( (1+2)*(3+4) - (5-6) ) = (3*7 - (-1)) = 21 + 1 = 22
                int rhs = (g(1,2) * g(3,4)) - (5 - 6);
                assert( run(3) <  (rhs + 1) ); // 22 < 23 → true
                assert( run(3) > (rhs + 1) ); // 34 > 23
            ");
        }

        [Fact]
        public async Task ComplexArithmeticWithTwoCalls_MultipleLiveTemporaries()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int K = 1;
                static int M = 3;

                int f(int x) {{
                    static int a = 0; a += x; return a;   // f(2)=2, then f(3)=5
                }}
                int g(int y) {{
                    static int b = 10; b += y; return b;  // g(4)=14, then g(1)=15
                }}

                // Expression: f(2) + (3*M) - (g(4) + (5-2))
                // Evaluate expected manually: f(2)=2; g(4)=14; 3*M = 9; (5-2)=3;
                // LHS = 2 + 9 - (14 + 3) = 11 - 17 = -6  (wraps to 16-bit if signed/unsigned; we compare via equality with a recomputed path)
                int lhs = f(2) + (3*M) - (g(4) + (5-2));

                // Recompute the same value without additional calls:
                int vv = 2 + 9 - (14 + 3); // -6
                assert(lhs == vv);

                // Another compound expression including a call on RHS; ensure nothing is clobbered:
                // (f(3) * 2 + K) == ((g(1) - 12) + (M + K))
                // f(3)=5; LHS = 5*2 + 1 = 11
                // g(1)=15; RHS = (15-12) + (3+1) = 3 + 4 = 7  → should be false
                assert( (f(3) * 2 + K) == ((g(1) - 12) + (M + K)) ? false : true );
                // Above trick asserts the expression is false; if equality were true, assertion would fail.
            ");
        }

        [Fact]
        public async Task Relational_Chained_Binary_WithCalls_BothOrders()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                static int S = 2;
                const int K = 5;

                int h(int d) {{
                    static int t=0; t+=d; return t;  // h(1)=1, h(2)=3, ...
                }}

                // Order A: call on left, complex RHS
                bool a = (h(1) >= ((S*3) + (K-1))); // 1 >= (6 + 4) → 1 >= 10 → false
                assert(a == false);

                // Order B: complex LHS, call on right; note h(2)=3
                bool b = (((S+S) * (K-2)) <= h(2)); // (4 * 3)=12 <= 3 → false
                assert(b == false);
            ");
        }

        [Fact]
        public async Task NestedEquality_UsesStoredTemps_NotRecomputedCalls()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                // Ensure temporaries are not accidentally re-used or overwritten,
                // and that the compiler doesn't re-invoke function calls when not needed.

                int callsF=0, callsG=0;

                int F(int v) {{ callsF++; return v + 10; }} // pure, no global effects
                int G(int v) {{ callsG++; return v * 2;   }}

                // Store a temp, compare to expression with another call
                int t = F(5);                // 15
                bool ok = (t == (G(5) + 5)); // G(5)=10, 10+5=15 → true
                assert(ok == true);

                // Calls should be exactly F once, G once
                assert(callsF == 1 && callsG == 1);
            ");
        }

        [Fact]
        public async Task DoubleCallVsSingleTemp_ProvesSideEffectsMatter()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                // This test documents the semantic difference:
                // run(1) == run(1) is NOT the same as int t = run(1); t == t; 
                // The first performs two calls with side effects (acc grows); the second stores a temp.

                static int S = 2;
                const int K = 7;

                int run(int x) {{
                    static int acc = 0; acc += x; return (acc * S) + K;
                }}

                // Two calls: run(1)=9, then run(1)=11 → false
                assert( !(run(1) == run(1)) );

                // Single call + temp: always true
                int t = run(0);   // acc unchanged other than this call; run(0)=current acc * 2 + 7
                assert( t == t );
            ");
        }

        [Fact]
        public async Task MixedComparison_WithCalls_NestedGrouping()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int cntF=0, cntH=0;

                int F(int a, int b) {{ cntF++; return a + b; }} // pure
                int H(int x) {{ cntH++; static int s=0; s+=x; return s; }} // side effect

                // LHS uses nested temps (F inside arithmetic)
                int lhs = (F(1,2) * F(3,4)) - (F(5,6) - 1); // (3*7) - (11 - 1) = 21 - 10 = 11

                // RHS uses a call with side effect
                bool okay = lhs == (H(11)); // H(11) returns 11 on first call
                assert(okay == true);

                // Double-check counts: F called 3 times, H called once
                assert(cntF == 3 && cntH == 1);
            ");
        }
    }
}
