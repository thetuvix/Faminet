using System;
using System.Runtime.CompilerServices;

namespace Faminet.Emulator
{
    public class CPUMemoryMap
    {
        // CPU memory:
        private readonly byte[] ram      = new byte[0x0800];     // $0000-$07FF (mirrors to $1FFF)
        private readonly byte[] ppuReg   = new byte[0x0008];     // $2000-$2007 (mirrors to $3FFF)
        private readonly byte[] apuIoReg = new byte[0x0018];     // $4000-$4017

        // Mapped cartridge memory (fixed mapping for now):
        private readonly byte[] prgRom;                          // Starts at $8000 (mirrors to $FFFF)

        private readonly int mapper;

        public CPUMemoryMap(int mapper, byte[] prgRom)
        {
            this.mapper = mapper;
            this.prgRom = prgRom;
        }

        public byte Read(ushort addr)
        {
            // TODO: Side effects from reading PPU/APU/IO ports
            return Peek(addr);
        }

        public byte Peek(ushort addr)
        {
            if (addr <= 0x1FFF)
                return ram[addr % 0x0800];
            else if (addr <= 0x3FFF)
                return ppuReg[addr % 0x0008];
            else if (addr <= 0x4017)
                return apuIoReg[addr - 0x4000];
            else if (addr >= 0x8000)
                return prgRom[addr % prgRom.Length];
            else
                throw new IndexOutOfRangeException();
        }
        public byte Peek(ushort addr, int offset) => Peek((ushort)(addr + offset));


        public void Write(ushort addr, byte b)
        {
            if (addr <= 0x1FFF)
                ram[addr % 0x0800] = b;
            else if (addr <= 0x3FFF)
                ppuReg[addr % 0x0008] = b;              // TODO: Special PPU write handling
            else if (addr <= 0x4017)
                apuIoReg[addr - 0x4000] = b;            // TODO: Special APU/IO write handling
            else if (addr >= 0x8000)
                throw new IndexOutOfRangeException();   // PRG ROM is readonly
            else
                throw new IndexOutOfRangeException();
        }
    }
}
