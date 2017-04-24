using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;

namespace PIC_Simulator
{
    class Processor
    {
        internal Collection<ProcessorInstruction> ProgramMemory = new Collection<ProcessorInstruction>();
        private ushort pc;

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
        }

        private void Clock_Tick(object sender, object e)
        {
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

            if ((opcode & 0xFC00) == 0x1400)
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
            throw new NotImplementedException();
        }

        private void clrf()
        {
            throw new NotImplementedException();
        }

        private void clrw()
        {
            throw new NotImplementedException();
        }

        private void comf()
        {
            throw new NotImplementedException();
        }

        private void decf()
        {
            throw new NotImplementedException();
        }

        private void decfsz()
        {
            throw new NotImplementedException();
        }

        private void incf()
        {
            throw new NotImplementedException();
        }

        private void incfsz()
        {
            throw new NotImplementedException();
        }

        private void iorwf()
        {
            throw new NotImplementedException();
        }

        private void movf()
        {
            throw new NotImplementedException();
        }

        private void movwf()
        {
            throw new NotImplementedException();
        }

        private void nop()
        {
            throw new NotImplementedException();
        }

        private void rlf()
        {
            throw new NotImplementedException();
        }

        private void rrf()
        {
            throw new NotImplementedException();
        }

        private void subwf()
        {
            throw new NotImplementedException();
        }

        private void swapf()
        {
            throw new NotImplementedException();
        }

        private void xorwf()
        {
            throw new NotImplementedException();
        }

        private void bcf()
        {
            throw new NotImplementedException();
        }

        private void bsf()
        {
            throw new NotImplementedException();
        }

        private void btfsc()
        {
            throw new NotImplementedException();
        }

        private void btfss()
        {
            throw new NotImplementedException();
        }

        private void addlw()
        {
            throw new NotImplementedException();
        }

        private void andlw()
        {
            throw new NotImplementedException();
        }

        private void call()
        {
            throw new NotImplementedException();
        }

        private void clrwdt()
        {
            throw new NotImplementedException();
        }

        private void goto_f()
        {
            throw new NotImplementedException();
        }

        private void iorlw()
        {
            throw new NotImplementedException();
        }

        private void movlw()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
        internal ObservableCollection<ushort> Memory = new ObservableCollection<ushort>();

        internal ushort GetFile(ushort address)
        {
            // Special purpose registers (Bank1): 0x00 - 0x0B
            // Special purpose registers (Bank2): 0x80 - 0x8B -> gemapped auf 0x50 - 0x5B
            // General purpose registers (Bank1): 0x0C - 0x4F
            // General purpose registers (Bank2): 0x8C - 0xCF -> gemapped auf 0x0C - 0x4F

            // Speicherlayout: SPR1 - GPR - SPR2 
            if (address >= 0x00 && address <= 0x4F) { return Memory[address]; }
            if ((address >= 0x50 && address <= 0x7F) || (address >= 0xD0 && address <= 0xFF)) { return 0; }
            if (address >= 0x80 && address <= 0x8B) { return Memory[address - 0x30]; }
            if (address >= 0x8C && address <= 0xCF) { return Memory[address - 0x80]; }

            return 0;
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
