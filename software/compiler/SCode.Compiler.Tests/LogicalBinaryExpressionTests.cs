using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Exhaustive coverage for logical binary expressions (|| and &&):
    /// - short-circuit with side-effect counters,
    /// - literals, locals, globals, function parameters,
    /// - function-return subexpressions,
    /// - grouped / nested expressions and precedence.
    /// </summary>
    public class LogicalBinaryExpressionTests
    {
        // ---------------------------
        // Short-circuit with literals
        // ---------------------------

        [Fact]
        public async Task Or_ShortCircuit_WithLiterals()
        {
            // true || f() → f() must NOT be called
            await SCodeRunner.ExecuteCodeAsync($@"
                int calls = 0;
                bool f() {{ calls++; return false; }}
                assert(true || f());
                assert(calls == 0);
            ");

            // false || t() → t() MUST be called once
            await SCodeRunner.ExecuteCodeAsync($@"
                int calls = 0;
                bool t() {{ calls++; return true; }}
                assert(false || t());
                assert(calls == 1);
            ");
        }

        [Fact]
        public async Task And_ShortCircuit_WithLiterals()
        {
            // false && t() → t() must NOT be called
            await SCodeRunner.ExecuteCodeAsync($@"
                int calls = 0;
                bool t() {{ calls++; return true; }}
                assert(!(false && t()));
                assert(calls == 0);
            ");

            // true && f() → f() MUST be called once
            await SCodeRunner.ExecuteCodeAsync($@"
                int calls = 0;
                bool f() {{ calls++; return false; }}
                assert(!(true && f()));
                assert(calls == 1);
            ");
        }

        // ------------------------
        // Locals as subexpressions
        // ------------------------

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public async Task Or_WithLocalVariables(bool a, bool b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool a = {a.ToString().ToLower()};
                bool b = {b.ToString().ToLower()};
                bool r = a || b;
                assert(r == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public async Task And_WithLocalVariables(bool a, bool b, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool a = {a.ToString().ToLower()};
                bool b = {b.ToString().ToLower()};
                bool r = a && b;
                assert(r == {expected.ToString().ToLower()});
            ");
        }

        // ----------------------------
        // Function parameters in logic
        // ----------------------------

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public async Task And_WithFunctionParameters(bool p, bool q, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool and2(bool a, bool b) {{ return a && b; }}
                assert(and2({p.ToString().ToLower()}, {q.ToString().ToLower()}) == {expected.ToString().ToLower()});
            ");
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public async Task Or_WithFunctionParameters(bool p, bool q, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool or2(bool a, bool b) {{ return a || b; }}
                assert(or2({p.ToString().ToLower()}, {q.ToString().ToLower()}) == {expected.ToString().ToLower()});
            ");
        }

        // -------------------------
        // Globals in logic contexts
        // -------------------------

        [Fact]
        public async Task Logic_WithGlobalsAndSideEffects()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool G1 = false;
                int hits = 0;

                bool T() {{ hits++; return true; }}
                bool F() {{ hits++; return false; }}

                bool changeG1() {{ G1 = true; return true; }}

                // 1) true || changeG1() → RHS not evaluated
                assert(true || changeG1());
                assert(G1 == false);

                // 2) false || changeG1() → RHS evaluated
                assert(false || changeG1());
                assert(G1 == true);

                // 3) true && F() → RHS evaluated
                int before = hits;
                assert(!(true && F()));
                assert(hits == before + 1);

                // 4) false && T() → RHS not evaluated
                before = hits;
                assert(!(false && T()));
                assert(hits == before);
            ");
        }

        // -----------------------
        // Function-return nesting
        // -----------------------

        [Fact]
        public async Task Nested_FunctionCalls_InLogicalExpressions()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int aCalls=0, bCalls=0, cCalls=0, dCalls=0;

                bool A() {{ aCalls++; return false; }}
                bool B() {{ bCalls++; return true; }}
                bool C() {{ cCalls++; return false; }}
                bool D() {{ dCalls++; return true; }}

                bool r = (A() || B()) && (C() || D());

                assert(r == true);
                assert(aCalls==1 && bCalls==1 && cCalls==1 && dCalls==1);
            ");
        }

        // -------------------------------------
        // Grouping / precedence and evaluation
        // -------------------------------------

        [Fact]
        public async Task GroupingAndPrecedence_AreRespected()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool a=false, b=true, c=false;
                bool r1 = a || b && c;
                bool r2 = a || (b && c);
                assert(r1 == false && r2 == false);

                a=false; b=true; c=true;
                r1 = a || b && c;
                r2 = a || (b && c);
                assert(r1 == true && r2 == true);

                a=false; b=true; c=false;
                bool r3 = (a || b) && c;
                assert(r3 == false);

                a=false; b=true; c=true;
                r3 = (a || b) && c;
                assert(r3 == true);
            ");
        }

        // ------------------------------------------------
        // Mixed: locals + params + literals + short-circuit
        // ------------------------------------------------

        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(true, false, true, true)]
        [InlineData(false, false, true, false)]
        public async Task Mixed_Locals_Params_Literals_ShortCircuit(bool x, bool p, bool lit, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool x = {x.ToString().ToLower()};
                bool fn(bool v) {{ return v; }}
                bool r = (x || fn({p.ToString().ToLower()})) && {lit.ToString().ToLower()};
                assert(r == {expected.ToString().ToLower()});
            ");
        }

        // -------------------------------------------------
        // Ensure only needed side-effects happen (&& / || )
        // -------------------------------------------------

        [Fact]
        public async Task SideEffects_OnlyInEvaluatedBranches()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a=0,b=0,c=0,d=0;

                bool incA(){{ a++; return true; }}
                bool incB(){{ b++; return false; }}
                bool incC(){{ c++; return true; }}
                bool incD(){{ d++; return false; }}

                assert(true || incA());  assert(a==0);
                assert(false || incA()); assert(a==1);

                assert(!(true && incB()));  assert(b==1);
                int beforeB=b;
                assert(!(false && incB())); assert(b==beforeB);

                bool r = (incC() || incD()) && (incD() || incC());
                assert(r==true);
                assert(a==1 && b==1 && c==2 && d==1);
            ");
        }

        // -------------------------------------------------
        // Deep nesting with parameters and globals mixed
        // -------------------------------------------------

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public async Task Deep_Nesting_With_AllKinds(bool gx, bool param, bool expected)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool GX = {gx.ToString().ToLower()};
                int hitA=0, hitB=0, hitC=0;

                bool A(){{ hitA++; return GX; }}
                bool B(bool p){{ hitB++; return p; }}
                bool C(){{ hitC++; return true; }}

                bool r = (A() || B({param.ToString().ToLower()})) && (C() || false);
                assert(r == {expected.ToString().ToLower()});

                if ({gx.ToString().ToLower()}) {{
                    assert(hitA==1 && hitB==0 && hitC==1);
                }} else if ({param.ToString().ToLower()}) {{
                    assert(hitA==1 && hitB==1 && hitC==1);
                }} else {{
                    assert(hitA==1 && hitB==1 && hitC==0);
                }}
            ");
        }
    }
}
