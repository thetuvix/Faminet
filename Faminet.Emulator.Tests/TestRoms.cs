using System;
using System.IO;
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

        [Fact]
        public void nestest()
        {
            var stream = File.OpenRead("./Roms/nestest.nes");
            ConsoleSystem console = ConsoleSystem.LoadFromInesStream(stream);

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
}
