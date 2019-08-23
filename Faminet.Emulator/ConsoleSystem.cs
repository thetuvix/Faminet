using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Faminet.Emulator
{
    public class ConsoleSystem
    {
        public CPUMemoryMap Memory { get; private set; }
        public CPU CPU { get; private set; }

        public ConsoleSystem(int mapper, byte[] prgRom)
        {
            Memory = new CPUMemoryMap(mapper, prgRom);
            CPU = new CPU(Memory);
        }

        public void Run()
        {
            bool halt;
            do
            {
                halt = CPU.Step();
            } while (!halt);
            Debug.WriteLine("Halting.");
        }

        public static ConsoleSystem LoadFromInesStream(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadBytes(4);
                if (magic[0] != 0x4E || magic[1] != 0x45 || magic[2] != 0x53 || magic[3] != 0x1A)
                {
                    throw new FileFormatException();
                }
                var prgRomSize = reader.ReadByte();
                var chrRomSize = reader.ReadByte();
                var flags6 = reader.ReadByte();
                var mapper = flags6 >> 4;
                if (mapper != 0)
                {
                    throw new NotSupportedException();
                }
                var padding = reader.ReadBytes(9);
                var prgRom = reader.ReadBytes(0x4000 * prgRomSize);
                var chrRom = reader.ReadBytes(0x2000 * chrRomSize);

                return new ConsoleSystem(mapper, prgRom);
            }
        }
    }
}
