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

        public ConsoleSystem(int mapper, byte[] prgRom, byte[] chrRom, byte[] prgRam)
        {
            Memory = new CPUMemoryMap(mapper, prgRom, chrRom, prgRam);
            CPU = new CPU(Memory);
        }

        public Func<bool> ShouldHalt = null;

        public void Run()
        {
            bool halt;
            do
            {
                halt = CPU.Step();
                halt |= (ShouldHalt?.Invoke() ?? false);
            } while (!halt);
            Debug.WriteLine("Halting.");
        }

        public static ConsoleSystem LoadFromInesStream(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadBytes(4);
                if (magic[0] != 'N' || magic[1] != 'E' || magic[2] != 'S' || magic[3] != 0x1A)
                {
                    throw new FileFormatException();
                }
                var prgRomSize = reader.ReadByte();
                var chrRomSize = reader.ReadByte();
                var flags6 = reader.ReadByte();
                var flags7 = reader.ReadByte();
                var mapper = flags6 >> 4 + ((flags7 >> 4) << 4);
                var hasPrgRam = true;   // Hack for now to run test roms   // flags6.IsBitSet(1);
                var padding = reader.ReadBytes(8);
                byte[] prgRom;
                byte[] chrRom;
                byte[] prgRam;
                switch (mapper)
                {
                    case 0:
                        prgRom = reader.ReadBytes(0x4000 * prgRomSize);
                        chrRom = reader.ReadBytes(0x2000 * chrRomSize);
                        if (hasPrgRam)
                            prgRam = new byte[0x2000];
                        else
                            prgRam = null;
                        break;

                    default:
                        throw new NotSupportedException();
                }

                return new ConsoleSystem(mapper, prgRom, chrRom, prgRam);
            }
        }
    }
}
