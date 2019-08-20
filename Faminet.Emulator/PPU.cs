using System;
using System.Collections.Generic;
using System.Text;

namespace Faminet.Emulator
{
    class PPU
    {
        private ushort v;   // Current VRAM address (15 bits)
        private ushort t;   // Temporary VRAM address (15 bits)
        private byte x;     // Fine x scroll (3 bits)
        private bool w;     // First or second write toggle (1 bit)
    }
}
