using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Ensures the compiler's "all code paths return" analysis does not get confused by
    /// 'break' inside switch statements. A 'break' inside switch does not exit the function;
    /// return-path analysis must continue after the switch.
    /// </summary>
    public class ReturnPathAnalysisTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 3)]
        public async Task Switch_WithBreaks_FollowedByReturn_IsValid(int arg, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int x) {{
                    static int s = 0;
                    s = s + x;

                    switch (s) {{
                        case 0:
                            break;
                        default:
                            break;
                    }}

                    return s;
                }}

                return foo({arg});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, 200)]
        [InlineData(42, 999)]
        public async Task Switch_AllCasesReturn_Exhaustive_Default(int x, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int v) {{
                    switch (v) {{
                        case 0:  return 100;
                        case 1:  return 200;
                        default: return 999;
                    }}
                }}
                return foo({x});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }

        [Theory]
        [InlineData(0, 11)]
        [InlineData(1, 22)]
        [InlineData(5, 777)]   // falls through the switch; hits final return
        public async Task Switch_NonExhaustive_WithReturnAfter_IsValid(int x, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int v) {{
                    switch (v) {{
                        case 0: return 11;
                        case 1: return 22;
                        // no default
                    }}
                    return 777;
                }}
                return foo({x});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(3, 8)]
        public async Task If_WithSwitchBreaks_ThenReturnAfter_IsValid(int x, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int v) {{
                    int acc = v + 2;
                    if (v > 1) {{
                        switch (acc) {{
                            case 0: break;
                            default: break;
                        }}
                    }} else {{
                        switch (acc) {{
                            case 0: break;
                            default: break;
                        }}
                    }}
                    return acc + 3;
                }}
                return foo({x});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 3)]
        public async Task Loop_WithSwitchBreakOrContinue_ThenReturnAfter_IsValid(int times, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int n) {{
                    int s=0;
                    for (int i=0; i<n; i++) {{
                        switch (i) {{
                            case 0: continue; // continue loop, not function
                            case 1: s += 1; break;
                            default: s += 2; break;
                        }}
                    }}
                    return s; // still reachable
                }}
                return foo({times});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }
        
        [Fact]
        public async Task MissingReturn_NonExhaustiveSwitch_NoReturnAfter_ShouldFail()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    int bad(int v) {{
                        switch (v) {{
                            case 0: return 1;
                            case 1: return 2;
                            // no default
                        }}
                        // no return here → NOT all paths return
                    }}
                    return 0;
                ");
            });
        }

        [Fact]
        public async Task MissingReturn_IfBranchesWithSwitchBreaks_NoFinalReturn_ShouldFail()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    int bad(int v) {{
                        if (v > 0) {{
                            switch (v) {{
                                case 1: break;
                                default: break;
                            }}
                        }} else {{
                            switch (v) {{
                                case 0: break;
                                default: break;
                            }}
                        }}
                        // no return after either branch
                    }}
                    return 0;
                ");
            });
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, 7)]
        [InlineData(5, 7)]
        public async Task Mixed_ReturnInSomeCases_BreakInOthers_FinalReturnAfter_IsValid(int v, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int x) {{
                    switch (x) {{
                        case 0: return 100;
                        case 2: return 200;
                        default: break; // fall out to final return
                    }}
                    return 7;
                }}
                return foo({v});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 20)]
        [InlineData(2, 30)]
        public async Task NestedSwitches_WithBreaks_ReturnAfter_IsValid(int a, int expected)
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync($@"
                int foo(int v) {{
                    int base = 0;
                    switch (v % 3) {{
                        case 0:
                            switch (v) {{
                                case 0: base = 10; break;
                                default: break;
                            }}
                            break;
                        case 1:
                            switch (v) {{
                                case 1: base = 20; break;
                                default: break;
                            }}
                            break;
                        default:
                            switch (v) {{
                                case 2: base = 30; break;
                                default: break;
                            }}
                            break;
                    }}
                    return base;
                }}
                return foo({a});
            ");
            Assert.Equal((ushort)expected, cpu.AccumulatorRegister);
        }
    }
}
