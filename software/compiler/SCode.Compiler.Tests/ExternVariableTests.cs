using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;
using SCPU.Architecture;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Tests for 'extern' variables (symbols defined outside the S-Code program and resolved
    /// by the assembler) and for the built-in heap layout symbols __heap_start / __heap_end.
    /// </summary>
    public class ExternVariableTests
    {
        [Fact]
        public async Task Extern_Variable_ResolvesAssemblerSymbol()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                extern int R0;
                assert(&R0 == 0x2700);
            ");
        }

        [Fact]
        public async Task Extern_Variable_ReservesNoMemory()
        {
            var assembly = await SCodeRunner.CompileToAssemblyAsync($@"
                extern int R0;
                int a = 1;
            ");

            Assert.DoesNotContain("R0: #res", assembly);
            Assert.Contains("a: #res 1", assembly);
        }

        [Fact]
        public async Task Extern_Variable_CanBeReadAndWrittenThroughPointer()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                extern int R9;
                int* p = &R9;
                *p = 1234;
                assert(R9 == 1234);
            ");
        }

        [Fact]
        public async Task Extern_Variable_CanBeDeclaredTwiceWithTheSameType()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                extern int R0;
                extern int R0;
                assert(&R0 == 0x2700);
            ");
        }

        [Fact]
        public async Task Extern_Variable_CannotBeRedeclaredWithAnotherType()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    extern int R0;
                    extern int* R0;
                ");
            });
        }

        [Fact]
        public async Task Extern_Variable_WithInitializer_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    extern int R0 = 1;
                ");
            });
        }

        [Fact]
        public async Task Extern_Variable_AsConst_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    extern const int R0;
                ");
            });
        }

        [Fact]
        public async Task Extern_Variable_InsideFunction_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    void foo() {{
                        extern int R0;
                    }}
                    foo();
                ");
            });
        }

        [Fact]
        public async Task Extern_Variable_AsArray_IsForbidden()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    extern int R0[4];
                ");
            });
        }

        // ---------------------------
        // Built-in heap symbols
        // ---------------------------

        [Fact]
        public async Task HeapStart_IsAfterAllStaticAllocations()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = 1;
                int b[4];
                int* heapStart = &__heap_start;
                assert(heapStart > &b);
                assert(heapStart >= &b + 4);
            ");
        }

        [Fact]
        public async Task HeapStart_IsTheLastUserPageReservation()
        {
            var assembly = await SCodeRunner.CompileToAssemblyAsync($@"
                int a = 1;
                int f() {{
                    static int s = 0;
                    s = s + 1;
                    return s;
                }}
                int r = f();
            ");

            var reservations = assembly
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Contains("#res"))
                .ToList();

            Assert.Equal($"{Ast.Program.HeapStartSymbol}: #res 1", reservations.Last());
        }

        [Fact]
        public async Task HeapEnd_IsTheStartOfTheReservedPage()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int* heapEnd = &__heap_end;
                assert(heapEnd == 0x2700);
            ");
        }

        [Fact]
        public async Task Heap_AreaIsUsable()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int* p = &__heap_start;
                *p = 0xAA55;
                *(p + 1) = 0x55AA;
                assert(*p == 0xAA55);
                assert(*(p + 1) == 0x55AA);
                assert(&__heap_end > p);
            ");
        }

        [Fact]
        public async Task Heap_SymbolsCannotBeRedeclared()
        {
            await Assert.ThrowsAsync<NodeCompilerException>(async () =>
            {
                await SCodeRunner.ExecuteCodeAsync($@"
                    int __heap_start = 0;
                ");
            });
        }
    }
}
