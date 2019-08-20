using System;
using System.IO;
using Xunit;

namespace Faminet.Emulator.Tests
{
    public class TestRoms
    {
        [Fact]
        public void nestest()
        {
            var stream = File.OpenRead("./Roms/nestest.nes");
            ConsoleSystem console = ConsoleSystem.LoadFromInesStream(stream);
            console.CPU.Jump(0xC000);
            console.Start();
        }
    }
}
