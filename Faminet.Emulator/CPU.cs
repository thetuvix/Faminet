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
        private bool N; private const byte NBit = 7;    // 7: Negative flag
        private bool V; private const byte VBit = 6;    // 6: Overflow flag
                                                        // 5: Bit 5 unused (always 1)
        private const byte BBit = 4;    // 4: Break Command flag (1 for BRK/PHP, 0 for IRQ/NMI)
        private bool D;                                 // 3: Decimal Mode flag
        private bool I;                                 // 2: Interrupt Disable flag
        private bool Z;                                 // 1: Zero flag
        private bool C;                                 // 0: Carry flag
        private byte P
        {
            get => (N, V, true, true, D, I, Z, C).AsBits();
            set => (N, V, _, _, D, I, Z, C) = value;
        }
        private string PAsString => $"{N.AsLetter("N")}{V.AsLetter("V")}__" +
                                    $"{D.AsLetter("D")}{I.AsLetter("I")}{Z.AsLetter("Z")}{C.AsLetter("C")}";
        private readonly CPUMemoryMap mem;
        public const ushort IRQAddr = 0xFFFE;
        public const ushort ResetAddr = 0xFFFC;
        public const ushort NMIAddr = 0xFFFA;

        public long Cycle { get; private set; }
        private void SpendCycles(int cycles) => Cycle += cycles;
        private ushort SpendCyclesFor(int cycles, ushort addr)
        {
            SpendCycles(cycles);
            return addr;
        }
        private ushort SpendCyclesFor(int cycles, ushort addr, byte offset, bool cycleLessIfSamePage)
        {
            (byte baseL, byte baseH) = GetAddrBytes(addr);
            var offsetAddr = Addr(baseL, baseH, offset);
            if (cycleLessIfSamePage && (baseH == GetAddrBytes(offsetAddr).H))
                SpendCycles(cycles - 1);
            else
                SpendCycles(cycles);
            return offsetAddr;
        }
        private byte SpendCyclesFor(int cycles, byte addr)
        {
            SpendCycles(cycles);
            return addr;
        }

        public string StateAsString =>
            //WriteDebugLine($"PC: {PC:X4}  {Peek(PC, 0):X2} {Peek(PC, 1):X2} {Peek(PC, 2):X2} {Peek(PC, 3):X2}  A:{A:X2} X:{X:X2} Y:{Y:X2} S:{S:X2}");
            $"{PC:X4}  {Disassemble(PC),-40}  A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P.WithBit(BBit, false):X2} SP:{S:X2}";

        public CPU(CPUMemoryMap memoryMap)
        {
            mem = memoryMap;

            P = 0b_00110100;    // nv__dIzc
            A = X = Y = 0;
            Cycle = 7;          // Matches known test logs

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

        public void JumpIndirect(ushort addr) => Jump(FollowIndirectAbsoluteAddrRead(addr));

        public bool Step()
        {
            byte inst = ReadNextByte();

            switch (inst)
            {
                #region Accumulator and arithmetic

                case 0xA9: LDA(ReadImmediateAddrMode()); break;
                case 0xA5: LDA(ReadZeroPageAddrMode()); break;
                case 0xB5: LDA(ReadZeroPageXAddrMode()); break;
                case 0xAD: LDA(ReadAbsoluteAddrMode()); break;
                case 0xBD: LDA(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0xB9: LDA(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0xA1: LDA(ReadIndexedIndirectXAddrMode()); break;
                case 0xB1: LDA(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                case 0xA2: LDX(ReadImmediateAddrMode()); break;
                case 0xA6: LDX(ReadZeroPageAddrMode()); break;
                case 0xB6: LDX(ReadZeroPageYAddrMode()); break;
                case 0xAE: LDX(ReadAbsoluteAddrMode()); break;
                case 0xBE: LDX(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;

                case 0xA0: LDY(ReadImmediateAddrMode()); break;
                case 0xA4: LDY(ReadZeroPageAddrMode()); break;
                case 0xB4: LDY(ReadZeroPageXAddrMode()); break;
                case 0xAC: LDY(ReadAbsoluteAddrMode()); break;
                case 0xBC: LDY(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;

                case 0x85: STA(ReadZeroPageAddrMode()); break;
                case 0x95: STA(ReadZeroPageXAddrMode()); break;
                case 0x8D: STA(ReadAbsoluteAddrMode()); break;
                case 0x9D: STA(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;
                case 0x99: STA(ReadAbsoluteYAddrMode(cycleLessIfSamePage: false)); break;
                case 0x81: STA(ReadIndexedIndirectXAddrMode()); break;
                case 0x91: STA(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: false)); break;

                case 0x86: STX(ReadZeroPageAddrMode()); break;
                case 0x96: STX(ReadZeroPageYAddrMode()); break;
                case 0x8E: STX(ReadAbsoluteAddrMode()); break;

                case 0x84: STY(ReadZeroPageAddrMode()); break;
                case 0x94: STY(ReadZeroPageXAddrMode()); break;
                case 0x8C: STY(ReadAbsoluteAddrMode()); break;

                case 0xAA: ReadImpliedMode(); TAX(); break;
                case 0x8A: ReadImpliedMode(); TXA(); break;
                case 0xA8: ReadImpliedMode(); TAY(); break;
                case 0x98: ReadImpliedMode(); TYA(); break;

                case 0x69: ADC(ReadImmediateAddrMode()); break;
                case 0x65: ADC(ReadZeroPageAddrMode()); break;
                case 0x75: ADC(ReadZeroPageXAddrMode()); break;
                case 0x6D: ADC(ReadAbsoluteAddrMode()); break;
                case 0x7D: ADC(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0x79: ADC(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0x61: ADC(ReadIndexedIndirectXAddrMode()); break;
                case 0x71: ADC(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                case 0xE9: SBC(ReadImmediateAddrMode()); break;
                case 0xE5: SBC(ReadZeroPageAddrMode()); break;
                case 0xF5: SBC(ReadZeroPageXAddrMode()); break;
                case 0xED: SBC(ReadAbsoluteAddrMode()); break;
                case 0xFD: SBC(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0xF9: SBC(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0xE1: SBC(ReadIndexedIndirectXAddrMode()); break;
                case 0xF1: SBC(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                case 0x29: AND(ReadImmediateAddrMode()); break;
                case 0x25: AND(ReadZeroPageAddrMode()); break;
                case 0x35: AND(ReadZeroPageXAddrMode()); break;
                case 0x2D: AND(ReadAbsoluteAddrMode()); break;
                case 0x3D: AND(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0x39: AND(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0x21: AND(ReadIndexedIndirectXAddrMode()); break;
                case 0x31: AND(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                case 0x09: ORA(ReadImmediateAddrMode()); break;
                case 0x05: ORA(ReadZeroPageAddrMode()); break;
                case 0x15: ORA(ReadZeroPageXAddrMode()); break;
                case 0x0D: ORA(ReadAbsoluteAddrMode()); break;
                case 0x1D: ORA(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0x19: ORA(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0x01: ORA(ReadIndexedIndirectXAddrMode()); break;
                case 0x11: ORA(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                case 0x49: EOR(ReadImmediateAddrMode()); break;
                case 0x45: EOR(ReadZeroPageAddrMode()); break;
                case 0x55: EOR(ReadZeroPageXAddrMode()); break;
                case 0x4D: EOR(ReadAbsoluteAddrMode()); break;
                case 0x5D: EOR(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0x59: EOR(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0x41: EOR(ReadIndexedIndirectXAddrMode()); break;
                case 0x51: EOR(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                #endregion

                #region Flags and status register

                case 0x38: ReadImpliedMode(); SEC(); break;
                case 0x18: ReadImpliedMode(); CLC(); break;

                case 0x78: ReadImpliedMode(); SEI(); break;
                case 0x58: ReadImpliedMode(); CLI(); break;

                case 0xF8: ReadImpliedMode(); SED(); break;
                case 0xD8: ReadImpliedMode(); CLD(); break;

                case 0xB8: ReadImpliedMode(); CLV(); break;

                #endregion

                #region Tests, branches and jumps

                case 0x4C: JMP(-1, ReadAbsoluteAddrMode()); break;
                case 0x6C: JMP(3, ReadIndirectAddrMode()); break;

                case 0x30: BMI(ReadRelativeAddrMode()); break;
                case 0x10: BPL(ReadRelativeAddrMode()); break;
                case 0x90: BCC(ReadRelativeAddrMode()); break;
                case 0xB0: BCS(ReadRelativeAddrMode()); break;
                case 0xF0: BEQ(ReadRelativeAddrMode()); break;
                case 0xD0: BNE(ReadRelativeAddrMode()); break;
                case 0x70: BVS(ReadRelativeAddrMode()); break;
                case 0x50: BVC(ReadRelativeAddrMode()); break;

                case 0xC9: CMP(ReadImmediateAddrMode()); break;
                case 0xC5: CMP(ReadZeroPageAddrMode()); break;
                case 0xD5: CMP(ReadZeroPageXAddrMode()); break;
                case 0xCD: CMP(ReadAbsoluteAddrMode()); break;
                case 0xDD: CMP(ReadAbsoluteXAddrMode(cycleLessIfSamePage: true)); break;
                case 0xD9: CMP(ReadAbsoluteYAddrMode(cycleLessIfSamePage: true)); break;
                case 0xC1: CMP(ReadIndexedIndirectXAddrMode()); break;
                case 0xD1: CMP(ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: true)); break;

                case 0xE0: CPX(ReadImmediateAddrMode()); break;
                case 0xE4: CPX(ReadZeroPageAddrMode()); break;
                case 0xEC: CPX(ReadAbsoluteAddrMode()); break;

                case 0xC0: CPY(ReadImmediateAddrMode()); break;
                case 0xC4: CPY(ReadZeroPageAddrMode()); break;
                case 0xCC: CPY(ReadAbsoluteAddrMode()); break;

                case 0x24: BIT(ReadZeroPageAddrMode()); break;
                case 0x2C: BIT(ReadAbsoluteAddrMode()); break;

                #endregion

                #region Stack processing

                case 0x20: JSR(ReadAbsoluteAddrMode()); break;
                case 0x60: ReadImpliedMode(); RTS(); break;

                case 0x48: ReadImpliedMode(); PHA(); break;
                case 0x68: ReadImpliedMode(); PLA(); break;

                case 0x9A: ReadImpliedMode(); TXS(); break;
                case 0xBA: ReadImpliedMode(); TSX(); break;

                case 0x08: ReadImpliedMode(); PHP(); break;
                case 0x28: ReadImpliedMode(); PLP(); break;

                #endregion

                #region Interrupts

                case 0x40: ReadImpliedMode(); RTI(); break;
                case 0x00: ReadImpliedMode(); BRK(); break;

                #endregion

                #region Shift and memory modification

                case 0x4A: LSR(ref ReadAccumulatorOperandMode()); break;
                case 0x46: LSR(ReadZeroPageAddrMode()); break;
                case 0x56: LSR(ReadZeroPageXAddrMode()); break;
                case 0x4E: LSR(ReadAbsoluteAddrMode()); break;
                case 0x5E: LSR(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;

                case 0x0A: ASL(ref ReadAccumulatorOperandMode()); break;
                case 0x06: ASL(ReadZeroPageAddrMode()); break;
                case 0x16: ASL(ReadZeroPageXAddrMode()); break;
                case 0x0E: ASL(ReadAbsoluteAddrMode()); break;
                case 0x1E: ASL(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;

                case 0x2A: ROL(ref ReadAccumulatorOperandMode()); break;
                case 0x26: ROL(ReadZeroPageAddrMode()); break;
                case 0x36: ROL(ReadZeroPageXAddrMode()); break;
                case 0x2E: ROL(ReadAbsoluteAddrMode()); break;
                case 0x3E: ROL(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;

                case 0x6A: ROR(ref ReadAccumulatorOperandMode()); break;
                case 0x66: ROR(ReadZeroPageAddrMode()); break;
                case 0x76: ROR(ReadZeroPageXAddrMode()); break;
                case 0x6E: ROR(ReadAbsoluteAddrMode()); break;
                case 0x7E: ROR(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;

                case 0xE6: INC(ReadZeroPageAddrMode()); break;
                case 0xF6: INC(ReadZeroPageXAddrMode()); break;
                case 0xEE: INC(ReadAbsoluteAddrMode()); break;
                case 0xFE: INC(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;

                case 0xE8: ReadImpliedMode(); INX(); break;
                case 0xC8: ReadImpliedMode(); INY(); break;

                case 0xC6: DEC(ReadZeroPageAddrMode()); break;
                case 0xD6: DEC(ReadZeroPageXAddrMode()); break;
                case 0xCE: DEC(ReadAbsoluteAddrMode()); break;
                case 0xDE: DEC(ReadAbsoluteXAddrMode(cycleLessIfSamePage: false)); break;

                case 0xCA: ReadImpliedMode(); DEX(); break;
                case 0x88: ReadImpliedMode(); DEY(); break;

                case 0xEA: ReadImpliedMode(); NOP(); break;

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
                    ReadImpliedMode(); /* KIL */ return true;

                case 0x1A:
                case 0x3A:
                case 0x5A:
                case 0x7A:
                case 0xDA:
                case 0xFA:
                    ReadImpliedMode(); /* Unofficial */ break;

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
                    mem.Read(ReadImmediateAddrMode()); /* Unofficial */ break;

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
                    ReadZeroPageAddrMode(); /* Unofficial */ break;

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
                    ReadZeroPageXAddrMode(); /* Unofficial */ break;

                case 0x97:
                case 0xB7:
                    ReadZeroPageYAddrMode(); /* Unofficial */ break;

                case 0x03:
                case 0x23:
                case 0x43:
                case 0x63:
                case 0x83:
                case 0xA3:
                case 0xC3:
                case 0xE3:
                    ReadIndexedIndirectXAddrMode(); /* Unofficial */ break;

                case 0x13:
                case 0x33:
                case 0x53:
                case 0x73:
                case 0x93:
                case 0xB3:
                case 0xD3:
                case 0xF3:
                    ReadIndirectIndexedYAddrMode(cycleLessIfSamePage: false); /* Unofficial */ break;

                case 0x0F:
                case 0x2F:
                case 0x4F:
                case 0x6F:
                case 0x8F:
                case 0xAF:
                case 0xCF:
                case 0xEF:
                case 0x0C:
                    ReadAbsoluteAddrMode(); /* Unofficial */ break;

                case 0x1F:
                case 0x3F:
                case 0x5F:
                case 0x7F:
                case 0xDF:
                case 0xFF:
                    ReadAbsoluteXAddrMode(cycleLessIfSamePage: false); /* Unofficial */ break;

                case 0x1C:
                case 0x3C:
                case 0x5C:
                case 0x7C:
                case 0x9C:
                case 0xDC:
                case 0xFC:
                    ReadAbsoluteXAddrMode(cycleLessIfSamePage: true); /* Unofficial */ break;

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
                    ReadAbsoluteYAddrMode(cycleLessIfSamePage: false); /* Unofficial */ break;

                #endregion

                default: throw new InvalidOperationException(); // Should not reach if all opcodes listed
            }

            return false;   // Do not halt except for KIL
        }

        private ushort Addr(byte L, byte H, byte offset = 0) => (ushort)(L + (H << 8) + offset);
        private (byte L, byte H) GetAddrBytes(ushort addr) => ((byte)(addr & 0xFF), (byte)(addr >> 8));
        private ushort NextAddrInPage(ushort addr) => (ushort)((addr & 0xFF00) + (byte)(addr + 1));

        private ushort StepNextAddr() => PC++;
        private byte ReadNextByte() => mem.Read(StepNextAddr());
        private ushort ReadNextAddr() => Addr(ReadNextByte(), ReadNextByte());
        private void DummyRead(ushort addr) => mem.Read(addr);                          // dummy read of next byte without advancing

        private void ReadImpliedMode() => DummyRead(PC);

        private ushort ReadImmediateAddrMode() => SpendCyclesFor(2, StepNextAddr());    // assumed to be read by instruction

        private ushort ReadAbsoluteAddrMode() => SpendCyclesFor(4, ReadNextAddr());
        private ushort ReadAbsoluteXAddrMode(bool cycleLessIfSamePage) => SpendCyclesFor(5, ReadNextAddr(), X, cycleLessIfSamePage);
        private ushort ReadAbsoluteYAddrMode(bool cycleLessIfSamePage) => SpendCyclesFor(5, ReadNextAddr(), Y, cycleLessIfSamePage);

        private byte ReadZeroPageAddr(byte offset) => (byte)(ReadNextByte() + offset);
        private byte ReadZeroPageAddrMode() => SpendCyclesFor(3, ReadZeroPageAddr(0));
        private byte ReadZeroPageXAddrMode() => SpendCyclesFor(4, ReadZeroPageAddr(X));
        private byte ReadZeroPageYAddrMode() => SpendCyclesFor(4, ReadZeroPageAddr(Y));

        private ushort FollowIndirectAbsoluteAddrRead(ushort addr) => Addr(mem.Read(addr), mem.Read(NextAddrInPage(addr)));     // 6502 bug: indirect wraps at page
        private ushort FollowIndirectZeroPageAddrRead(byte addr) => Addr(mem.Read(addr), mem.Read((byte)(addr + 1)));

        private ushort ReadIndirectAddrMode() => SpendCyclesFor(2, FollowIndirectAbsoluteAddrRead(ReadNextAddr()));

        private ushort ReadIndexedIndirectXAddrMode() => SpendCyclesFor(6, FollowIndirectZeroPageAddrRead(ReadZeroPageAddr(X)));

        private ushort ReadIndirectIndexedYAddrMode(bool cycleLessIfSamePage) => SpendCyclesFor(6, FollowIndirectZeroPageAddrRead(ReadZeroPageAddr(0)), Y, cycleLessIfSamePage);

        private ushort ReadRelativeAddrMode() => SpendCyclesFor(2, (ushort)((sbyte)ReadNextByte() + PC));

        private ref byte ReadAccumulatorOperandMode()
        {
            DummyRead(PC);
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
            SpendCycles(2);
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

        private void SEC() { SpendCycles(2); C = true; }
        private void CLC() { SpendCycles(2); C = false; }

        private void SEI() { SpendCycles(2); I = true; }
        private void CLI() { SpendCycles(2); I = false; }

        private void SED() { SpendCycles(2); D = true; }
        private void CLD() { SpendCycles(2); D = false; }

        private void CLV() { SpendCycles(2); V = false; }

        #endregion

        #region Tests, branches and jumps

        private void JMP(int cycles, ushort addr) { SpendCycles(cycles); PC = addr; }

        private void BMI(ushort addr) => Bcd(N, addr);
        private void BPL(ushort addr) => Bcd(!N, addr);
        private void BCC(ushort addr) => Bcd(!C, addr);
        private void BCS(ushort addr) => Bcd(C, addr);
        private void BEQ(ushort addr) => Bcd(Z, addr);
        private void BNE(ushort addr) => Bcd(!Z, addr);
        private void BVS(ushort addr) => Bcd(V, addr);
        private void BVC(ushort addr) => Bcd(!V, addr);
        private void Bcd(bool cond, ushort addr)
        {
            if (cond)
            {
                if (GetAddrBytes(addr).H == GetAddrBytes(PC).H)
                    SpendCycles(1);
                else
                    SpendCycles(2);

                PC = addr;
            }
        }

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
            (byte L, byte H) = GetAddrBytes(addr);
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
            SpendCycles(2);
            PushAddr((ushort)(PC - 1));
            PC = addr;
        }

        private void RTS() { SpendCycles(6); PC = (ushort)(PullAddr() + 1); }

        private void PHA() { SpendCycles(3); Push(A); }
        private void PLA()
        {
            SpendCycles(4);
            A = Pull();
            SetZN(A);
        }

        private void TXS() { SpendCycles(2); S = X; }       //Writes to S do not change status bits
        private void TSX() => Tsd(ref S, ref X);

        private void PHP() { SpendCycles(3); Push(P.WithBit(BBit, true)); }
        private void PLP() { SpendCycles(4); P = Pull(); }

        #endregion

        #region Interrupts

        private void RTI()
        {
            SpendCycles(6);
            P = Pull();
            PC = PullAddr();
        }

        public void NMI() => Interrupt(NMIAddr, B: false);
        public void IRQ() => Interrupt(IRQAddr, B: false);
        public void BRK() => Interrupt(IRQAddr, B: true);

        private void Interrupt(ushort addr, bool B)
        {
            SpendCycles(7);
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
        private void LSR(byte addr) => LSR((ushort)addr);
        private void LSR(ref byte r)
        {
            SpendCycles(2);
            C = r.IsBitSet(0);
            r = (byte)(r >> 1);
            SetZN(r);
        }

        private void ASL(ushort addr)
        {
            byte M = mem.Read(addr);
            ASL(ref M);
            mem.Write(addr, M);
        }
        private void ASL(byte addr) => ASL((ushort)addr);
        private void ASL(ref byte r)
        {
            SpendCycles(2);
            C = r.IsBitSet(7);
            r = (byte)(r << 1);
            SetZN(r);
        }

        private void ROL(ushort addr)
        {
            byte M = mem.Read(addr);
            ROL(ref M);
            mem.Write(addr, M);
        }
        private void ROL(byte addr) => ROL((ushort)addr);
        private void ROL(ref byte r)
        {
            SpendCycles(2);
            bool oldC = C;
            C = r.IsBitSet(7);
            r = ((byte)(r << 1)).WithBit(0, oldC);
            SetZN(r);
        }

        private void ROR(ushort addr)
        {
            byte M = mem.Read(addr);
            ROR(ref M);
            mem.Write(addr, M);
        }
        private void ROR(byte addr) => ROR((ushort)addr);
        private void ROR(ref byte r)
        {
            SpendCycles(2);
            bool oldC = C;
            C = r.IsBitSet(0);
            r = ((byte)(r >> 1)).WithBit(7, oldC);
            SetZN(r);
        }

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
            SpendCycles(2);
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
            SpendCycles(2);
            r--;
            SetZN(r);
        }

        private void NOP() => SpendCycles(2);

        #endregion

        public string Disassemble(ushort addr)
        {
            byte inst = mem.Peek(addr);

            switch (inst)
            {
                #region Accumulator and arithmetic

                case 0xA9: return DisassembleImmediate(" LDA", addr);
                case 0xA5: return DisassembleZeroPage(" LDA", addr) + DisassembleZeroPageValue(addr);
                case 0xB5: return DisassembleZeroPageX(" LDA", addr) + DisassembleZeroPageXValue(addr);
                case 0xAD: return DisassembleAbsolute(" LDA", addr) + DisassembleAbsoluteValue(addr);
                case 0xBD: return DisassembleAbsoluteX(" LDA", addr) + DisassembleAbsoluteXValue(addr);
                case 0xB9: return DisassembleAbsoluteY(" LDA", addr) + DisassembleAbsoluteYValue(addr);
                case 0xA1: return DisassembleIndexedIndirectX(" LDA", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0xB1: return DisassembleIndirectIndexedY(" LDA", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0xA2: return DisassembleImmediate(" LDX", addr);
                case 0xA6: return DisassembleZeroPage(" LDX", addr) + DisassembleZeroPageValue(addr);
                case 0xB6: return DisassembleZeroPageY(" LDX", addr) + DisassembleZeroPageYValue(addr);
                case 0xAE: return DisassembleAbsolute(" LDX", addr) + DisassembleAbsoluteValue(addr);
                case 0xBE: return DisassembleAbsoluteY(" LDX", addr) + DisassembleAbsoluteYValue(addr);

                case 0xA0: return DisassembleImmediate(" LDY", addr);
                case 0xA4: return DisassembleZeroPage(" LDY", addr) + DisassembleZeroPageValue(addr);
                case 0xB4: return DisassembleZeroPageX(" LDY", addr) + DisassembleZeroPageXValue(addr);
                case 0xAC: return DisassembleAbsolute(" LDY", addr) + DisassembleAbsoluteValue(addr);
                case 0xBC: return DisassembleAbsoluteX(" LDY", addr) + DisassembleAbsoluteXValue(addr);

                case 0x85: return DisassembleZeroPage(" STA", addr) + DisassembleZeroPageValue(addr);
                case 0x95: return DisassembleZeroPageX(" STA", addr) + DisassembleZeroPageXValue(addr);
                case 0x8D: return DisassembleAbsolute(" STA", addr) + DisassembleAbsoluteValue(addr);
                case 0x9D: return DisassembleAbsoluteX(" STA", addr) + DisassembleAbsoluteXValue(addr);
                case 0x99: return DisassembleAbsoluteY(" STA", addr) + DisassembleAbsoluteYValue(addr);
                case 0x81: return DisassembleIndexedIndirectX(" STA", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0x91: return DisassembleIndirectIndexedY(" STA", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0x86: return DisassembleZeroPage(" STX", addr) + DisassembleZeroPageValue(addr);
                case 0x96: return DisassembleZeroPageY(" STX", addr) + DisassembleZeroPageYValue(addr);
                case 0x8E: return DisassembleAbsolute(" STX", addr) + DisassembleAbsoluteValue(addr);

                case 0x84: return DisassembleZeroPage(" STY", addr) + DisassembleZeroPageValue(addr);
                case 0x94: return DisassembleZeroPageX(" STY", addr) + DisassembleZeroPageXValue(addr);
                case 0x8C: return DisassembleAbsolute(" STY", addr) + DisassembleAbsoluteValue(addr);

                case 0xAA: return DisassembleImplied(" TAX", addr);
                case 0x8A: return DisassembleImplied(" TXA", addr);
                case 0xA8: return DisassembleImplied(" TAY", addr);
                case 0x98: return DisassembleImplied(" TYA", addr);

                case 0x69: return DisassembleImmediate(" ADC", addr);
                case 0x65: return DisassembleZeroPage(" ADC", addr) + DisassembleZeroPageValue(addr);
                case 0x75: return DisassembleZeroPageX(" ADC", addr) + DisassembleZeroPageXValue(addr);
                case 0x6D: return DisassembleAbsolute(" ADC", addr) + DisassembleAbsoluteValue(addr);
                case 0x7D: return DisassembleAbsoluteX(" ADC", addr) + DisassembleAbsoluteXValue(addr);
                case 0x79: return DisassembleAbsoluteY(" ADC", addr) + DisassembleAbsoluteYValue(addr);
                case 0x61: return DisassembleIndexedIndirectX(" ADC", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0x71: return DisassembleIndirectIndexedY(" ADC", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0xE9: return DisassembleImmediate(" SBC", addr);
                case 0xE5: return DisassembleZeroPage(" SBC", addr) + DisassembleZeroPageValue(addr);
                case 0xF5: return DisassembleZeroPageX(" SBC", addr) + DisassembleZeroPageXValue(addr);
                case 0xED: return DisassembleAbsolute(" SBC", addr) + DisassembleAbsoluteValue(addr);
                case 0xFD: return DisassembleAbsoluteX(" SBC", addr) + DisassembleAbsoluteXValue(addr);
                case 0xF9: return DisassembleAbsoluteY(" SBC", addr) + DisassembleAbsoluteYValue(addr);
                case 0xE1: return DisassembleIndexedIndirectX(" SBC", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0xF1: return DisassembleIndirectIndexedY(" SBC", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0x29: return DisassembleImmediate(" AND", addr);
                case 0x25: return DisassembleZeroPage(" AND", addr) + DisassembleZeroPageValue(addr);
                case 0x35: return DisassembleZeroPageX(" AND", addr) + DisassembleZeroPageXValue(addr);
                case 0x2D: return DisassembleAbsolute(" AND", addr) + DisassembleAbsoluteValue(addr);
                case 0x3D: return DisassembleAbsoluteX(" AND", addr) + DisassembleAbsoluteXValue(addr);
                case 0x39: return DisassembleAbsoluteY(" AND", addr) + DisassembleAbsoluteYValue(addr);
                case 0x21: return DisassembleIndexedIndirectX(" AND", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0x31: return DisassembleIndirectIndexedY(" AND", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0x09: return DisassembleImmediate(" ORA", addr);
                case 0x05: return DisassembleZeroPage(" ORA", addr) + DisassembleZeroPageValue(addr);
                case 0x15: return DisassembleZeroPageX(" ORA", addr) + DisassembleZeroPageXValue(addr);
                case 0x0D: return DisassembleAbsolute(" ORA", addr) + DisassembleAbsoluteValue(addr);
                case 0x1D: return DisassembleAbsoluteX(" ORA", addr) + DisassembleAbsoluteXValue(addr);
                case 0x19: return DisassembleAbsoluteY(" ORA", addr) + DisassembleAbsoluteYValue(addr);
                case 0x01: return DisassembleIndexedIndirectX(" ORA", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0x11: return DisassembleIndirectIndexedY(" ORA", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0x49: return DisassembleImmediate(" EOR", addr);
                case 0x45: return DisassembleZeroPage(" EOR", addr) + DisassembleZeroPageValue(addr);
                case 0x55: return DisassembleZeroPageX(" EOR", addr) + DisassembleZeroPageXValue(addr);
                case 0x4D: return DisassembleAbsolute(" EOR", addr) + DisassembleAbsoluteValue(addr);
                case 0x5D: return DisassembleAbsoluteX(" EOR", addr) + DisassembleAbsoluteXValue(addr);
                case 0x59: return DisassembleAbsoluteY(" EOR", addr) + DisassembleAbsoluteYValue(addr);
                case 0x41: return DisassembleIndexedIndirectX(" EOR", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0x51: return DisassembleIndirectIndexedY(" EOR", addr) + DisassembleIndirectIndexedYValue(addr);

                #endregion

                #region Flags and status register

                case 0x38: return DisassembleImplied(" SEC", addr);
                case 0x18: return DisassembleImplied(" CLC", addr);

                case 0x78: return DisassembleImplied(" SEI", addr);
                case 0x58: return DisassembleImplied(" CLI", addr);

                case 0xF8: return DisassembleImplied(" SED", addr);
                case 0xD8: return DisassembleImplied(" CLD", addr);

                case 0xB8: return DisassembleImplied(" CLV", addr);

                #endregion

                #region Tests, branches and jumps

                case 0x4C: return DisassembleAbsolute(" JMP", addr);
                case 0x6C: return DisassembleIndirect(" JMP", addr) + DisassembleIndirectValue(addr);

                case 0x30: return DisassembleRelative(" BMI", addr);
                case 0x10: return DisassembleRelative(" BPL", addr);
                case 0x90: return DisassembleRelative(" BCC", addr);
                case 0xB0: return DisassembleRelative(" BCS", addr);
                case 0xF0: return DisassembleRelative(" BEQ", addr);
                case 0xD0: return DisassembleRelative(" BNE", addr);
                case 0x70: return DisassembleRelative(" BVS", addr);
                case 0x50: return DisassembleRelative(" BVC", addr);

                case 0xC9: return DisassembleImmediate(" CMP", addr);
                case 0xC5: return DisassembleZeroPage(" CMP", addr) + DisassembleZeroPageValue(addr);
                case 0xD5: return DisassembleZeroPageX(" CMP", addr) + DisassembleZeroPageXValue(addr);
                case 0xCD: return DisassembleAbsolute(" CMP", addr) + DisassembleAbsoluteValue(addr);
                case 0xDD: return DisassembleAbsoluteX(" CMP", addr) + DisassembleAbsoluteXValue(addr);
                case 0xD9: return DisassembleAbsoluteY(" CMP", addr) + DisassembleAbsoluteYValue(addr);
                case 0xC1: return DisassembleIndexedIndirectX(" CMP", addr) + DisassembleIndexedIndirectXValue(addr);
                case 0xD1: return DisassembleIndirectIndexedY(" CMP", addr) + DisassembleIndirectIndexedYValue(addr);

                case 0xE0: return DisassembleImmediate(" CPX", addr);
                case 0xE4: return DisassembleZeroPage(" CPX", addr) + DisassembleZeroPageValue(addr);
                case 0xEC: return DisassembleAbsolute(" CPX", addr) + DisassembleAbsoluteValue(addr);

                case 0xC0: return DisassembleImmediate(" CPY", addr);
                case 0xC4: return DisassembleZeroPage(" CPY", addr) + DisassembleZeroPageValue(addr);
                case 0xCC: return DisassembleAbsolute(" CPY", addr) + DisassembleAbsoluteValue(addr);

                case 0x24: return DisassembleZeroPage(" BIT", addr) + DisassembleZeroPageValue(addr);
                case 0x2C: return DisassembleAbsolute(" BIT", addr) + DisassembleAbsoluteValue(addr);

                #endregion

                #region Stack processing

                case 0x20: return DisassembleAbsolute(" JSR", addr);
                case 0x60: return DisassembleImplied(" RTS", addr);

                case 0x48: return DisassembleImplied(" PHA", addr);
                case 0x68: return DisassembleImplied(" PLA", addr);

                case 0x9A: return DisassembleImplied(" TXS", addr);
                case 0xBA: return DisassembleImplied(" TSX", addr);

                case 0x08: return DisassembleImplied(" PHP", addr);
                case 0x28: return DisassembleImplied(" PLP", addr);

                #endregion

                #region Interrupts

                case 0x40: return DisassembleImplied(" RTI", addr);
                case 0x00: return DisassembleImplied(" BRK", addr);

                #endregion

                #region Shift and memory modification

                case 0x4A: return DisassembleAccumulator(" LSR", addr);
                case 0x46: return DisassembleZeroPage(" LSR", addr) + DisassembleZeroPageValue(addr);
                case 0x56: return DisassembleZeroPageX(" LSR", addr) + DisassembleZeroPageXValue(addr);
                case 0x4E: return DisassembleAbsolute(" LSR", addr) + DisassembleAbsoluteValue(addr);
                case 0x5E: return DisassembleAbsoluteX(" LSR", addr) + DisassembleAbsoluteXValue(addr);

                case 0x0A: return DisassembleAccumulator(" ASL", addr);
                case 0x06: return DisassembleZeroPage(" ASL", addr) + DisassembleZeroPageValue(addr);
                case 0x16: return DisassembleZeroPageX(" ASL", addr) + DisassembleZeroPageXValue(addr);
                case 0x0E: return DisassembleAbsolute(" ASL", addr) + DisassembleAbsoluteValue(addr);
                case 0x1E: return DisassembleAbsoluteX(" ASL", addr) + DisassembleAbsoluteXValue(addr);

                case 0x2A: return DisassembleAccumulator(" ROL", addr);
                case 0x26: return DisassembleZeroPage(" ROL", addr) + DisassembleZeroPageValue(addr);
                case 0x36: return DisassembleZeroPageX(" ROL", addr) + DisassembleZeroPageXValue(addr);
                case 0x2E: return DisassembleAbsolute(" ROL", addr) + DisassembleAbsoluteValue(addr);
                case 0x3E: return DisassembleAbsoluteX(" ROL", addr) + DisassembleAbsoluteXValue(addr);

                case 0x6A: return DisassembleAccumulator(" ROR", addr);
                case 0x66: return DisassembleZeroPage(" ROR", addr) + DisassembleZeroPageValue(addr);
                case 0x76: return DisassembleZeroPageX(" ROR", addr) + DisassembleZeroPageXValue(addr);
                case 0x6E: return DisassembleAbsolute(" ROR", addr) + DisassembleAbsoluteValue(addr);
                case 0x7E: return DisassembleAbsoluteX(" ROR", addr) + DisassembleAbsoluteXValue(addr);

                case 0xE6: return DisassembleZeroPage(" INC", addr) + DisassembleZeroPageValue(addr);
                case 0xF6: return DisassembleZeroPageX(" INC", addr) + DisassembleZeroPageXValue(addr);
                case 0xEE: return DisassembleAbsolute(" INC", addr) + DisassembleAbsoluteValue(addr);
                case 0xFE: return DisassembleAbsoluteX(" INC", addr) + DisassembleAbsoluteXValue(addr);

                case 0xE8: return DisassembleImplied(" INX", addr);
                case 0xC8: return DisassembleImplied(" INY", addr);

                case 0xC6: return DisassembleZeroPage(" DEC", addr) + DisassembleZeroPageValue(addr);
                case 0xD6: return DisassembleZeroPageX(" DEC", addr) + DisassembleZeroPageXValue(addr);
                case 0xCE: return DisassembleAbsolute(" DEC", addr) + DisassembleAbsoluteValue(addr);
                case 0xDE: return DisassembleAbsoluteX(" DEC", addr) + DisassembleAbsoluteXValue(addr);

                case 0xCA: return DisassembleImplied(" DEX", addr);
                case 0x88: return DisassembleImplied(" DEY", addr);

                case 0xEA: return DisassembleImplied(" NOP", addr);

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
                    return DisassembleImplied(" KIL", addr);

                case 0x1A:
                case 0x3A:
                case 0x5A:
                case 0x7A:
                case 0xDA:
                case 0xFA:
                    return DisassembleImplied("****", addr);

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
                    return DisassembleImmediate("****", addr);

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
                    return DisassembleZeroPage("****", addr);

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
                    return DisassembleZeroPageX("****", addr);

                case 0x97:
                case 0xB7:
                    return DisassembleZeroPageY("****", addr);

                case 0x03:
                case 0x23:
                case 0x43:
                case 0x63:
                case 0x83:
                case 0xA3:
                case 0xC3:
                case 0xE3:
                    return DisassembleIndexedIndirectX("****", addr);

                case 0x13:
                case 0x33:
                case 0x53:
                case 0x73:
                case 0x93:
                case 0xB3:
                case 0xD3:
                case 0xF3:
                    return DisassembleIndirectIndexedY("****", addr);

                case 0x0F:
                case 0x2F:
                case 0x4F:
                case 0x6F:
                case 0x8F:
                case 0xAF:
                case 0xCF:
                case 0xEF:
                case 0x0C:
                    return DisassembleAbsolute("****", addr);

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
                    return DisassembleAbsoluteX("****", addr);

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
                    return DisassembleAbsoluteY("****", addr);

                #endregion

                default: throw new InvalidOperationException(); // Should not reach if all opcodes listed
            }
        }

        private string PeekByte(ushort addr) => $"{mem.Peek(addr, 0):X2}      ";
        private string Peek2Bytes(ushort addr) => $"{mem.Peek(addr, 0):X2} {mem.Peek(PC, 1):X2}   ";
        private string Peek3Bytes(ushort addr) => $"{mem.Peek(addr, 0):X2} {mem.Peek(PC, 1):X2} {mem.Peek(PC, 2):X2}";

        private byte PeekImmediateOperand(ushort instAddr) => mem.Peek(instAddr, 1);
        private ushort PeekAbsoluteOperand(ushort instAddr) => Addr(mem.Peek(instAddr, 1), mem.Peek(instAddr, 2));
        private byte PeekZeroPageOperand(ushort instAddr) => mem.Peek(instAddr, 1);
        private ushort PeekRelativeOperand(ushort instAddr) => (ushort)((instAddr + 2) + (sbyte)mem.Peek(instAddr, 1));

        private string DisassembleImplied(string inst, ushort instAddr) => $"{PeekByte(instAddr)} {inst}";
        private string DisassembleImmediate(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} #${PeekImmediateOperand(instAddr):X2}";
        private string DisassembleAbsolute(string inst, ushort instAddr) => $"{Peek3Bytes(instAddr)} {inst} ${PeekAbsoluteOperand(instAddr):X4}";
        private string DisassembleAbsoluteX(string inst, ushort instAddr) => $"{Peek3Bytes(instAddr)} {inst} ${PeekAbsoluteOperand(instAddr):X4},X";
        private string DisassembleAbsoluteY(string inst, ushort instAddr) => $"{Peek3Bytes(instAddr)} {inst} ${PeekAbsoluteOperand(instAddr):X4},Y";
        private string DisassembleZeroPage(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} ${PeekZeroPageOperand(instAddr):X2}";
        private string DisassembleZeroPageX(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} ${PeekZeroPageOperand(instAddr):X2},X";
        private string DisassembleZeroPageY(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} ${PeekZeroPageOperand(instAddr):X2},Y";
        private string DisassembleIndirect(string inst, ushort instAddr) => $"{Peek3Bytes(instAddr)} {inst} (${PeekAbsoluteOperand(instAddr):X4})";
        private string DisassembleIndexedIndirectX(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} (${PeekZeroPageOperand(instAddr):X2},X)";
        private string DisassembleIndirectIndexedY(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} (${PeekZeroPageOperand(instAddr):X2}),Y";
        private string DisassembleRelative(string inst, ushort instAddr) => $"{Peek2Bytes(instAddr)} {inst} ${PeekRelativeOperand(instAddr):X4}";
        private string DisassembleAccumulator(string inst, ushort instAddr) => $"{PeekByte(instAddr)} {inst} A";

        private ushort FollowIndirectAbsoluteAddrPeek(ushort addr) => Addr(mem.Peek(addr), mem.Peek(NextAddrInPage(addr)));   // 6502 bug: indirect wraps at page
        private ushort FollowIndirectZeroPageAddrPeek(byte addr) => Addr(mem.Peek(addr), mem.Peek((byte)(addr + 1)));

        private string DisassembleValue(ushort addr) => $" = {mem.Peek(addr):X2}";
        private string DisassembleAbsoluteAddr(ushort addr) => $" = {FollowIndirectAbsoluteAddrPeek(addr):X4}";
        private string DisassembleZeroPageAddr(byte addr) => $" = {FollowIndirectZeroPageAddrPeek(addr):X4}";
        private string DisassembleZeroPageAddrValue(byte addr) => $" = {mem.Peek(FollowIndirectZeroPageAddrPeek(addr)):X2}";
        private string DisassembleIndexedValue(ushort addr) => $" @ {addr:X4}{DisassembleValue(addr)}";

        private string DisassembleAbsoluteValue(ushort instAddr) => DisassembleValue(PeekAbsoluteOperand(instAddr));
        private string DisassembleAbsoluteXValue(ushort instAddr) => DisassembleIndexedValue((ushort)(PeekAbsoluteOperand(instAddr) + X));
        private string DisassembleAbsoluteYValue(ushort instAddr) => DisassembleIndexedValue((ushort)(PeekAbsoluteOperand(instAddr) + Y));
        private string DisassembleZeroPageValue(ushort instAddr) => DisassembleValue(PeekZeroPageOperand(instAddr));
        private string DisassembleZeroPageIndexedValue(byte addr) => $" @ {addr:X2}{DisassembleValue(addr)}";
        private string DisassembleZeroPageXValue(ushort instAddr) => DisassembleZeroPageIndexedValue((byte)(PeekZeroPageOperand(instAddr) + X));
        private string DisassembleZeroPageYValue(ushort instAddr) => DisassembleZeroPageIndexedValue((byte)(PeekZeroPageOperand(instAddr) + Y));
        private string DisassembleIndirectValue(ushort instAddr) => DisassembleAbsoluteAddr(PeekAbsoluteOperand(instAddr));
        private string DisassembleIndexedIndirectValue(byte addr) => $" @ {addr:X2}{DisassembleZeroPageAddr(addr)}{DisassembleZeroPageAddrValue(addr)}";
        private string DisassembleIndexedIndirectXValue(ushort instAddr) => DisassembleIndexedIndirectValue((byte)(PeekZeroPageOperand(instAddr) + X));
        private string DisassembleIndirectIndexedValue(byte addr, int offset) => $"{DisassembleZeroPageAddr(addr)}{DisassembleIndexedValue((ushort)(FollowIndirectZeroPageAddrPeek(addr) + offset))}";
        private string DisassembleIndirectIndexedYValue(ushort instAddr) => DisassembleIndirectIndexedValue(PeekZeroPageOperand(instAddr), Y);
    }

    static class BitHelpers
    {
        public static byte AsBit(this bool b, int bit = 0) => b ? (byte)(1 << bit) : (byte)0;
        public static byte AsBits(this (bool b7, bool b6, bool b5, bool b4, bool b3, bool b2, bool b1, bool b0) b) =>
            (byte)(b.b7.AsBit(7) + b.b6.AsBit(6) + b.b5.AsBit(5) + b.b4.AsBit(4) +
                   b.b3.AsBit(3) + b.b2.AsBit(2) + b.b1.AsBit(1) + b.b0.AsBit(0));

        public static string AsLetter(this bool b, string letter) => b ? letter.ToUpperInvariant() : letter.ToLowerInvariant();

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