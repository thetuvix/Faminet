using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Faminet.Emulator
{
    public class CPU
    {
        private byte A;     // Accumulator
        private byte X, Y;  // Indexes
        private ushort PC;  // Program counter
        private byte S;     // Stack pointer

        // Processor status (P) flags:
        private bool C;                                     // Carry flag
        private bool Z;                                     // Zero flag
        private bool I;                                     // Interrupt Disable flag
        private bool D;                                     // Decimal Mode flag
                            private const byte BBit = 4;    // Break Command flag (1 for BRK/PHP, 0 for IRQ/NMI)
                                                            // Bit 5 unused (always 1)
        private bool V;     private const byte VBit = 6;    // Overflow flag
        private bool N;     private const byte NBit = 7;    // Negative flag
        private byte P
        {
            get => (N, V, true, true, D, I, Z, C).AsBits();
            set => (N, V, _, _, D, I, Z, C) = value;
        }
        private readonly CPUMemoryMap mem;
        public const ushort IRQAddr = 0xFFFE;
        public const ushort ResetAddr = 0xFFFC;
        public const ushort NMIAddr = 0xFFFA;

        public void Jump(ushort addr)
        {
            PC = addr;
        }

        public void JumpIndirect(ushort addr)
        {
            PC = FollowIndirectAddr(addr);
        }

        public CPU(CPUMemoryMap memoryMap)
        {
            mem = memoryMap;

            P = 0b_0011_0100;
            A = X = Y = 0;
            S = 0xFD;

            JumpIndirect(ResetAddr);
        }

        public bool Step()
        {
            Debug.WriteLine($"PC: {PC:X4}  {PeekPC():X2} {PeekPC(1):X2} {PeekPC(2):X2} {PeekPC(3):X2}  A:{A:X2} X:{X:X2} Y:{Y:X2} S:{S:X2}");

            byte inst = ReadPC();

            switch (inst)
            {
                #region Accumulator and arithmetic

                case 0xA9: LDA(Immediate()); break;
                case 0xA5: LDA(ZeroPage()); break;
                case 0xB5: LDA(ZeroPageX()); break;
                case 0xAD: LDA(Absolute()); break;
                case 0xBD: LDA(AbsoluteX()); break;
                case 0xB9: LDA(AbsoluteY()); break;
                case 0xA1: LDA(IndexedIndirectX()); break;
                case 0xB1: LDA(IndirectIndexedY()); break;

                case 0xA2: LDX(Immediate()); break;
                case 0xA6: LDX(ZeroPage()); break;
                case 0xB6: LDX(ZeroPageY()); break;
                case 0xAE: LDX(Absolute()); break;
                case 0xBE: LDX(AbsoluteY()); break;

                case 0xA0: LDY(Immediate()); break;
                case 0xA4: LDY(ZeroPage()); break;
                case 0xB4: LDY(ZeroPageX()); break;
                case 0xAC: LDY(Absolute()); break;
                case 0xBC: LDY(AbsoluteX()); break;

                case 0x85: STA(ZeroPage()); break;
                case 0x95: STA(ZeroPageX()); break;
                case 0x8D: STA(Absolute()); break;
                case 0x9D: STA(AbsoluteX()); break;
                case 0x99: STA(AbsoluteY()); break;
                case 0x81: STA(IndexedIndirectX()); break;
                case 0x91: STA(IndirectIndexedY()); break;

                case 0x86: STX(ZeroPage()); break;
                case 0x96: STX(ZeroPageY()); break;
                case 0x8E: STX(Absolute()); break;

                case 0x84: STY(ZeroPage()); break;
                case 0x94: STY(ZeroPageX()); break;
                case 0x8C: STY(Absolute()); break;

                case 0xAA: Implied(); TAX(); break;
                case 0x8A: Implied(); TXA(); break;
                case 0xA8: Implied(); TAY(); break;
                case 0x98: Implied(); TYA(); break;

                case 0x69: ADC(Immediate()); break;
                case 0x65: ADC(ZeroPage()); break;
                case 0x75: ADC(ZeroPageX()); break;
                case 0x6D: ADC(Absolute()); break;
                case 0x7D: ADC(AbsoluteX()); break;
                case 0x79: ADC(AbsoluteY()); break;
                case 0x61: ADC(IndexedIndirectX()); break;
                case 0x71: ADC(IndirectIndexedY()); break;

                case 0xE9: SBC(Immediate()); break;
                case 0xE5: SBC(ZeroPage()); break;
                case 0xF5: SBC(ZeroPageX()); break;
                case 0xED: SBC(Absolute()); break;
                case 0xFD: SBC(AbsoluteX()); break;
                case 0xF9: SBC(AbsoluteY()); break;
                case 0xE1: SBC(IndexedIndirectX()); break;
                case 0xF1: SBC(IndirectIndexedY()); break;

                case 0x29: AND(Immediate()); break;
                case 0x25: AND(ZeroPage()); break;
                case 0x35: AND(ZeroPageX()); break;
                case 0x2D: AND(Absolute()); break;
                case 0x3D: AND(AbsoluteX()); break;
                case 0x39: AND(AbsoluteY()); break;
                case 0x21: AND(IndexedIndirectX()); break;
                case 0x31: AND(IndirectIndexedY()); break;

                case 0x09: ORA(Immediate()); break;
                case 0x05: ORA(ZeroPage()); break;
                case 0x15: ORA(ZeroPageX()); break;
                case 0x0D: ORA(Absolute()); break;
                case 0x1D: ORA(AbsoluteX()); break;
                case 0x19: ORA(AbsoluteY()); break;
                case 0x01: ORA(IndexedIndirectX()); break;
                case 0x11: ORA(IndirectIndexedY()); break;

                case 0x49: EOR(Immediate()); break;
                case 0x45: EOR(ZeroPage()); break;
                case 0x55: EOR(ZeroPageX()); break;
                case 0x4D: EOR(Absolute()); break;
                case 0x5D: EOR(AbsoluteX()); break;
                case 0x59: EOR(AbsoluteY()); break;
                case 0x41: EOR(IndexedIndirectX()); break;
                case 0x51: EOR(IndirectIndexedY()); break;

                #endregion

                #region Flags and status register

                case 0x38: Implied(); SEC(); break;
                case 0x18: Implied(); CLC(); break;

                case 0x78: Implied(); SEI(); break;
                case 0x58: Implied(); CLI(); break;

                case 0xF8: Implied(); SED(); break;
                case 0xD8: Implied(); CLD(); break;

                case 0xB8: Implied(); CLV(); break;

                #endregion

                #region Tests, branches and jumps

                case 0x4C: JMP(Absolute()); break;
                case 0x6C: JMP(Indirect()); break;

                case 0x30: BMI(Relative()); break;
                case 0x10: BPL(Relative()); break;
                case 0x90: BCC(Relative()); break;
                case 0xB0: BCS(Relative()); break;
                case 0xF0: BEQ(Relative()); break;
                case 0xD0: BNE(Relative()); break;
                case 0x70: BVS(Relative()); break;
                case 0x50: BVC(Relative()); break;

                case 0xC9: CMP(Immediate()); break;
                case 0xC5: CMP(ZeroPage()); break;
                case 0xD5: CMP(ZeroPageX()); break;
                case 0xCD: CMP(Absolute()); break;
                case 0xDD: CMP(AbsoluteX()); break;
                case 0xD9: CMP(AbsoluteY()); break;
                case 0xC1: CMP(IndexedIndirectX()); break;
                case 0xD1: CMP(IndirectIndexedY()); break;

                case 0xE0: CPX(Immediate()); break;
                case 0xE4: CPX(ZeroPage()); break;
                case 0xEC: CPX(Absolute()); break;

                case 0xC0: CPY(Immediate()); break;
                case 0xC4: CPY(ZeroPage()); break;
                case 0xCC: CPY(Absolute()); break;

                case 0x24: BIT(ZeroPage()); break;
                case 0x2C: BIT(Absolute()); break;

                #endregion

                #region Stack processing

                case 0x20: JSR(Absolute()); break;
                case 0x60: Implied(); RTS(); break;

                case 0x48: Implied(); PHA(); break;
                case 0x68: Implied(); PLA(); break;

                case 0x9A: Implied(); TXS(); break;
                case 0xBA: Implied(); TSX(); break;

                case 0x08: Implied(); PHP(); break;
                case 0x28: Implied(); PLP(); break;

                #endregion

                #region Interrupts

                case 0x40: Implied(); RTI(); break;
                case 0x00: Implied(); BRK(); break;

                #endregion

                #region Shift and memory modification

                case 0x4A: LSR(ref Accumulator()); break;
                case 0x46: LSR(ZeroPage()); break;
                case 0x56: LSR(ZeroPageX()); break;
                case 0x4E: LSR(Absolute()); break;
                case 0x5E: LSR(AbsoluteX()); break;

                case 0x0A: ASL(ref Accumulator()); break;
                case 0x06: ASL(ZeroPage()); break;
                case 0x16: ASL(ZeroPageX()); break;
                case 0x0E: ASL(Absolute()); break;
                case 0x1E: ASL(AbsoluteX()); break;

                case 0x2A: ROL(ref Accumulator()); break;
                case 0x26: ROL(ZeroPage()); break;
                case 0x36: ROL(ZeroPageX()); break;
                case 0x2E: ROL(Absolute()); break;
                case 0x3E: ROL(AbsoluteX()); break;

                case 0x6A: ROR(ref Accumulator()); break;
                case 0x66: ROR(ZeroPage()); break;
                case 0x76: ROR(ZeroPageX()); break;
                case 0x6E: ROR(Absolute()); break;
                case 0x7E: ROR(AbsoluteX()); break;

                case 0xE6: INC(ZeroPage()); break;
                case 0xF6: INC(ZeroPageX()); break;
                case 0xEE: INC(Absolute()); break;
                case 0xFE: INC(AbsoluteX()); break;

                case 0xE8: Implied(); INX(); break;
                case 0xC8: Implied(); INY(); break;

                case 0xC6: DEC(ZeroPage()); break;
                case 0xD6: DEC(ZeroPageX()); break;
                case 0xCE: DEC(Absolute()); break;
                case 0xDE: DEC(AbsoluteX()); break;

                case 0xCA: Implied(); DEX(); break;
                case 0x88: Implied(); DEY(); break;

                case 0xEA: Implied(); /* NOP */ break;

                #endregion

                #region Unsupported opcodes

                case 0x02:
                case 0x12:
                case 0x22:
                case 0x32:
                case 0x42:
                case 0x52:
                case 0x62:
                case 0x72:
                case 0x92:
                case 0xB2:
                case 0xD2:
                case 0xF2:
                    {
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} KIL");
                        return true;
                    }

                case 0x1A:
                case 0x3A:
                case 0x5A:
                case 0x7A:
                case 0xDA:
                case 0xFA:
                    {
                        Implied();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2}");
                        break;
                    }

                case 0x0B:
                case 0x2B:
                case 0x4B:
                case 0x6B:
                case 0x8B:
                case 0xAB:
                case 0xCB:
                case 0xEB:
                case 0x82:
                case 0xC2:
                case 0xE2:
                case 0x80:
                case 0x89:
                    {
                        byte b = mem[Immediate()];
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} #${b:X2}");
                        break;
                    }

                case 0x07:
                case 0x27:
                case 0x47:
                case 0x67:
                case 0x87:
                case 0xA7:
                case 0xC7:
                case 0xE7:
                case 0x04:
                case 0x44:
                case 0x64:
                    {
                        ushort addr = ZeroPage();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X2}");
                        break;
                    }

                case 0x17:
                case 0x37:
                case 0x57:
                case 0x77:
                case 0xD7:
                case 0xF7:
                case 0x14:
                case 0x34:
                case 0x54:
                case 0x74:
                case 0xD4:
                case 0xF4:
                    {
                        ushort addr = ZeroPageX();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X2},X");
                        break;
                    }

                case 0x97:
                case 0xB7:
                    {
                        ushort addr = ZeroPageY();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X2},Y");
                        break;
                    }

                case 0x03:
                case 0x23:
                case 0x43:
                case 0x63:
                case 0x83:
                case 0xA3:
                case 0xC3:
                case 0xE3:
                    {
                        ushort addr = IndexedIndirectX();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} (${addr:X2},X)");
                        break;
                    }

                case 0x13:
                case 0x33:
                case 0x53:
                case 0x73:
                case 0x93:
                case 0xB3:
                case 0xD3:
                case 0xF3:
                    {
                        ushort addr = IndirectIndexedY();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} (${addr:X2}),Y");
                        break;
                    }

                case 0x0F:
                case 0x2F:
                case 0x4F:
                case 0x6F:
                case 0x8F:
                case 0xAF:
                case 0xCF:
                case 0xEF:
                case 0x0C:
                    {
                        ushort addr = Absolute();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X4}");
                        break;
                    }

                case 0x1F:
                case 0x3F:
                case 0x5F:
                case 0x7F:
                case 0xDF:
                case 0xFF:
                case 0x1C:
                case 0x3C:
                case 0x5C:
                case 0x7C:
                case 0x9C:
                case 0xDC:
                case 0xFC:
                    {
                        ushort addr = AbsoluteX();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X4},X");
                        break;
                    }

                case 0x1B:
                case 0x3B:
                case 0x5B:
                case 0x7B:
                case 0x9B:
                case 0xBB:
                case 0xDB:
                case 0xFB:
                case 0x9F:
                case 0xBF:
                case 0x9E:
                    {
                        ushort addr = AbsoluteY();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X4},Y");
                        break;
                    }

                #endregion

                default: throw new InvalidOperationException(); // Should not reach if all opcodes listed
            }

            return false;   // Do not halt except for KIL
        }

        private ushort Addr(byte L, byte H, byte offset = 0) => (ushort)(L + (H << 8) + offset);

        #region Address fetches

        private ushort NextAddr() => PC++;
        private byte ReadPC() => mem[NextAddr()];
        private byte PeekPC(int offset = 0) => mem[(ushort)(PC + offset)];

        private ushort RelativeAddr() => (ushort)(ReadPC() + PC);
        private ushort AbsoluteAddr(byte offset) => Addr(ReadPC(), ReadPC(), offset);
        private ushort ZeroPageAddr(byte offset) => (byte)(ReadPC() + offset);
        private ushort IndirectAddr() => FollowIndirectAddr(AbsoluteAddr(0));
        private ushort IndexedIndirectAddr(byte offset) => FollowIndirectAddr(ZeroPageAddr(offset));
        private ushort IndirectIndexedAddr(byte offset) => (ushort)(FollowIndirectAddr(ZeroPageAddr(0)) + offset);
        private ushort FollowIndirectAddr(ushort addr) => Addr(mem[addr], mem[++addr]);

        #endregion

        #region Specific addressing modes

        private void Implied() => PeekPC();             // dummy read of operand byte

        private ushort Immediate() => NextAddr();       // assumed to be read by instruction

        private ushort Absolute() => AbsoluteAddr(0);
        private ushort AbsoluteX() => AbsoluteAddr(X);
        private ushort AbsoluteY() => AbsoluteAddr(Y);

        private ushort ZeroPage() => ZeroPageAddr(0);
        private ushort ZeroPageX() => ZeroPageAddr(X);
        private ushort ZeroPageY() => ZeroPageAddr(Y);

        private ushort Relative() => RelativeAddr();

        private ushort Indirect() => IndirectAddr();
        private ushort IndexedIndirectX() => IndexedIndirectAddr(X);
        private ushort IndirectIndexedY() => IndirectIndexedAddr(Y);

        private ref byte Accumulator()
        {
            PeekPC();       // dummy read of operand byte
            return ref A;
        }

        #endregion

        private void SetZN(byte r)
        {
            Z = (r == 0);
            N = (((sbyte)r) < 0);
        }

        #region Accumulator and arithmetic

        private void LDA(ushort addr) => LDr(addr, ref A);
        private void LDX(ushort addr) => LDr(addr, ref X);
        private void LDY(ushort addr) => LDr(addr, ref Y);
        private void LDr(ushort addr, ref byte r)
        {
            r = mem[addr];
            SetZN(r);
        }

        private void STA(ushort addr) => STr(addr, ref A);
        private void STX(ushort addr) => STr(addr, ref X);
        private void STY(ushort addr) => STr(addr, ref Y);
        private void STr(ushort addr, ref byte r)
        {
            mem[addr] = r;
        }

        private void TAX() => Tsd(ref A, ref X);
        private void TXA() => Tsd(ref X, ref A);
        private void TAY() => Tsd(ref A, ref Y);
        private void TYA() => Tsd(ref Y, ref A);
        private void Tsd(ref byte s, ref byte d)
        {
            d = s;
            SetZN(d);
        }

        private void ADC(ushort addr)
        {
            var sum = A + mem[addr] + C.AsBit();
            A = (byte)sum;
            C = (sum != (byte)sum);
            V = (sum != (sbyte)sum);
            SetZN(A);
        }

        private void SBC(ushort addr)
        {
            bool borrow = !C;
            var diff = A - mem[addr] - borrow.AsBit();
            A = (byte)diff;
            C = (diff == (byte)diff);
            V = (diff != (sbyte)diff);
            SetZN(A);
        }

        private void AND(ushort addr)
        {
            A = (byte)(A & mem[addr]);
            SetZN(A);
        }

        private void ORA(ushort addr)
        {
            A = (byte)(A | mem[addr]);
            SetZN(A);
        }

        private void EOR(ushort addr)
        {
            A = (byte)(A ^ mem[addr]);
            SetZN(A);
        }

        #endregion

        #region Flags and status register

        private void SEC() => C = true;
        private void CLC() => C = false;

        private void SEI() => I = true;
        private void CLI() => I = false;

        private void SED() => D = true;
        private void CLD() => D = false;

        private void CLV() => V = false;

        #endregion

        #region Tests, branches and jumps

        private void JMP(ushort addr) => PC = addr;

        private void BMI(ushort addr) => PC = (N ? addr : PC);
        private void BPL(ushort addr) => PC = (!N ? addr : PC);
        private void BCC(ushort addr) => PC = (!C ? addr : PC);
        private void BCS(ushort addr) => PC = (C ? addr : PC);
        private void BEQ(ushort addr) => PC = (Z ? addr : PC);
        private void BNE(ushort addr) => PC = (!Z ? addr : PC);
        private void BVS(ushort addr) => PC = (V ? addr : PC);
        private void BVC(ushort addr) => PC = (!V ? addr : PC);

        private void CMP(ushort addr) => CPr(addr, ref A);
        private void CPX(ushort addr) => CPr(addr, ref X);
        private void CPY(ushort addr) => CPr(addr, ref Y);
        private void CPr(ushort addr, ref byte r)
        {
            var diff = r - mem[addr];
            C = (diff == (byte)diff);
            SetZN((byte)diff);
        }

        private void BIT(ushort addr)
        {
            byte M = mem[addr];
            Z = ((M & A) == 0);
            N = (M.IsBitSet(NBit));
            V = (M.IsBitSet(VBit));
        }

        #endregion

        #region Stack processing

        private void Push(byte b) => mem[Addr(S--, 0x01)] = b;
        private void PushAddr(ushort addr)
        {
            byte L = (byte)(addr & 0xFF);
            byte H = (byte)(addr >> 8);
            Push(H);
            Push(L);
        }

        private byte Pull() => mem[Addr(++S, 0x01)];
        private ushort PullAddr()
        {
            byte L = Pull();
            byte H = Pull();
            return Addr(L, H);
        }

        private void JSR(ushort addr)
        {
            PushAddr((ushort)(PC - 1));
            PC = addr;
        }

        private void RTS()
        {
            PC = (ushort)(PullAddr() + 1);
        }

        private void PHA() => Push(A);
        private void PLA() => A = Pull();

        private void TXS() => S = X;                // Writes to S do not change status bits
        private void TSX() => Tsd(ref S, ref X);

        private void PHP() => Push(P.WithBit(BBit, true));
        private void PLP() => P = Pull();

        #endregion

        #region Interrupts

        private void RTI()
        {
            P = Pull();
            PC = PullAddr();
        }

        private void BRK()
        {
            PushAddr((ushort)(PC + 1));
            Push(P.WithBit(BBit, true));
            PC = FollowIndirectAddr(IRQAddr);
        }

        #endregion

        #region Shift and memory modification

        private void LSR(ushort addr)
        {
            byte M = mem[addr];
            LSR(ref M);
            mem[addr] = M;
        }
        private void LSR(ref byte r)
        {
            C = r.IsBitSet(0);
            r = (byte)(r >> 1);
            SetZN(A);
        }
        [Obsolete("Missing ref on register", true)]
        private void LSR(byte r) { throw new InvalidOperationException(); }

        private void ASL(ushort addr)
        {
            byte M = mem[addr];
            ASL(ref M);
            mem[addr] = M;
        }
        private void ASL(ref byte r)
        {
            C = r.IsBitSet(7);
            r = (byte)(r << 1);
            SetZN(A);
        }
        [Obsolete("Missing ref on register", true)]
        private void ASL(byte r) { throw new InvalidOperationException(); }

        private void ROL(ushort addr)
        {
            byte M = mem[addr];
            ROL(ref M);
            mem[addr] = M;
        }
        private void ROL(ref byte r)
        {
            bool oldC = C;
            C = r.IsBitSet(7);
            r = ((byte)(r << 1)).WithBit(0, oldC);
            SetZN(A);
        }
        [Obsolete("Missing ref on register", true)]
        private void ROL(byte r) { throw new InvalidOperationException(); }

        private void ROR(ushort addr)
        {
            byte M = mem[addr];
            ROR(ref M);
            mem[addr] = M;
        }
        private void ROR(ref byte r)
        {
            bool oldC = C;
            C = r.IsBitSet(0);
            r = ((byte)(r >> 1)).WithBit(7, oldC);
            SetZN(A);
        }
        [Obsolete("Missing ref on register", true)]
        private void ROR(byte r) { throw new InvalidOperationException(); }

        private void INC(ushort addr)
        {
            byte M = mem[addr];
            INr(ref M);
            mem[addr] = M;
        }
        private void INX() => INr(ref X);
        private void INY() => INr(ref Y);
        private void INr(ref byte r)
        {
            r++;
            SetZN(r);
        }

        private void DEC(ushort addr)
        {
            byte M = mem[addr];
            DEr(ref M);
            mem[addr] = M;
        }
        private void DEX() => DEr(ref X);
        private void DEY() => DEr(ref Y);
        private void DEr(ref byte r)
        {
            r--;
            SetZN(r);
        }

        #endregion
    }

    static class BitHelpers
    {
        public static byte AsBit(this bool b, int bit = 0) => b ? (byte)(1 << bit) : (byte)0;
        public static byte AsBits(this (bool b7, bool b6, bool b5, bool b4, bool b3, bool b2, bool b1, bool b0) b) =>
            (byte)(b.b7.AsBit(7) + b.b6.AsBit(6) + b.b5.AsBit(5) + b.b4.AsBit(4) +
                   b.b3.AsBit(3) + b.b2.AsBit(2) + b.b1.AsBit(1) + b.b0.AsBit(0));

        public static bool IsBitSet(this byte b, int bit) => (b & (1 << bit)) != 0;
        public static (bool b7, bool b6, bool b5, bool b4, bool b3, bool b2, bool b1, bool b0) GetBits(this byte b) =>
            (IsBitSet(b, 7), IsBitSet(b, 6), IsBitSet(b, 5), IsBitSet(b, 4),
             IsBitSet(b, 3), IsBitSet(b, 2), IsBitSet(b, 1), IsBitSet(b, 0));
        public static void Deconstruct(this byte b, out bool b7, out bool b6, out bool b5, out bool b4,
                                                    out bool b3, out bool b2, out bool b1, out bool b0) =>
            (b7, b6, b5, b4, b3, b2, b1, b0) = b.GetBits();

        public static byte WithBit(this byte b, int bit, bool set) =>
            set ?
                (byte)(b | (1 << bit)) :
                (byte)(b & ~(1 << bit));
    }
}