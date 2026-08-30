using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    /// <summary>
    /// Unary operators: logical NOT (!), AddressOf (&), and dereference (*).
    /// Covers locals, globals, pointer assignment, function parameters by pointer,
    /// and nested dereferences on arrays.
    /// </summary>
    public class UnaryExpressionTests
    {
        // --- logical NOT ---

        [Fact]
        public async Task LogicalNot_OnLiteralsAndLocals()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool t = true, f = false;
                assert(!f == true);
                assert(!t == false);

                int x = 0, y = 3;
                // if int->bool: 0=false, non-zero=true
                assert(!(bool)x == true);
                assert(!(bool)y == false);
            ");
        }

        // --- address-of & and dereference * ---

        [Fact]
        public async Task AddressOf_And_Deref()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int a = 7;
                int* p = &a;      // take address
                assert(*p == 7);  // read through pointer
            ");
        }


        // --- pointers as parameters ---

        [Fact]
        public async Task PassingPointersToFunctions()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                void check(int* p, int e) {{ assert(*p == e); }}

                int v = 10;
                check(&v, v);
            ");
        }

        [Fact(Skip = "Write to pointer not supported")]
        public async Task PassingPointersToFunctions_ReadWrite()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                void set42(int* p) {{ *p = 42; }}
                void addN(int* p, int n) {{ *p = *p + n; }}

                int v = 10;
                set42(&v);
                assert(v == 42);

                addN(&v, 8);
                assert(v == 50);
            ");
        }

        // --- AddressOf (&) on an array ---

        [Fact]
        public async Task AddressOf_Array()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int numbers[] = {{ 6, 9, 3, 8 }};
                int* p = &numbers;
                p += 2;
                assert(*p == 3);
            ");
        }


        [Fact]
        public async Task AddressOf_ArrayElement()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                int numbers[] = {{ 6, 9, 3, 8 }};
                int* p = &numbers[2];   // points to '3'
                assert(*p == 3);
            ");
        }

        [Fact]
        public async Task AddressOf_ArrayElementChar()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                char letters[] = {{ 'A','B','C','D' }};
                char* cp = &letters[1];   // points to 'B'
                assert(*cp == 'B');
            ");
        }

        [Fact]
        public async Task AddressOf_Element_PassedToFunction()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                void check(int* slot, int e) {{  assert(*slot == e); }}

                int xs[] = {{ 10, 20, 30 }};
                check(&xs[0], 10);
                check(&xs[2], 30);
            ");
        }

        [Fact]
        public async Task Deref_On_AnyExpression_With_StringArray_Indexer()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                string items[] = {{ ""Z"", ""xy"", ""mno"" }};

                // *(*(items + 1)) == items[1][0] == 'x'
                assert(*(*(&items + 1)) == 'x');

                // *(*(items + 2) + 2) == items[2][2] == 'o'
                assert(*(*(&items + 2) + 2) == 'o');
            ");
        }
    }
}
