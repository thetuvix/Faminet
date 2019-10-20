using System;
using System.Collections.Generic;
using System.Text;

namespace Faminet.Emulator
{
    public class PPU
    {
        private ushort v;   // Current VRAM address (15 bits)
        private ushort t;   // Temporary VRAM address (15 bits)
        private byte x;     // Fine x scroll (3 bits)
        private bool w;     // First or second write toggle (1 bit)

        public long frame;
        public long scanline;
        public long cycle;

        public PPU()
        {
            frame = 0;
            scanline = 241;
            cycle = 0;
        }

        public void StepCycles(int cycles)
        {
            cycle += cycles;
            if (cycle > 340)
            {
                cycle -= 341;
                scanline++;
            }
            if (scanline > 260)
            {
                scanline -= 262;
                frame++;
            }
        }

        public string StateAsString =>
            $"CYC:{cycle,3} SL:{scanline}";
    }
}