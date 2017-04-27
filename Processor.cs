using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using System.Diagnostics;
using System.Collections.Generic;

namespace PIC_Simulator
{
    class Processor
    {
        internal Collection<ProcessorInstruction> ProgramMemory = new Collection<ProcessorInstruction>();
        private MemoryController memController;
        private ushort pc;
        private byte wreg;
        private bool twoCycles;
        private Stack<ushort> Stack = new Stack<ushort>();

        /// <summary>
        /// Der interne Takt für (!)µC-Zyklen
        /// Beachten: f_PIC = f_Quarz / 4, also 4 Quarz Schwingungen 
        /// ergeben einen µC-Takt
        /// </summary>
        public DispatcherTimer Clock = new DispatcherTimer();

        private ISourcecodeViewInterface ViewInterface;

        #region Steuerlogik

        public Processor(ISourcecodeViewInterface viewInterface)
        {
            this.ViewInterface = viewInterface;
            Clock.Tick += Clock_Tick;
            this.Clock.Interval = new TimeSpan(20); // 20 Ticks ^= 2000 ns ^= 2MHz Quarzfrequenz
            this.memController = new MemoryController();
            this.twoCycles = false;
        }

        private void Clock_Tick(object sender, object e)
        {
            if (this.twoCycles)
                this.twoCycles = false;
            else
                this.Step();
        }

        public void Step()
        {
            this.Decode();
            this.pc++;
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[pc].LineNumber - 1);
        }

        internal void Reset()
        {
            this.pc = 0;
            this.wreg = 0;
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[0].LineNumber - 1);
        }

        /// <summary>
        /// Diese Routine dekodiert den aktuellen Befehl und ruft die entsprechende
        /// Subroutine zur Ausführung des Maschinenbefehls auf.
        /// Zur Dekodierung werden die pro Maschinenbefehl ausschlaggebenden Bits maskiert
        /// und gemäß Datenblatt auf ihren Wert geprüft.
        /// </summary>
        private void Decode()
        {
            ushort opcode = this.ProgramMemory[pc].Opcode;

            if ((opcode & 0xFF00) == 0x0700)
                this.addwf();

            if ((opcode & 0xFF00) == 0x0500)
                this.andwf();

            if ((opcode & 0xFF80) == 0x0180)
                this.clrf();

            if ((opcode & 0xFF80) == 0x0100)
                this.clrw();

            if ((opcode & 0xFF00) == 0x0900)
                this.comf();

            if ((opcode & 0xFF00) == 0x0300)
                this.decf();

            if ((opcode & 0xFF00) == 0x0B00)
                this.decfsz();

            if ((opcode & 0xFF00) == 0x0A00)
                this.incf();

            if ((opcode & 0xFF00) == 0x0F00)
                this.incfsz();

            if ((opcode & 0xFF00) == 0x0400)
                this.iorwf();

            if ((opcode & 0xFF00) == 0x0800)
                this.movf();

            if ((opcode & 0xFF80) == 0x0080)
                this.movwf();

            if ((opcode & 0xFF9F) == 0x0000)
                this.pc++;

            if ((opcode & 0xFF00) == 0x0E00)
                this.rlf();

            if ((opcode & 0xFF00) == 0x0C00)
                this.rrf();

            if ((opcode & 0xFF00) == 0x0200)
                this.subwf();

            if ((opcode & 0xFF00) == 0x0E00)
                this.swapf();

            if ((opcode & 0xFF00) == 0x0600)
                this.xorwf();

            //---

            if ((opcode & 0xFC00) == 0x1000)
                this.bcf();

            if ((opcode & 0xFC00) == 0x1400)
                this.bsf();

            if ((opcode & 0xFC00) == 0x1800)
                this.btfsc();

            if ((opcode & 0xFC00) == 0x1C00)
                this.btfss();

            //---

            if ((opcode & 0xFE00) == 0x3E00)
                this.addlw();

            if ((opcode & 0xFF00) == 0x3900)
                this.andlw();

            if ((opcode & 0xF800) == 0x2000)
                this.call();

            if ((opcode & 0xFFFF) == 0x0064)
                this.clrwdt();

            if ((opcode & 0xF800) == 0x2800)
                this.goto_f();

            if ((opcode & 0xFF00) == 0x3800)
                this.iorlw();

            if ((opcode & 0xFC00) == 0x3000)
                this.movlw();

            if ((opcode & 0xFFFF) == 0x0009)
                this.retfie();

            if ((opcode & 0xFC00) == 0x3400)
                this.retlw();

            if ((opcode & 0xFFFF) == 0x0008)
                this.return_f();

            if ((opcode & 0xFFFF) == 0x0063)
                this.sleep();

            if ((opcode & 0xFF00) == 0x3C00)
                this.sublw();

            if ((opcode & 0xFF00) == 0x3A00)
                this.xorlw();
        }

        #endregion

        #region Maschinenbefehle

#pragma warning disable IDE1006 // Benennungsstile (Namen müssen mit Großbuchstaben anfangen)
        private void addwf()
        {
            throw new NotImplementedException();
        }

        private void andwf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(this.wreg & value);

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.wreg = result;

            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();
        }

        private void clrf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            this.memController.SetFile(address, 0);
        }

        private void clrw()
        {
            this.wreg = 0;
            this.memController.SetZeroFlag();
        }

        private void comf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(~value);

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.wreg = result;
        }

        private void decf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            value--;

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.wreg = value;

            if (value == 0)
                this.memController.SetZeroFlag();
        }

        private void decfsz()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            value--;

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.wreg = value;

            if (value == 0)
            {
                this.twoCycles = true;
                this.pc++;
            }
        }

        private void incf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = value++;

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.wreg = result;

            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();
        }

        private void incfsz()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = value++;

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.wreg = result;

            if (result == 0)
            {
                this.twoCycles = true;
                this.pc++;  //Programmcounter hochzaehlen, um naechsten Befehl zu ueberspringen
            }
        }

        private void iorwf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(value | this.wreg);

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.wreg = result;

            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();
        }

        private void movf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            bool destination = ((this.ProgramMemory[pc].Opcode & 0x0080) == 0x0080);
            byte value = this.memController.GetFile(address);

            // if d == 0, value von f in w-register schreiben, 
            // andernfalls f in f schreiben (redundant) 
            if (!destination)
                this.wreg = value;

            if (value == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();
        }

        private void movwf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            this.memController.SetFile(address, this.wreg);
        }

        private void rlf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            ushort value = (ushort)this.memController.GetFile(address);

            value = (ushort)(value << 1);
            byte carry = this.memController.GetBit(0x03, 0);
            byte newCarry = (byte)((value & 0x0100) >> 8);
            byte newValue = (byte)(value + carry);

            this.memController.SetFile(address, newValue);
            if (newCarry == 1)
                this.memController.SetBit(0x03, 0);
            else
                this.memController.ClearBit(0x03, 0);
        }

        private void rrf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);

            byte carry = this.memController.GetBit(0x03, 0);
            byte newCarry = (byte)(value & 0x01);
            value = (byte)((value >> 1) + (carry << 7));

            this.memController.SetFile(address, value);
            if (newCarry == 1)
                this.memController.SetBit(0x03, 0);
            else
                this.memController.ClearBit(0x03, 0);
        }

        private void subwf()
        {
            throw new NotImplementedException();
        }

        private void swapf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);
            byte lowerNibbles = (byte)(value & 0x0F);
            byte upperNibbles = (byte)((value & 0xF0) >> 4);

            value = (byte)((lowerNibbles << 4) + upperNibbles);

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.wreg = value;
        }

        private void xorwf()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte value = this.memController.GetFile(address);

            byte result = (byte)(value ^ this.wreg);

            if ((this.ProgramMemory[pc].Opcode & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.wreg = result;
        }

        private void bcf()
        {
            byte bit = (byte)((this.ProgramMemory[pc].Opcode & 0x0380) >> 7);
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            this.memController.ClearBit(address, bit);
        }

        private void bsf()
        {
            byte bit = (byte)((this.ProgramMemory[pc].Opcode & 0x0380) >> 7);
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            this.memController.SetBit(address, bit);
        }

        private void btfsc()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte bit = (byte)((this.ProgramMemory[pc].Opcode & 0x0380) >> 7);
            byte result = this.memController.GetBit(address, bit);

            if (result == 0)
            {
                this.twoCycles = true;
                this.pc++;
            }
        }

        private void btfss()
        {
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x007F);
            byte bit = (byte)((this.ProgramMemory[pc].Opcode & 0x0380) >> 7);
            byte result = this.memController.GetBit(address, bit);

            if (result > 0)
            {
                this.twoCycles = true;
                this.pc++;
            }
        }

        private void addlw()
        {
            throw new NotImplementedException();
        }

        private void andlw()
        {
            byte literal = (byte)(this.ProgramMemory[pc].Opcode & 0x00FF);
            this.wreg &= literal;

            if (this.wreg == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();
        }

        private void call()
        {
            // TODO two cycles
            // Dokumentation sagt PCLATH<4:3> nach PC<12:11>
            // Dadurch wird Bit 2 komplett übergangen.
            ushort pclath = (ushort)((this.memController.GetPC() & 0x1800) >> 1);
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x07FF);
            this.Stack.Push((ushort)(this.pc + 1));
            this.memController.SetPC((ushort)(address + pclath));
        }

        private void clrwdt()
        {
            // TO und PD Bits im Statusregister auf 1 Setzen
            this.memController.SetBit(0x03, 4);
            this.memController.SetBit(0x03, 3);

            //TODO WDT auf 0x00 setzen
            //TODO WDT prescaler auf 0 setzen
        }

        private void goto_f()
        {
            //TODO two cycles
            ushort pclath = (ushort)((this.memController.GetPC() & 0x1800) >> 1);
            ushort address = (ushort)(this.ProgramMemory[pc].Opcode & 0x07FF);
            this.memController.SetPC((ushort)(address + pclath));
        }

        private void iorlw()
        {
            byte literal = (byte)(this.ProgramMemory[pc].Opcode & 0x00FF);
            this.wreg = (byte)(literal | this.wreg);

            if (this.wreg == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();
        }

        private void movlw()
        {
            byte literal = (byte)(this.ProgramMemory[pc].Opcode & 0x00FF);
            this.wreg = literal;
        }

        private void retfie()
        {
            throw new NotImplementedException();
        }

        private void retlw()
        {
            throw new NotImplementedException();
        }

        private void return_f()
        {
            throw new NotImplementedException();
        }

        private void sleep()
        {
            throw new NotImplementedException();
        }

        private void sublw()
        {
            throw new NotImplementedException();
        }

        private void xorlw()
        {
            byte literal = (byte)(this.ProgramMemory[pc].Opcode & 0x00FF);
            this.wreg = (byte)(literal ^ this.wreg);

            if (this.wreg == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

        }
#pragma warning restore IDE1006 // Benennungsstile
        #endregion
    }

    interface ISourcecodeViewInterface
    {
        void SetCurrentSourcecodeLine(int line);
    }


    internal class MemoryController
    {
        internal ObservableCollection<byte> Memory = new ObservableCollection<byte>();
        //TODO Low Order 8 Bits des program counters sind an Adresse 0x02 (PCL)

        internal MemoryController()
        {
            InitializeMemory();
        }

        internal byte GetFile(ushort address)
        {
            int index = DecodeAddress(address);
            return this.Memory[index];
        }

        internal void SetFile(ushort address, byte value)
        {
            int index = DecodeAddress(address);
            this.Memory[index] = value;
        }

        internal void SetZeroFlag()
        {
            // Das Zero Bit befindet sich als zweites Bit an Speicheradresse 0x03
            this.SetBit(0x03, 2);
        }

        internal void ClearZeroFlag()
        {
            this.ClearBit(0x03, 2);
        }

        internal byte GetBit(ushort address, byte bit)
        {
            return (byte)((this.GetFile(address) & (1 << bit)) >> bit);
        }

        internal void SetBit(ushort address, byte bit)
        {
            byte value = this.GetFile(address);
            value |= (byte)(1 << bit);
            this.SetFile(address, value);
        }

        internal void ClearBit(ushort address, byte bit)
        {
            byte value = this.GetFile(address);
            value &= (byte)~(1 << bit);
            this.SetFile(address, value);
        }

        internal ushort GetPC()
        {
            ushort lowerBits = (ushort)GetFile(0x02);
            ushort upperBits = (ushort)GetFile(0x0A);
            ushort pc = (ushort)((upperBits << 8) + lowerBits);

            return pc;
        }

        internal void SetPC(ushort pc)
        {
            byte lowerBits = (byte)(pc & 0x00FF);
            byte upperBits = (byte)((pc & 0x1F00) >> 8);

            SetFile(0x02, lowerBits);
            SetFile(0x0A, upperBits);
        }

        internal void IncPC()
        {
            ushort pc = GetPC();
            pc++;
            SetPC(pc);
        }

        private int DecodeAddress(ushort address)
        {
            // Special purpose registers (Bank1): 0x00 - 0x0B
            // Special purpose registers (Bank2): 0x80 - 0x8B -> gemapped auf 0x50 - 0x5B
            // General purpose registers (Bank1): 0x0C - 0x4F
            // General purpose registers (Bank2): 0x8C - 0xCF -> gemapped auf 0x0C - 0x4F

            // Speicherlayout: SPR1 - GPR - SPR2 
            if (address >= 0x00 && address <= 0x4F) { return address; }
            if ((address >= 0x50 && address <= 0x7F) || (address >= 0xD0 && address <= 0xFF)) { return 0; }
            if (address >= 0x80 && address <= 0x8B) { return address - 0x30; }
            if (address >= 0x8C && address <= 0xCF) { return address - 0x80; }

            throw new Exception();
        }


        private void InitializeMemory()
        {
            for (int i = 0; i <= 0x5B; i++)
            {
                this.Memory.Add(0);
            }
        }
    }
}
