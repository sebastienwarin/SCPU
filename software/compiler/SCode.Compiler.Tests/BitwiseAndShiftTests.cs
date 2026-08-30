using SCode.Compiler.Tests.Support;

namespace SCode.Compiler.Tests
{
    public class BitwiseAndShiftTests
    {
        [Fact]
        public async Task BitwiseNotIs16Bit() => await SCodeRunner.ExecuteCodeAsync($"int r = ~0; assert(r == 0xFFFF);");

        [Fact]
        public async Task AndOrXorNot()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int a=0b1010, b=0b0110;
                assert((a & b)==0b0010);
                assert((a | b)==0b1110);
                assert((a ^ b)==0b1100);
                assert((~a)==0xFFF5);
            ");

        [Fact]
        public async Task ShiftsFromLitteral()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int x=1;
                assert((x << 3)==8);
                assert((0b1000 >> 3)==1);
            ");

        [Fact]
        public async Task ShiftsFromVariable()
            => await SCodeRunner.ExecuteCodeAsync(@"
                int x=1, y=3;
                assert((x << y)==8);
                assert((0b1000 >> y)==1);
            ");
    }
}
