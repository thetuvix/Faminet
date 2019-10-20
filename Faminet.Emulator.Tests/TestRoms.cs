using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Faminet.Emulator.Tests
{
    public class TestRoms
    {
        private readonly ITestOutputHelper output;

        public TestRoms(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData(@"registers.nes")]
        public void cpu_reset(string subtest)
        {
            blarggTest($@"cpu_reset\{subtest}");
        }

        [Theory]
        [InlineData(@"01-implied.nes")]
        public void instr_test(string subtest)
        {
            blarggTest($@"nes_instr_test\rom_singles\{subtest}");
        }

        private void blarggTest(string testPath)
        {
            var stream = File.OpenRead($@".\nes-test-roms\{testPath}");
            ConsoleSystem console = ConsoleSystem.LoadFromInesStream(stream);
            console.LogWriteLineAction = output.WriteLine;

            // Run until test completes:
            var continueStatuses = new byte[] { 0x00, 0x80 };
            console.ShouldHalt = () =>
                (console.Memory.Peek(0x6001) != 0x00) &&                    // test started?
                !continueStatuses.Contains(console.Memory.Peek(0x6000));    // test not finished?
            console.Run();

            // Ensure that all tests passed:
            output.WriteLine($"$6000: {console.Memory.Peek(0x6000):X2}");
            output.WriteLine($"$6004: {console.Memory.PeekString(0x6004)}");
            Assert.Equal(0, console.Memory.Peek(0x6000));
        }

        [Fact]
        public void nestest()
        {
            var stream = File.OpenRead(@".\nes-test-roms\other\nestest.nes");
            ConsoleSystem console = ConsoleSystem.LoadFromInesStream(stream);
            console.LogWriteLineAction = output.WriteLine;

            // Jump to "automation mode" address:
            console.CPU.Jump(0xC000);

            // Run until CPU halts:
            console.Run();

            // Ensure that all tests passed:
            output.WriteLine($"$02: {console.Memory.Peek(0x02):X2} $03: {console.Memory.Peek(0x03):X2}");
            Assert.Equal(0, console.Memory.Peek(0x02));
            Assert.Equal(0, console.Memory.Peek(0x03));
        }
    }

    public static class TestHelpers
    {
        public static string PeekString(this CPUMemoryMap mem, ushort addr)
        {
            var builder = new StringBuilder();

            char ch;
            while ((ch = (char)mem.Peek(addr++)) != 0)
            {
                builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
