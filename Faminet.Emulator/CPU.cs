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
        private bool N;     private const byte NBit = 7;    // Negative flag
        private bool V;     private const byte VBit = 6;    // Overflow flag
                                                            // Bit 5 unused (always 1)
                            private const byte BBit = 4;    // Break Command flag (1 for BRK/PHP, 0 for IRQ/NMI)
        private bool D;                                     // Decimal Mode flag
        private bool I;                                     // Interrupt Disable flag
        private bool Z;                                     // Zero flag
        private bool C;                                     // Carry flag
        private byte P
        {
            get => (N, V, true, true, D, I, Z, C).AsBits();
            set => (N, V, _, _, D, I, Z, C) = value;
        }
        private readonly CPUMemoryMap mem;
        public const ushort IRQAddr = 0xFFFE;
        public const ushort ResetAddr = 0xFFFC;
        public const ushort NMIAddr = 0xFFFA;

        public CPU(CPUMemoryMap memoryMap)
        {
            mem = memoryMap;

            P = 0b_00110100;    // nv_BdIzc
            A = X = Y = 0;

            Reset();
        }

        public void Reset()
        {
            S -= 3;
            I = true;
            // TODO: Reset APU

            JumpIndirect(ResetAddr);
        }

        public void Jump(ushort addr) => PC = addr;

        public void JumpIndirect(ushort addr) => Jump(FollowIndirectAddrRead(addr));

        public bool Step()
        {
            //Debug.WriteLine($"PC: {PC:X4}  {Peek(PC, 0):X2} {Peek(PC, 1):X2} {Peek(PC, 2):X2} {Peek(PC, 3):X2}  A:{A:X2} X:{X:X2} Y:{Y:X2} S:{S:X2}");
            Debug.WriteLine($"PC: {PC:X4}  {Disassemble(PC),-16}  A:{A:X2} X:{X:X2} Y:{Y:X2} S:{S:X2}");

            byte inst = ReadNextByte();

            switch (inst)
            {
                #region Accumulator and arithmetic

                case 0xA9: LDA(ReadImmediateAddr()); break;
                case 0xA5: LDA(ReadZeroPageAddr()); break;
                case 0xB5: LDA(ReadZeroPageXAddr()); break;
                case 0xAD: LDA(ReadAbsoluteAddr()); break;
                case 0xBD: LDA(ReadAbsoluteXAddr()); break;
                case 0xB9: LDA(ReadAbsoluteYAddr()); break;
                case 0xA1: LDA(ReadIndexedIndirectXAddr()); break;
                case 0xB1: LDA(ReadIndirectIndexedYAddr()); break;

                case 0xA2: LDX(ReadImmediateAddr()); break;
                case 0xA6: LDX(ReadZeroPageAddr()); break;
                case 0xB6: LDX(ReadZeroPageYAddr()); break;
                case 0xAE: LDX(ReadAbsoluteAddr()); break;
                case 0xBE: LDX(ReadAbsoluteYAddr()); break;

                case 0xA0: LDY(ReadImmediateAddr()); break;
                case 0xA4: LDY(ReadZeroPageAddr()); break;
                case 0xB4: LDY(ReadZeroPageXAddr()); break;
                case 0xAC: LDY(ReadAbsoluteAddr()); break;
                case 0xBC: LDY(ReadAbsoluteXAddr()); break;

                case 0x85: STA(ReadZeroPageAddr()); break;
                case 0x95: STA(ReadZeroPageXAddr()); break;
                case 0x8D: STA(ReadAbsoluteAddr()); break;
                case 0x9D: STA(ReadAbsoluteXAddr()); break;
                case 0x99: STA(ReadAbsoluteYAddr()); break;
                case 0x81: STA(ReadIndexedIndirectXAddr()); break;
                case 0x91: STA(ReadIndirectIndexedYAddr()); break;

                case 0x86: STX(ReadZeroPageAddr()); break;
                case 0x96: STX(ReadZeroPageYAddr()); break;
                case 0x8E: STX(ReadAbsoluteAddr()); break;

                case 0x84: STY(ReadZeroPageAddr()); break;
                case 0x94: STY(ReadZeroPageXAddr()); break;
                case 0x8C: STY(ReadAbsoluteAddr()); break;

                case 0xAA: ReadImplied(); TAX(); break;
                case 0x8A: ReadImplied(); TXA(); break;
                case 0xA8: ReadImplied(); TAY(); break;
                case 0x98: ReadImplied(); TYA(); break;

                case 0x69: ADC(ReadImmediateAddr()); break;
                case 0x65: ADC(ReadZeroPageAddr()); break;
                case 0x75: ADC(ReadZeroPageXAddr()); break;
                case 0x6D: ADC(ReadAbsoluteAddr()); break;
                case 0x7D: ADC(ReadAbsoluteXAddr()); break;
                case 0x79: ADC(ReadAbsoluteYAddr()); break;
                case 0x61: ADC(ReadIndexedIndirectXAddr()); break;
                case 0x71: ADC(ReadIndirectIndexedYAddr()); break;

                case 0xE9: SBC(ReadImmediateAddr()); break;
                case 0xE5: SBC(ReadZeroPageAddr()); break;
                case 0xF5: SBC(ReadZeroPageXAddr()); break;
                case 0xED: SBC(ReadAbsoluteAddr()); break;
                case 0xFD: SBC(ReadAbsoluteXAddr()); break;
                case 0xF9: SBC(ReadAbsoluteYAddr()); break;
                case 0xE1: SBC(ReadIndexedIndirectXAddr()); break;
                case 0xF1: SBC(ReadIndirectIndexedYAddr()); break;

                case 0x29: AND(ReadImmediateAddr()); break;
                case 0x25: AND(ReadZeroPageAddr()); break;
                case 0x35: AND(ReadZeroPageXAddr()); break;
                case 0x2D: AND(ReadAbsoluteAddr()); break;
                case 0x3D: AND(ReadAbsoluteXAddr()); break;
                case 0x39: AND(ReadAbsoluteYAddr()); break;
                case 0x21: AND(ReadIndexedIndirectXAddr()); break;
                case 0x31: AND(ReadIndirectIndexedYAddr()); break;

                case 0x09: ORA(ReadImmediateAddr()); break;
                case 0x05: ORA(ReadZeroPageAddr()); break;
                case 0x15: ORA(ReadZeroPageXAddr()); break;
                case 0x0D: ORA(ReadAbsoluteAddr()); break;
                case 0x1D: ORA(ReadAbsoluteXAddr()); break;
                case 0x19: ORA(ReadAbsoluteYAddr()); break;
                case 0x01: ORA(ReadIndexedIndirectXAddr()); break;
                case 0x11: ORA(ReadIndirectIndexedYAddr()); break;

                case 0x49: EOR(ReadImmediateAddr()); break;
                case 0x45: EOR(ReadZeroPageAddr()); break;
                case 0x55: EOR(ReadZeroPageXAddr()); break;
                case 0x4D: EOR(ReadAbsoluteAddr()); break;
                case 0x5D: EOR(ReadAbsoluteXAddr()); break;
                case 0x59: EOR(ReadAbsoluteYAddr()); break;
                case 0x41: EOR(ReadIndexedIndirectXAddr()); break;
                case 0x51: EOR(ReadIndirectIndexedYAddr()); break;

                #endregion

                #region Flags and status register

                case 0x38: ReadImplied(); SEC(); break;
                case 0x18: ReadImplied(); CLC(); break;

                case 0x78: ReadImplied(); SEI(); break;
                case 0x58: ReadImplied(); CLI(); break;

                case 0xF8: ReadImplied(); SED(); break;
                case 0xD8: ReadImplied(); CLD(); break;

                case 0xB8: ReadImplied(); CLV(); break;

                #endregion

                #region Tests, branches and jumps

                case 0x4C: JMP(ReadAbsoluteAddr()); break;
                case 0x6C: JMP(ReadIndirectAddr()); break;

                case 0x30: BMI(ReadRelativeAddr()); break;
                case 0x10: BPL(ReadRelativeAddr()); break;
                case 0x90: BCC(ReadRelativeAddr()); break;
                case 0xB0: BCS(ReadRelativeAddr()); break;
                case 0xF0: BEQ(ReadRelativeAddr()); break;
                case 0xD0: BNE(ReadRelativeAddr()); break;
                case 0x70: BVS(ReadRelativeAddr()); break;
                case 0x50: BVC(ReadRelativeAddr()); break;

                case 0xC9: CMP(ReadImmediateAddr()); break;
                case 0xC5: CMP(ReadZeroPageAddr()); break;
                case 0xD5: CMP(ReadZeroPageXAddr()); break;
                case 0xCD: CMP(ReadAbsoluteAddr()); break;
                case 0xDD: CMP(ReadAbsoluteXAddr()); break;
                case 0xD9: CMP(ReadAbsoluteYAddr()); break;
                case 0xC1: CMP(ReadIndexedIndirectXAddr()); break;
                case 0xD1: CMP(ReadIndirectIndexedYAddr()); break;

                case 0xE0: CPX(ReadImmediateAddr()); break;
                case 0xE4: CPX(ReadZeroPageAddr()); break;
                case 0xEC: CPX(ReadAbsoluteAddr()); break;

                case 0xC0: CPY(ReadImmediateAddr()); break;
                case 0xC4: CPY(ReadZeroPageAddr()); break;
                case 0xCC: CPY(ReadAbsoluteAddr()); break;

                case 0x24: BIT(ReadZeroPageAddr()); break;
                case 0x2C: BIT(ReadAbsoluteAddr()); break;

                #endregion

                #region Stack processing

                case 0x20: JSR(ReadAbsoluteAddr()); break;
                case 0x60: ReadImplied(); RTS(); break;

                case 0x48: ReadImplied(); PHA(); break;
                case 0x68: ReadImplied(); PLA(); break;

                case 0x9A: ReadImplied(); TXS(); break;
                case 0xBA: ReadImplied(); TSX(); break;

                case 0x08: ReadImplied(); PHP(); break;
                case 0x28: ReadImplied(); PLP(); break;

                #endregion

                #region Interrupts

                case 0x40: ReadImplied(); RTI(); break;
                case 0x00: ReadImplied(); BRK(); break;

                #endregion

                #region Shift and memory modification

                case 0x4A: LSR(ref ReadAccumulatorOperand()); break;
                case 0x46: LSR(ReadZeroPageAddr()); break;
                case 0x56: LSR(ReadZeroPageXAddr()); break;
                case 0x4E: LSR(ReadAbsoluteAddr()); break;
                case 0x5E: LSR(ReadAbsoluteXAddr()); break;

                case 0x0A: ASL(ref ReadAccumulatorOperand()); break;
                case 0x06: ASL(ReadZeroPageAddr()); break;
                case 0x16: ASL(ReadZeroPageXAddr()); break;
                case 0x0E: ASL(ReadAbsoluteAddr()); break;
                case 0x1E: ASL(ReadAbsoluteXAddr()); break;

                case 0x2A: ROL(ref ReadAccumulatorOperand()); break;
                case 0x26: ROL(ReadZeroPageAddr()); break;
                case 0x36: ROL(ReadZeroPageXAddr()); break;
                case 0x2E: ROL(ReadAbsoluteAddr()); break;
                case 0x3E: ROL(ReadAbsoluteXAddr()); break;

                case 0x6A: ROR(ref ReadAccumulatorOperand()); break;
                case 0x66: ROR(ReadZeroPageAddr()); break;
                case 0x76: ROR(ReadZeroPageXAddr()); break;
                case 0x6E: ROR(ReadAbsoluteAddr()); break;
                case 0x7E: ROR(ReadAbsoluteXAddr()); break;

                case 0xE6: INC(ReadZeroPageAddr()); break;
                case 0xF6: INC(ReadZeroPageXAddr()); break;
                case 0xEE: INC(ReadAbsoluteAddr()); break;
                case 0xFE: INC(ReadAbsoluteXAddr()); break;

                case 0xE8: ReadImplied(); INX(); break;
                case 0xC8: ReadImplied(); INY(); break;

                case 0xC6: DEC(ReadZeroPageAddr()); break;
                case 0xD6: DEC(ReadZeroPageXAddr()); break;
                case 0xCE: DEC(ReadAbsoluteAddr()); break;
                case 0xDE: DEC(ReadAbsoluteXAddr()); break;

                case 0xCA: ReadImplied(); DEX(); break;
                case 0x88: ReadImplied(); DEY(); break;

                case 0xEA: ReadImplied(); /* NOP */ break;

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
                        ReadImplied();
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
                        byte b = mem.Read(ReadImmediateAddr());
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
                        ushort addr = ReadZeroPageAddr();
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
                        ushort addr = ReadZeroPageXAddr();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X2},X");
                        break;
                    }

                case 0x97:
                case 0xB7:
                    {
                        ushort addr = ReadZeroPageYAddr();
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
                        ushort addr = ReadIndexedIndirectXAddr();
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
                        ushort addr = ReadIndirectIndexedYAddr();
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
                        ushort addr = ReadAbsoluteAddr();
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
                        ushort addr = ReadAbsoluteXAddr();
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
                        ushort addr = ReadAbsoluteYAddr();
                        Debug.WriteLine($"Unsupported opcode: {inst:X2} ${addr:X4},Y");
                        break;
                    }

                #endregion

                default: throw new InvalidOperationException(); // Should not reach if all opcodes listed
            }

            return false;   // Do not halt except for KIL
        }

        private ushort Addr(byte L, byte H, byte offset = 0) => (ushort)(L + (H << 8) + offset);

        private ushort StepNextAddr() => PC++;
        private byte ReadNextByte() => mem.Read(StepNextAddr());
        private void DummyRead(ushort addr) => mem.Read(addr);

        private void ReadImplied() => DummyRead(PC);                // dummy read of next byte without advancing

        private ushort ReadImmediateAddr() => StepNextAddr();       // assumed to be read by instruction
        
        private ushort ReadAbsoluteAddr(byte offset) => Addr(ReadNextByte(), ReadNextByte(), offset);
        private ushort ReadAbsoluteAddr() => ReadAbsoluteAddr(0);
        private ushort ReadAbsoluteXAddr() => ReadAbsoluteAddr(X);
        private ushort ReadAbsoluteYAddr() => ReadAbsoluteAddr(Y);

        private ushort ReadZeroPageAddr(byte offset) => (byte)(ReadNextByte() + offset);
        private ushort ReadZeroPageAddr() => ReadZeroPageAddr(0);
        private ushort ReadZeroPageXAddr() => ReadZeroPageAddr(X);
        private ushort ReadZeroPageYAddr() => ReadZeroPageAddr(Y);

        private ushort ReadIndirectAddr() => FollowIndirectAddrRead(ReadAbsoluteAddr());
        private ushort FollowIndirectAddrRead(ushort addr) => Addr(mem.Read(addr), mem.Read(++addr));

        private ushort ReadIndexedIndirectAddr(byte offset) => FollowIndirectAddrRead(ReadZeroPageAddr(offset));
        private ushort ReadIndexedIndirectXAddr() => ReadIndexedIndirectAddr(X);

        private ushort ReadIndirectIndexedAddr(byte offset) => (ushort)(FollowIndirectAddrRead(ReadZeroPageAddr()) + offset);
        private ushort ReadIndirectIndexedYAddr() => ReadIndirectIndexedAddr(Y);

        private ushort ReadRelativeAddr() => (ushort)((sbyte)ReadNextByte() + PC);

        private ref byte ReadAccumulatorOperand()
        {
            ReadImplied();
            return ref A;
        }

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
            r = mem.Read(addr);
            SetZN(r);
        }

        private void STA(ushort addr) => STr(addr, ref A);
        private void STX(ushort addr) => STr(addr, ref X);
        private void STY(ushort addr) => STr(addr, ref Y);
        private void STr(ushort addr, ref byte r)
        {
            mem.Write(addr, r);
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
            var unsignedSum = A + mem.Read(addr) + C.AsBit();
            var signedSum = (sbyte)A + (sbyte)mem.Read(addr) + C.AsBit();
            A = (byte)unsignedSum;
            C = (unsignedSum != (byte)unsignedSum);
            V = (signedSum != (sbyte)signedSum);
            SetZN(A);
        }

        private void SBC(ushort addr)
        {
            bool borrow = !C;
            var unsignedDiff = A - mem.Read(addr) - borrow.AsBit();
            var signedDiff = (sbyte)A - (sbyte)mem.Read(addr) - borrow.AsBit();
            A = (byte)unsignedDiff;
            C = (unsignedDiff == (byte)unsignedDiff);
            V = (signedDiff != (sbyte)signedDiff);
            SetZN(A);
        }

        private void AND(ushort addr)
        {
            A = (byte)(A & mem.Read(addr));
            SetZN(A);
        }

        private void ORA(ushort addr)
        {
            A = (byte)(A | mem.Read(addr));
            SetZN(A);
        }

        private void EOR(ushort addr)
        {
            A = (byte)(A ^ mem.Read(addr));
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
            var diff = r - mem.Read(addr);
            C = (diff == (byte)diff);
            SetZN((byte)diff);
        }

        private void BIT(ushort addr)
        {
            byte M = mem.Read(addr);
            Z = ((M & A) == 0);
            N = (M.IsBitSet(NBit));
            V = (M.IsBitSet(VBit));
        }

        #endregion

        #region Stack processing

        private void Push(byte b) => mem.Write(Addr(S--, 0x01), b);
        private void PushAddr(ushort addr)
        {
            byte L = (byte)(addr & 0xFF);
            byte H = (byte)(addr >> 8);
            Push(H);
            Push(L);
        }

        private byte Pull() => mem.Read(Addr(++S, 0x01));
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
        private void PLA()
        {
            A = Pull();
            SetZN(A);
        }

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

        public void NMI() => Interrupt(NMIAddr, B: false);
        public void IRQ() => Interrupt(IRQAddr, B: false);
        public void BRK() => Interrupt(IRQAddr, B: true);

        private void Interrupt(ushort addr, bool B)
        {
            PushAddr((ushort)(PC + 1));
            Push(P.WithBit(BBit, B));
            I = true;
            JumpIndirect(addr);
        }

        #endregion

        #region Shift and memory modification

        private void LSR(ushort addr)
        {
            byte M = mem.Read(addr);
            LSR(ref M);
            mem.Write(addr, M);
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
            byte M = mem.Read(addr);
            ASL(ref M);
            mem.Write(addr, M);
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
            byte M = mem.Read(addr);
            ROL(ref M);
            mem.Write(addr, M);
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
            byte M = mem.Read(addr);
            ROR(ref M);
            mem.Write(addr, M);
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
            byte M = mem.Read(addr);
            INr(ref M);
            mem.Write(addr, M);
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
            byte M = mem.Read(addr);
            DEr(ref M);
            mem.Write(addr, M);
        }
        private void DEX() => DEr(ref X);
        private void DEY() => DEr(ref Y);
        private void DEr(ref byte r)
        {
            r--;
            SetZN(r);
        }

        #endregion

        public string Disassemble(ushort addr)
        {
            byte inst = mem.Peek(addr);

            switch (inst)
            {
                #region Accumulator and arithmetic

                case 0xA9: return "LDA" + DisassembleImmediate(addr);
                case 0xA5: return "LDA" + DisassembleZeroPage(addr);
                case 0xB5: return "LDA" + DisassembleZeroPageX(addr);
                case 0xAD: return "LDA" + DisassembleAbsolute(addr);
                case 0xBD: return "LDA" + DisassembleAbsoluteX(addr);
                case 0xB9: return "LDA" + DisassembleAbsoluteY(addr);
                case 0xA1: return "LDA" + DisassembleIndexedIndirectX(addr);
                case 0xB1: return "LDA" + DisassembleIndirectIndexedY(addr);

                case 0xA2: return "LDX" + DisassembleImmediate(addr);
                case 0xA6: return "LDX" + DisassembleZeroPage(addr);
                case 0xB6: return "LDX" + DisassembleZeroPageY(addr);
                case 0xAE: return "LDX" + DisassembleAbsolute(addr);
                case 0xBE: return "LDX" + DisassembleAbsoluteY(addr);

                case 0xA0: return "LDY" + DisassembleImmediate(addr);
                case 0xA4: return "LDY" + DisassembleZeroPage(addr);
                case 0xB4: return "LDY" + DisassembleZeroPageX(addr);
                case 0xAC: return "LDY" + DisassembleAbsolute(addr);
                case 0xBC: return "LDY" + DisassembleAbsoluteX(addr);

                case 0x85: return "STA" + DisassembleZeroPage(addr);
                case 0x95: return "STA" + DisassembleZeroPageX(addr);
                case 0x8D: return "STA" + DisassembleAbsolute(addr);
                case 0x9D: return "STA" + DisassembleAbsoluteX(addr);
                case 0x99: return "STA" + DisassembleAbsoluteY(addr);
                case 0x81: return "STA" + DisassembleIndexedIndirectX(addr);
                case 0x91: return "STA" + DisassembleIndirectIndexedY(addr);

                case 0x86: return "STX" + DisassembleZeroPage(addr);
                case 0x96: return "STX" + DisassembleZeroPageY(addr);
                case 0x8E: return "STX" + DisassembleAbsolute(addr);

                case 0x84: return "STY" + DisassembleZeroPage(addr);
                case 0x94: return "STY" + DisassembleZeroPageX(addr);
                case 0x8C: return "STY" + DisassembleAbsolute(addr);

                case 0xAA: return "TAX" + DisassembleImplied();
                case 0x8A: return "TXA" + DisassembleImplied();
                case 0xA8: return "TAY" + DisassembleImplied();
                case 0x98: return "TYA" + DisassembleImplied();

                case 0x69: return "ADC" + DisassembleImmediate(addr);
                case 0x65: return "ADC" + DisassembleZeroPage(addr);
                case 0x75: return "ADC" + DisassembleZeroPageX(addr);
                case 0x6D: return "ADC" + DisassembleAbsolute(addr);
                case 0x7D: return "ADC" + DisassembleAbsoluteX(addr);
                case 0x79: return "ADC" + DisassembleAbsoluteY(addr);
                case 0x61: return "ADC" + DisassembleIndexedIndirectX(addr);
                case 0x71: return "ADC" + DisassembleIndirectIndexedY(addr);

                case 0xE9: return "SBC" + DisassembleImmediate(addr);
                case 0xE5: return "SBC" + DisassembleZeroPage(addr);
                case 0xF5: return "SBC" + DisassembleZeroPageX(addr);
                case 0xED: return "SBC" + DisassembleAbsolute(addr);
                case 0xFD: return "SBC" + DisassembleAbsoluteX(addr);
                case 0xF9: return "SBC" + DisassembleAbsoluteY(addr);
                case 0xE1: return "SBC" + DisassembleIndexedIndirectX(addr);
                case 0xF1: return "SBC" + DisassembleIndirectIndexedY(addr);

                case 0x29: return "AND" + DisassembleImmediate(addr);
                case 0x25: return "AND" + DisassembleZeroPage(addr);
                case 0x35: return "AND" + DisassembleZeroPageX(addr);
                case 0x2D: return "AND" + DisassembleAbsolute(addr);
                case 0x3D: return "AND" + DisassembleAbsoluteX(addr);
                case 0x39: return "AND" + DisassembleAbsoluteY(addr);
                case 0x21: return "AND" + DisassembleIndexedIndirectX(addr);
                case 0x31: return "AND" + DisassembleIndirectIndexedY(addr);

                case 0x09: return "ORA" + DisassembleImmediate(addr);
                case 0x05: return "ORA" + DisassembleZeroPage(addr);
                case 0x15: return "ORA" + DisassembleZeroPageX(addr);
                case 0x0D: return "ORA" + DisassembleAbsolute(addr);
                case 0x1D: return "ORA" + DisassembleAbsoluteX(addr);
                case 0x19: return "ORA" + DisassembleAbsoluteY(addr);
                case 0x01: return "ORA" + DisassembleIndexedIndirectX(addr);
                case 0x11: return "ORA" + DisassembleIndirectIndexedY(addr);

                case 0x49: return "EOR" + DisassembleImmediate(addr);
                case 0x45: return "EOR" + DisassembleZeroPage(addr);
                case 0x55: return "EOR" + DisassembleZeroPageX(addr);
                case 0x4D: return "EOR" + DisassembleAbsolute(addr);
                case 0x5D: return "EOR" + DisassembleAbsoluteX(addr);
                case 0x59: return "EOR" + DisassembleAbsoluteY(addr);
                case 0x41: return "EOR" + DisassembleIndexedIndirectX(addr);
                case 0x51: return "EOR" + DisassembleIndirectIndexedY(addr);

                #endregion

                #region Flags and status register

                case 0x38: return "SEC" + DisassembleImplied();
                case 0x18: return "CLC" + DisassembleImplied();

                case 0x78: return "SEI" + DisassembleImplied();
                case 0x58: return "CLI" + DisassembleImplied();

                case 0xF8: return "SED" + DisassembleImplied();
                case 0xD8: return "CLD" + DisassembleImplied();

                case 0xB8: return "CLV" + DisassembleImplied();

                #endregion

                #region Tests, branches and jumps

                case 0x4C: return "JMP" + DisassembleAbsolute(addr);
                case 0x6C: return "JMP" + DisassembleIndirect(addr);

                case 0x30: return "BMI" + DisassembleRelative(addr);
                case 0x10: return "BPL" + DisassembleRelative(addr);
                case 0x90: return "BCC" + DisassembleRelative(addr);
                case 0xB0: return "BCS" + DisassembleRelative(addr);
                case 0xF0: return "BEQ" + DisassembleRelative(addr);
                case 0xD0: return "BNE" + DisassembleRelative(addr);
                case 0x70: return "BVS" + DisassembleRelative(addr);
                case 0x50: return "BVC" + DisassembleRelative(addr);

                case 0xC9: return "CMP" + DisassembleImmediate(addr);
                case 0xC5: return "CMP" + DisassembleZeroPage(addr);
                case 0xD5: return "CMP" + DisassembleZeroPageX(addr);
                case 0xCD: return "CMP" + DisassembleAbsolute(addr);
                case 0xDD: return "CMP" + DisassembleAbsoluteX(addr);
                case 0xD9: return "CMP" + DisassembleAbsoluteY(addr);
                case 0xC1: return "CMP" + DisassembleIndexedIndirectX(addr);
                case 0xD1: return "CMP" + DisassembleIndirectIndexedY(addr);

                case 0xE0: return "CPX" + DisassembleImmediate(addr);
                case 0xE4: return "CPX" + DisassembleZeroPage(addr);
                case 0xEC: return "CPX" + DisassembleAbsolute(addr);

                case 0xC0: return "CPY" + DisassembleImmediate(addr);
                case 0xC4: return "CPY" + DisassembleZeroPage(addr);
                case 0xCC: return "CPY" + DisassembleAbsolute(addr);

                case 0x24: return "BIT" + DisassembleZeroPage(addr);
                case 0x2C: return "BIT" + DisassembleAbsolute(addr);

                #endregion

                #region Stack processing

                case 0x20: return "JSR" + DisassembleAbsolute(addr);
                case 0x60: return "RTS" + DisassembleImplied();

                case 0x48: return "PHA" + DisassembleImplied();
                case 0x68: return "PLA" + DisassembleImplied();

                case 0x9A: return "TXS" + DisassembleImplied();
                case 0xBA: return "TSX" + DisassembleImplied();

                case 0x08: return "PHP" + DisassembleImplied();
                case 0x28: return "PLP" + DisassembleImplied();

                #endregion

                #region Interrupts

                case 0x40: return "RTI" + DisassembleImplied();
                case 0x00: return "BRK" + DisassembleImplied();

                #endregion

                #region Shift and memory modification

                case 0x4A: return "LSR" + DisassembleAccumulator(addr);
                case 0x46: return "LSR" + DisassembleZeroPage(addr);
                case 0x56: return "LSR" + DisassembleZeroPageX(addr);
                case 0x4E: return "LSR" + DisassembleAbsolute(addr);
                case 0x5E: return "LSR" + DisassembleAbsoluteX(addr);

                case 0x0A: return "ASL" + DisassembleAccumulator(addr);
                case 0x06: return "ASL" + DisassembleZeroPage(addr);
                case 0x16: return "ASL" + DisassembleZeroPageX(addr);
                case 0x0E: return "ASL" + DisassembleAbsolute(addr);
                case 0x1E: return "ASL" + DisassembleAbsoluteX(addr);

                case 0x2A: return "ROL" + DisassembleAccumulator(addr);
                case 0x26: return "ROL" + DisassembleZeroPage(addr);
                case 0x36: return "ROL" + DisassembleZeroPageX(addr);
                case 0x2E: return "ROL" + DisassembleAbsolute(addr);
                case 0x3E: return "ROL" + DisassembleAbsoluteX(addr);

                case 0x6A: return "ROR" + DisassembleAccumulator(addr);
                case 0x66: return "ROR" + DisassembleZeroPage(addr);
                case 0x76: return "ROR" + DisassembleZeroPageX(addr);
                case 0x6E: return "ROR" + DisassembleAbsolute(addr);
                case 0x7E: return "ROR" + DisassembleAbsoluteX(addr);

                case 0xE6: return "INC" + DisassembleZeroPage(addr);
                case 0xF6: return "INC" + DisassembleZeroPageX(addr);
                case 0xEE: return "INC" + DisassembleAbsolute(addr);
                case 0xFE: return "INC" + DisassembleAbsoluteX(addr);

                case 0xE8: return "INX" + DisassembleImplied();
                case 0xC8: return "INY" + DisassembleImplied();

                case 0xC6: return "DEC" + DisassembleZeroPage(addr);
                case 0xD6: return "DEC" + DisassembleZeroPageX(addr);
                case 0xCE: return "DEC" + DisassembleAbsolute(addr);
                case 0xDE: return "DEC" + DisassembleAbsoluteX(addr);

                case 0xCA: return "DEX" + DisassembleImplied();
                case 0x88: return "DEY" + DisassembleImplied();

                case 0xEA: return "NOP" + DisassembleImplied();

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
                    return "KIL" + DisassembleImplied();

                case 0x1A:
                case 0x3A:
                case 0x5A:
                case 0x7A:
                case 0xDA:
                case 0xFA:
                    return $"{inst:X2}" + DisassembleImplied();

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
                    return $"{inst:X2}" + DisassembleImmediate(addr);

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
                    return $"{inst:X2}" + DisassembleZeroPage(addr);

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
                    return $"{inst:X2}" + DisassembleZeroPageX(addr);

                case 0x97:
                case 0xB7:
                    return $"{inst:X2}" + DisassembleZeroPageY(addr);

                case 0x03:
                case 0x23:
                case 0x43:
                case 0x63:
                case 0x83:
                case 0xA3:
                case 0xC3:
                case 0xE3:
                    return $"{inst:X2}" + DisassembleIndexedIndirectX(addr);

                case 0x13:
                case 0x33:
                case 0x53:
                case 0x73:
                case 0x93:
                case 0xB3:
                case 0xD3:
                case 0xF3:
                    return $"{inst:X2}" + DisassembleIndirectIndexedY(addr);

                case 0x0F:
                case 0x2F:
                case 0x4F:
                case 0x6F:
                case 0x8F:
                case 0xAF:
                case 0xCF:
                case 0xEF:
                case 0x0C:
                    return $"{inst:X2}" + DisassembleAbsolute(addr);

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
                    return $"{inst:X2}" + DisassembleAbsoluteX(addr);

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
                    return $"{inst:X2}" + DisassembleAbsoluteY(addr);

                #endregion

                default: throw new InvalidOperationException(); // Should not reach if all opcodes listed
            }
        }

        private string DisassembleImplied() => "";
        private string DisassembleImmediate(ushort instAddr) => $" #${PeekImmediateOperand(instAddr):X2}";
        private string DisassembleAbsolute(ushort instAddr) => $" ${PeekAbsoluteOperand(instAddr):X4}";
        private string DisassembleAbsoluteX(ushort instAddr) => $" ${PeekAbsoluteOperand(instAddr):X4},X";
        private string DisassembleAbsoluteY(ushort instAddr) => $" ${PeekAbsoluteOperand(instAddr):X4},Y";
        private string DisassembleZeroPage(ushort instAddr) => $" ${PeekZeroPageOperand(instAddr):X2}";
        private string DisassembleZeroPageX(ushort instAddr) => $" ${PeekZeroPageOperand(instAddr):X2},X";
        private string DisassembleZeroPageY(ushort instAddr) => $" ${PeekZeroPageOperand(instAddr):X2},Y";
        private string DisassembleIndirect(ushort instAddr) => $" (${PeekAbsoluteOperand(instAddr):X4})";
        private string DisassembleIndexedIndirectX(ushort instAddr) => $" (${PeekZeroPageOperand(instAddr):X2},X)";
        private string DisassembleIndirectIndexedY(ushort instAddr) => $" (${PeekZeroPageOperand(instAddr):X2}),Y";
        private string DisassembleRelative(ushort instAddr) => $" ${PeekRelativeOperand(instAddr):X4}";
        private string DisassembleAccumulator(ushort instAddr) => $" A";

        private ushort PeekImmediateOperand(ushort instAddr) => mem.Peek(instAddr, 1);
        private ushort PeekAbsoluteOperand(ushort instAddr) => Addr(mem.Peek(instAddr, 1), mem.Peek(instAddr, 2));
        private ushort PeekZeroPageOperand(ushort instAddr) => mem.Peek(instAddr, 1);
        private ushort PeekRelativeOperand(ushort instAddr) => Addr(mem.Peek(instAddr, 1), mem.Peek(instAddr, 2));
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