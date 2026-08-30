using SCode.Compiler.Tests.Support;
using SCPU.Architecture;

namespace SCode.Compiler.Tests
{
    public class InlineAsmTests
    {
        [Fact]
        public async Task InlineAsmCanWriteMemory()
        {
            var cpu = await SCodeRunner.ExecuteCodeAsync(@"
                // Write 0x1234 at user page start using inline asm
                asm(""LDA #0x1234"");
                asm(""STA 0x12000"");
                assert(1);
            ");
            Assert.Equal(0x1234, cpu.LookupValue(MemoryMap.Ram.Start));
        }
    }
}
