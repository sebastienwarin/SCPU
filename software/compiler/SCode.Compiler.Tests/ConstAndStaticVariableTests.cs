using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Tests for S-Code 'const' (compile-time initialized, runtime readable, non-assignable)
    /// and 'static' variables (single storage, lifetime = program; local statics persist across calls).
    /// </summary>
    public class ConstAndStaticVariableTests
    {
        // ---------------------------
        // CONST: initialization + read
        // ---------------------------

        [Fact]
        public async Task Const_Global_BasicUsage()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int A = 42;
                const bool FLAG = true;
                int r = A + (FLAG ? 1 : 0);
                assert(r == 43);
            ");
        }

        [Fact]
        public async Task Const_Local_InFunction_And_Expressions()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int f(int x) {{
                    const int K = 10;
                    return x + K;
                }}
                assert(f(32) == 42);
                assert(f(0)  == 10);
            ");
        }

        [Fact]
        public async Task Const_UsedInArrayDims_AndIndices()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int N = 3;
                int xs[] = {{ 10, 20, 30 }};
                assert(xs[N-1] == 30);
            ");
        }

        // CONST: negative — assignments must fail at compile-time

        [Fact]
        public async Task Const_Global_Assignment_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    const int A = 1;
                    A = 2;           // ❌ should be a compile-time error
                ");
            });
        }

        [Fact]
        public async Task Const_Local_Assignment_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    void foo() {{
                        const int K = 7;
                        int z = 0;
                        z = K;        // ok (read)
                        K = 8;        // ❌ not allowed
                    }}
                    foo();
                ");
            });
        }

        // ---------------------------
        // STATIC (global): single storage, program lifetime
        // ---------------------------

        [Fact]
        public async Task Static_Global_Persists_And_Mutable()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                static int G = 5;
                void inc() {{ G++; }}
                assert(G == 5);
                inc();
                assert(G == 6);
                inc();
                assert(G == 7);
            ");
        }

        // ---------------------------
        // STATIC (local): persists across calls, initialized once
        // ---------------------------

        [Fact]
        public async Task Static_Local_PersistsAcrossCalls_InitializedOnce()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int x) {{
                    static int s = 0;
                    s = s + x;       // accumulates across calls
                    return s;
                }}

                assert(foo(1) == 1);  // s: 0 -> 1
                assert(foo(2) == 3);  // s: 1 -> 3
                assert(foo(5) == 8);  // s: 3 -> 8
            ");
        }

        [Fact]
        public async Task Static_Local_InitDependsOnExpression_EvaluatedOnce()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int calls=0;
                int seed() {{ calls++; return 10; }}

                int f(int x) {{
                    static int base = seed();   // seed() must run exactly once
                    return base + x;
                }}

                assert(f(1) == 11);
                assert(f(2) == 12);
                assert(f(31) == 41);
                assert(calls == 1);
            ");
        }

        // ---------------------------
        // STATIC arrays: mutation persists
        // ---------------------------

        [Fact]
        public async Task Static_Array_Global_MutationPersists()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                static int buf[] = {{ 1, 2, 3 }};
                void bump() {{
                    buf[1] += 10;
                }}

                assert(buf[1] == 2);
                bump();
                assert(buf[1] == 12);
                bump();
                assert(buf[1] == 22);
            ");
        }

        [Fact]
        public async Task Static_Array_Local_MutationPersistsBetweenCalls()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int pushAndSum(int v) {{
                    static int q[] = {{ 0, 0, 0 }};
                    q[0] = q[1];
                    q[1] = q[2];
                    q[2] = v;
                    return q[0] + q[1] + q[2];
                }}

                assert(pushAndSum(5)  == 5);     // [0,0,5]
                assert(pushAndSum(10) == 15);    // [0,5,10]
                assert(pushAndSum(20) == 35);    // [5,10,20]
            ");
        }

        // ---------------------------
        // Shadowing / scope sanity
        // ---------------------------

        [Fact]
        public async Task Local_DoesNotLeak_And_CanShadowGlobal()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int X = 100;   // global (non-static)
                int foo() {{
                    int X = 1; // non-static local shadows the global within this function
                    X += 2;    // local X is recreated each call → always 3
                    return X;
                }}
                int bar() {{
                    // global X is independent
                    return X;
                }}

                assert(foo() == 3);   // fresh local each call
                assert(foo() == 3);   // still 3 (no persistence)
                assert(bar() == 100); // global unchanged
            ");
        }

        [Fact]
        public async Task Static_Local_DoesNotLeak_And_CanShadowGlobal()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                static int X = 100;   // global static
                int foo() {{
                    static int X = 1; // static local shadows the global within this function
                    X += 2;           // local static X persists: 1->3, then 3->5...
                    return X;
                }}
                int bar() {{
                    // global X is independent
                    return X;
                }}

                assert(foo() == 3);   // local static X: 1 -> 3
                assert(foo() == 5);   // local static X: 3 -> 5 (persists)
                assert(bar() == 100); // global static still 100
            ");
        }

        [Fact]
        public async Task Static_Locals_SameName_InDifferentFunctions_AreIndependent()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int v) {{
                    static int counter = 0;
                    counter += v;
                    return counter;
                }}

                int bar(int v) {{
                    static int counter = 100;
                    counter += v;
                    return counter;
                }}

                // foo() and bar() each maintain their own 'counter'
                assert(foo(1) == 1);     // foo.counter = 1
                assert(foo(2) == 3);     // foo.counter = 3
                assert(bar(1) == 101);   // bar.counter = 101
                assert(bar(2) == 103);   // bar.counter = 103

                // Confirm they don't interfere
                assert(foo(3) == 6);     // foo.counter continues independently
                assert(bar(3) == 106);   // bar.counter continues independently
            ");
        }

        [Fact]
        public async Task Static_Local_InitSideEffect_RunsOnce_PerFunction()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int seedFooCalls = 0;
                int seedBarCalls = 0;

                int seedFoo() {{ seedFooCalls++; return 10; }}
                int seedBar() {{ seedBarCalls++; return 100; }}

                int foo(int x) {{
                    static int base = seedFoo();   // must run once overall (first foo() call)
                    return base + x;
                }}

                int bar(int x) {{
                    static int base = seedBar();   // must run once overall (first bar() call)
                    return base + x;
                }}

                assert(foo(1) == 11);
                assert(foo(2) == 12);
                assert(foo(31) == 41);

                assert(bar(1) == 101);
                assert(bar(2) == 102);

                assert(seedFooCalls == 1);
                assert(seedBarCalls == 1);
            ");
        }

        // ---------------------------
        // Mixing const/static/local in expressions
        // ---------------------------

        [Fact]
        public async Task Mixed_Const_Static_Local_Expressions()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int K = 7;
                static int S = 2;

                int run(int x) {{
                    static int acc = 0;
                    acc += x;
                    return (acc * S) + K; // K const, S global static, acc local static
                }}
                
                assert(run(1) == (1*2 + 7));   // 9
                assert(run(2) == (3*2 + 7));   // 13
                assert(run(5) == (8*2 + 7));   // 23
            ");
        }

        // ---------------------------
        // Negative: static redeclare conflicting types (if language forbids)
        // ---------------------------

        [Fact]
        public async Task Static_RedeclareWithDifferentType_ShouldFail()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    static int X = 0;
                    static bool X = true;   // ❌ same name, different type
                ");
            });
        }
    }
}
