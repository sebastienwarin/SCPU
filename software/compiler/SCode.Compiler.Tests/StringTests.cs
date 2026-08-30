using SCode.Compiler.Exceptions;
using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class StringTests
    {
        private const string StandardStringLib = @"
            int strlen(string str) {
                char* pointer = str; int base = pointer;
                while (*pointer != '\0') { ++pointer; }
                return pointer - base;
            }
            bool strcmp(string str, string str2) {
                char* pointer = str; char* pointer2 = str2;
                while (*pointer == *(pointer2++)) {
                    if(*(pointer++) == '\0') return true;
                }
                return false;
            }
        ";

        [Theory]
        [InlineData("string")]
        [InlineData("const string")]
        public async Task TestStringAccessor(string type)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {type} prompt = ""Hello S-CPU !!"";
                int n = 0;
                assert(prompt[n] == 72);
                assert(prompt[0] == 'H');
                assert(prompt[n+4] == 'o');
            ");
        }

        [Theory]
        [InlineData("string")]
        [InlineData("const string")]
        public async Task TestStringInFunctionParameter(string type)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}

                {type} prompt = ""Hello S-CPU !!"";
                string test2 = ""Another string"";
                
                assert(strlen(prompt) == 14);
                assert(sizeof(prompt) == 1);
            ");
        }

        [Theory]
        [InlineData("Hello S-CPU !!")]
        [InlineData("Hello")]
        [InlineData("Hi")]
        [InlineData("H")]
        [InlineData("")]
        public async Task TestStringInMultipleFunctionParameter(string text)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}
                const string prompt = ""{text}"";
                void checkLength(string str, int expected) {{
                  int len = strlen(str);
                  assert(len == expected);
                }}
                checkLength(prompt, {text.Length}); // sizeof include null terminaison
            ");
        }

        [Fact]
        public async Task TestStringCompare()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}
                string test = ""Hello""; int a = 123;
                const string test2 = ""Hello""; int b = 456;
                string test3 = ""World"";

                // Pointer (aliasing) semantics: t0/t1 point to the same addresses as text[0]/text[1]
                assert(strcmp(test, test2) == true);
                assert(strcmp(test, test3) == false);

                // Inspect a few characters via pointer & array accessor
                assert(*test == 'H');
                assert(test[0] == 'H');
                assert(test2[0] == 'H');
                assert(test[4] == 'o');
                assert(test[4] == test2[4]);
            ");
        }

        [Fact]
        public async Task StringArray_Sizeof_And_PointerSemantics()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}
                // Any of these are equivalent:
                // char* text[] = {{ ""Hello"", ""S-CPU"" }};
                string text[] = {{ ""Hello"", ""S-CPU"" }};
                string hello = ""Hello"";

                // sizeof(text) returns the element count (not bytes)
                assert(sizeof(text) == 2);

                // Indexing returns a pointer to the string data
                string t0 = text[0];
                string t1 = text[1];
                assert(sizeof(t0) == 1);

                // Pointer (aliasing) semantics: t0/t1 point to the same addresses as text[0]/text[1]
                assert(strcmp(t0, text[0]) == true);
                assert(strcmp(t1, text[1]) == true);
                assert(strcmp(t0, t1) == false);

                // Inspect a few characters via pointer arithmetic/deref
                assert(*t0 == 'H');

                assert(t0[0] == 'H');
                assert(t0[2] == 'l');

                int a = 2;
                assert(t0[a++] == 'l');
                assert(t0[a++] == 'l');
                assert(t0[a] == 'o');
                assert(t0[1] == 'e');

                char* c = t0;
                assert(*c == 'H');
                assert(*(c+2) == 'l');
            ");
        }

        // --- Mixing declarations: char*[] vs string[] vs const variants behave equivalently for reading ---

        [Theory]
        [InlineData("string")]
        [InlineData("const string")]
        public async Task StringArray_DeclarationForms_AreInterchangeable_ForReadAccess(string arrayTest)
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {arrayTest} a[] = {{ ""Hi"", ""There"" }};

                // Element count
                assert(sizeof(a) == 2);

                // Content
                string p = a[0];
                assert(*p == 'H');
                assert(p[1] == 'i');
                assert(*(p+1) == 'i');

                p = a[1];
                assert(*p == 'T');
                assert(p[1] == 'h');
                assert(*(p+1) == 'h');
                assert(*(p+2) == p[2]);
            ");
        }

        [Fact]
        public async Task StringArray_PassElement_AsCharPtr_FunctionReadsContent2()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool startsWithS(char* s) {{ return (*s == 'S'); }}

                string texts[] = {{ ""Hello"", ""S-CPU"" }};
                assert(startsWithS((char*)texts[1]) == true);   // Cast explicit
                assert(startsWithS(texts[1]) == true);          // Cast implicit
                assert(startsWithS(texts[0]) == false);         // Cast implicit
            ");
        }


        [Fact]
        public async Task StringArray_PassElement_AsCharPtr_FunctionReadsContent3()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                bool startsWithS(string s) {{ return (*s == 'S'); }}

                string texts[] = {{ ""Hello"", ""S-CPU"" }};
                assert(startsWithS(texts[1]) == true);
                assert(startsWithS(texts[0]) == false);
            ");
        }

        // --- Using sizeof in a loop over a string array; element is a pointer copy (no deep copy) ---

        [Fact]
        public async Task StringArray_Loop_UsingSizeof_ElementIsPointerCopy()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                string items[] = {{ ""A"", ""BC"", ""DEF"" }};
                int count = 0;

                assert(**(&items) == 'A');
                assert(**(&items + 1) == 'B');
                assert(*(*(&items + 1) + 1) == 'C');

                for (int i = 0; i < sizeof(items); i++)
                {{
                    string s = items[i];     // pointer copy

                    // Check first char per element
                    if (i == 0) assert(*s == 'A');
                    if (i == 1) assert(*s == 'B');
                    if (i == 2) assert(*s == 'D');
                    count++;
                }}

                assert(count == 3);
            ");
        }

        [Fact]
        public async Task AllocateString()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}
                char buf[16];             // buffer
                string s = &buf;

                s[0] = 'H';
                s[1] = 'i';
                
                assert(strcmp(s, ""Hi""));
                assert(buf[0] == 'H');
                assert(buf[1] == 'i');

                s = ""Seb"";
                assert(strcmp(s, ""Seb""));

            ");
        }

        [Fact]
        public async Task String_DerefOnAnyExpression_ViaAddressOf()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                string t = ""Hi"";
                // *(&t) = t (pointer) ; **(&t) = *t = first char
                assert(**(&t) == 'H');
                assert(*(*(&t) + 1) == 'i');
            ");
        }

        [Fact]
        public async Task StringArray_DerefOnAnyExpression_WithAddressOfAndPointerArithmetic()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                string items[] = {{ ""A"", ""BC"", ""DEF"" }};

                assert(**(&items) == 'A');                 // items[0][0]
                assert(**(&items + 1) == 'B');             // items[1][0]
                assert(*(*(&items + 1) + 1) == 'C');       // items[1][1]
                assert(*(*(&items + 2) + 2) == 'F');       // items[2][2]
            ");
        }

        [Fact]
        public async Task String_ImplicitCast_Between_String_And_CharPtr_BothWays()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}

                string s = ""OK"";
                char* p = s;                 // string -> char* (implicit)
                assert(*p == 'O');

                string s2 = p;               // char* -> string (implicit)
                assert(strcmp(s2, ""OK"") == true);

                char* p2 = ""YO"";           // litteral: char* impl.
                string s3 = p2;              // char* -> string (implicit)
                assert(strcmp(s3, ""YO"") == true);
            ");
        }

        [Fact]
        public async Task String_PointerArithmetic_WithImplicitCasts()
        {
            await SCodeRunner.ExecuteCodeAsync($@"
                {StandardStringLib}

                string s = ""ABC"";
                char* p = s;          // implicit
                assert(*p == 'A');
                p = p + 1;
                assert(*p == 'B');

                string s2 = p;        // implicit (char* -> string)
                assert(strcmp(s2, ""BC"") == true);

                // *(&s) == s (pointer); **(&s) == *s == 'A'
                assert(**(&s) == 'A');
            ");
        }
    }
}
