using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Arrays: 1D const/non-const, 2D non-const, read/write, index by literals/variables/expressions,
    /// and small loops to validate contents.
    /// </summary>
    public class ArrayTests
    {
        // --- 1D const LUT + read ---

        [Fact]
        public async Task Const1D_LUT_Read()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                const int smallLut[] = {{ 1, 2, 3 }};
                assert(smallLut[0] == 1);
                assert(smallLut[1] == 2);
                assert(smallLut[2] == 3);
            ");
        }

        // --- 1D non-const init + read/write + index by variable ---

        [Fact]
        public async Task OneD_ReadWrite_WithVariableIndex()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int numbers[] = {{ 6, 9, 3, 8, 0, 4, 2, 5, 7, 1 }};
                int idx = 2;
                assert(numbers[idx] == 3);
                numbers[idx] = 123;
                assert(numbers[2] == 123);

                // expression index
                numbers[1 + 3] = 777; // index 4
                assert(numbers[4] == 777);
            ");
        }

        // --- 2D arrays: init (non-const), read, write with literal & variable indices ---

        [Fact]
        public async Task TwoD_Init_Read_Write()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int matrix[,] = {{
                  {{ 4, 5, 0 }},
                  {{ 4, 5, 1 }},
                  {{ 4, 5, 1 }}
                }};

                // Read literals
                assert(matrix[0, 0] == 4);
                assert(matrix[0, 1] == 5);
                assert(matrix[1, 2] == 1);

                // Write with literal indices
                matrix[2, 1] = 456;
                assert(matrix[2, 1] == 456);

                int idx = 2;
                matrix[1, idx] = 789;  // variable index
                assert(matrix[1, 2] == 789);
            ");
        }

        // --- iterate over arrays (for-loops) ---

        [Fact]
        public async Task OneD_Iteration_Sum()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int xs[] = {{ 1, 2, 3, 4, 5 }};
                int sum = 0;
                for(int i=0;i<5;i++) {{ sum += xs[i]; }}
                assert(sum == 15);
            ");
        }

        [Fact]
        public async Task TwoD_Iteration_Sum()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int m[,] = {{
                  {{1, 2, 3}},
                  {{4, 5, 6}}
                }};
                int s = 0;
                for(int r=0;r<2;r++) {{
                    for(int c=0;c<3;c++) {{
                        s += m[r,c];
                    }}
                }}
                assert(s == (1+2+3+4+5+6));
            ");
        }

        // --- address of element & pointer write back ---

        [Fact]
        public async Task OneD_AddressOfElement_WriteBack()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int xs[] = {{ 10, 20, 30 }};
                int* p = &xs;
                int index = 2;
                p = p + index; // *p == 30
                assert(*p == 30);
                assert(xs[index] == 30);
            ");
        }

        [Fact]
        public async Task Local1D_ReadWrite_AndLoopSum()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int foo() {{
                    static int xs[] = {{ 1, 2, 3, 4, 5 }};  // static mandatory
                    int idx = 2;
                    xs[idx] = 10;             // xs = [1,2,10,4,5]
                    int sum = 0;
                    for (int i=0;i<5;i++) {{ sum += xs[i]; }}
                    assert(sum == (1+2+10+4+5)); // 22
                    return sum;
                }}
                assert(foo() == 22);
            ");
        }

        [Fact]
        public async Task Pass1DArray_ByParam_MutateFromCallee()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                void test(int* arr, int index, int expected) {{
                    int* p = arr + index;
                    assert(*p == expected);
                }}

                int run() {{
                    static int xs[] = {{ 6, 9, 3, 8, 0 }};
                    test(&xs, 2, 3);
                    assert(xs[2] == 3);
                    return xs[2];
                }}
                
                assert(run() == 3);
            ");
        }

        [Fact]
        public async Task PassArrayByPointer_AndUsePointerArithmetic()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int demo() {{
                    static int xs[] = {{ 10, 20, 30, 40 }};
                    int* p = &xs;
                    p++;                // p = 1
                    assert(xs[1] == *p);

                    int* q = p + 2;     // q = 3
                    assert(xs[3] == *q);

                    // simple sum to ensure values are intact
                    int sum = 0;
                    for (int i=0;i<sizeof(xs);i++) {{ sum += xs[i]; }}
                    return sum; // 10 + 20 + 30 + 40 = 100
                }}

                assert(demo() == 100);
            ");
        }
    }
}
