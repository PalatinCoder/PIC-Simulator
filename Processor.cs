using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;

namespace PIC_Simulator
{
    class Processor : INotifyPropertyChanged
    {
        internal Collection<ProcessorInstruction> ProgramMemory = new Collection<ProcessorInstruction>();
        internal MemoryController memController;
        private byte wreg;
        internal byte Wreg { get { return wreg; } set { wreg = value; this.OnPropertyChanged(); } }
        private bool twoCycles;
        private ushort timerPrescalerCounter = 0;
        private short timer_waitcycles = 0;
        internal ObservableStack<ushort> Stack = new ObservableStack<ushort>();
        internal static ushort PORTB = 0x06;
        internal static ushort INTCON = 0x0B;
        internal static ushort OPTION_REG = 0x81;
        private byte tmpPORTA, tmpPORTB, tmpINTCON;

        /// <summary>
        /// Der interne Takt für (!)µC-Zyklen
        /// Beachten: f_PIC = f_Quarz / 4, also 4 Quarz Schwingungen
        /// ergeben einen µC-Takt
        /// </summary>
        public DispatcherTimer Clock = new DispatcherTimer();

        private ISourcecodeViewInterface ViewInterface;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Steuerlogik

        public Processor(ISourcecodeViewInterface viewInterface)
        {
            this.ViewInterface = viewInterface;
            Clock.Tick += Clock_Tick;
            this.Clock.Interval = new TimeSpan(20); // 20 Ticks ^= 2000 ns ^= 2MHz Quarzfrequenz
            this.memController = new MemoryController(this.EnableWaitCycles);
            this.twoCycles = false;
            tmpPORTA = tmpPORTB = tmpINTCON = 0;
        }

        private ushort GetOpcode()
        {
            ushort pc = this.memController.PC;
            return this.ProgramMemory[pc].Opcode;
        }

        private void Clock_Tick(object sender, object e)
        {
            if (this.ProgramMemory[this.memController.PC].IsBreakpoint)
            {
                this.Clock.Stop();
                this.ViewInterface.SetIsProgrammRunning(false);
                return;
            }

            this.Step();
        }

        public void Step()
        {
            this.ViewInterface.IncreaseStopwatch(this.Clock.Interval);
            Tmr0Tick();
            this.Decode();
            if (this.twoCycles)
            {
                this.ViewInterface.IncreaseStopwatch(this.Clock.Interval);
                Tmr0Tick();
                this.twoCycles = false;
            }
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[memController.PC].LineNumber - 1);
        }

        internal void Reset()
        {
            // TODO Richtige reset Werte
            this.memController.ClearMemory();
            this.memController.PC = 0;
            this.Wreg = 0;
            this.memController.SetFile(0x81, 0xFF);
            ViewInterface.ResetStopwatch();
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[0].LineNumber - 1);
        }

        private void Tmr0Tick()
        {
            if (this.memController.GetBit(OPTION_REG, 5) == 0)
            {
                // Timer Modus wenn T0CS clear
                // Prescaler holen
                ushort PrescalerRateSelect = (ushort)(this.memController.GetFile(0x81) & 0x07);
                ushort PrescalerRatio = (ushort)(1 << (PrescalerRateSelect + 1)); // = 2 ^ (PrescalerRateSelect + 1)
                ushort psa = (ushort)(this.memController.GetBit(0x81, 3));

                if (timer_waitcycles <= 0 && ((psa == 0 && this.timerPrescalerCounter >= PrescalerRatio) || !(psa == 0))) {
                    if (psa == 0) this.timerPrescalerCounter = 0;
                    IncTimer();
                }
                else
                    timer_waitcycles--;

                this.timerPrescalerCounter++;
            }
            else
            {
                // Counter Modus wenn T0CS gesetzt
                if (this.memController.GetBit(OPTION_REG, 4) == 0)
                {
                    // Counter schaltet bei rising edge
                    if ((this.memController.GetBit(INTCON, 4) == 1) && ((tmpINTCON & 0x10) == 0))
                        IncTimer();
                }
                else
                {
                    // Counter schaltet bei falling edge
                    if ((this.memController.GetBit(INTCON, 4) == 0) && ((tmpINTCON & 0x10) == 1))
                        IncTimer();
                }
            }
        }

        private void IncTimer()
        {
            byte timer = this.memController.GetFile(0x01);
            timer++;
            this.memController.SetTimer(timer);

            if (timer == 0)
            {
                // Overflow des Timers -> Interrupt auslösen (T0IF)
                this.memController.SetBit(INTCON, 2);
            }
        }

        private void CheckForInterrupts()
        {
            // Testen ob GIE Bit gesetzt ist (ansonsten nicht auf Interrupts prüfen)
            if (this.memController.GetBit(INTCON, 7) == 1)
            {
                // Bei Interrupt wird an Stelle 0x04 gesprungen
                // GIE löschen
                // Code ausfähren
                // GIE wieder setzen

                // INT (RB0) Interrupt



                // PORTB Interrupt:
                //if (this.memController.GetBit(PORTB, 3))...
            }
        }

        private void EnableWaitCycles()
        {
            this.timer_waitcycles = 2;
        }

        /// <summary>
        /// Diese Routine dekodiert den aktuellen Befehl und ruft die entsprechende
        /// Subroutine zur Ausführung des Maschinenbefehls auf.
        /// Zur Dekodierung werden die pro Maschinenbefehl ausschlaggebenden Bits maskiert
        /// und gemäß Datenblatt auf ihren Wert geprüft.
        /// </summary>
        private void Decode()
        {
            ushort opcode = this.GetOpcode();

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
                this.memController.PC++;

            if ((opcode & 0xFF00) == 0x0D00)
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
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            ushort value = (ushort)this.memController.GetFile(address);
            ushort result = (ushort)(this.Wreg + value);

            // DC-Flag handling
            if (value <= 0x0F && this.Wreg <= 0x0F && result >= 0x10)
                this.memController.SetBit(0x03, 1);
            else
                this.memController.ClearBit(0x03, 1);

            // C-Flag handling
            if ((result & 0xFF00) > 0)
                this.memController.SetBit(0x03, 0);
            else
                this.memController.ClearBit(0x03, 0);

            // Z-Flag handling
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, (byte)result);
            else
                this.Wreg = (byte)result;

            this.memController.PC++;
        }

        private void andwf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(this.Wreg & value);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            // Z-Flag handling
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void clrf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            this.memController.SetFile(address, 0);
            this.memController.SetZeroFlag();

            this.memController.PC++;
        }

        private void clrw()
        {
            this.Wreg = 0;
            this.memController.SetZeroFlag();

            this.memController.PC++;
        }

        private void comf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(~value);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            this.memController.PC++;
        }

        private void decf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            value--;

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.Wreg = value;

            // Z-Flag handling
            if (value == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void decfsz()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            value--;

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.Wreg = value;

            if (value == 0)
            {
                this.twoCycles = true;
                this.memController.PC++;
            }

            this.memController.PC++;
        }

        private void incf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(value + 1);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            // Z-Flag handling
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void incfsz()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(value + 1);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            if (result == 0)
            {
                this.twoCycles = true;
                this.memController.PC++;  //Programmcounter hochzaehlen, um naechsten Befehl zu ueberspringen
            }

            this.memController.PC++;
        }

        private void iorwf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            byte result = (byte)(value | this.Wreg);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            // Z-Flag handling
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void movf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            bool destination = ((this.GetOpcode() & 0x0080) == 0x0080);
            byte value = this.memController.GetFile(address);

            // if d == 0, value von f in w-register schreiben,
            // andernfalls f in f schreiben (redundant)
            if (!destination)
                this.Wreg = value;

            // Z-Flag handling
            if (value == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void movwf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            this.memController.SetFile(address, this.Wreg);

            this.memController.PC++;
        }

        private void rlf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            ushort value = (ushort)this.memController.GetFile(address);

            value = (ushort)(value << 1);
            byte carry = this.memController.GetBit(0x03, 0);
            byte newCarry = (byte)((value & 0x0100) >> 8);
            byte newValue = (byte)(value + carry);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, newValue);
            else
                this.Wreg = newValue;

            // C-Flag handling
            if (newCarry == 1)
                this.memController.SetBit(0x03, 0);
            else
                this.memController.ClearBit(0x03, 0);

            this.memController.PC++;
        }

        private void rrf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);

            byte carry = this.memController.GetBit(0x03, 0);
            byte newCarry = (byte)(value & 0x01);
            value = (byte)((value >> 1) + (carry << 7));

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.Wreg = value;

            // C-Flag handling
            if (newCarry == 1)
                this.memController.SetBit(0x03, 0);
            else
                this.memController.ClearBit(0x03, 0);

            this.memController.PC++;
        }

        private void subwf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = (byte)(this.memController.GetFile(address));

            byte result = (byte)(value - this.Wreg);

            // C-Flag handling
            if (this.Wreg > value)
                this.memController.ClearBit(0x03, 0);
            else
                this.memController.SetBit(0x03, 0);

            // DC-Flag handling
            if ((this.Wreg & 0x0F) > (value & 0x0F))
                this.memController.ClearBit(0x03, 1);
            else
                this.memController.SetBit(0x03, 1);

            // Z-Flag handling
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            // In W-reg oder file
            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            this.memController.PC++;
        }

        private void swapf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);
            byte lowerNibbles = (byte)(value & 0x0F);
            byte upperNibbles = (byte)((value & 0xF0) >> 4);

            value = (byte)((lowerNibbles << 4) + upperNibbles);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, value);
            else
                this.Wreg = value;

            this.memController.PC++;
        }

        private void xorwf()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte value = this.memController.GetFile(address);

            byte result = (byte)(value ^ this.Wreg);

            if ((this.GetOpcode() & 0x0080) > 0)
                this.memController.SetFile(address, result);
            else
                this.Wreg = result;

            this.memController.PC++;
        }

        private void bcf()
        {
            byte bit = (byte)((this.GetOpcode() & 0x0380) >> 7);
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            this.memController.ClearBit(address, bit);

            this.memController.PC++;
        }

        private void bsf()
        {
            byte bit = (byte)((this.GetOpcode() & 0x0380) >> 7);
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            this.memController.SetBit(address, bit);

            this.memController.PC++;
        }

        private void btfsc()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte bit = (byte)((this.GetOpcode() & 0x0380) >> 7);
            byte result = this.memController.GetBit(address, bit);

            if (result == 0)
            {
                this.twoCycles = true;
                this.memController.PC++;
            }

            this.memController.PC++;
        }

        private void btfss()
        {
            ushort address = (ushort)(this.GetOpcode() & 0x007F);
            byte bit = (byte)((this.GetOpcode() & 0x0380) >> 7);
            byte result = this.memController.GetBit(address, bit);

            if (result > 0)
            {
                this.twoCycles = true;
                this.memController.PC++;
            }

            this.memController.PC++;
        }

        private void addlw()
        {
            byte literal = (byte)(this.GetOpcode() & 0x00FF);
            ushort result = (ushort)(literal + this.Wreg);

            //Set DC Bit
            if (literal <= 0x0F && this.Wreg <= 0x0F && result >= 0x10)
                this.memController.SetBit(0x03, 1);
            else
                this.memController.ClearBit(0x03, 1);

            //Set C Bit
            if ((result & 0xFF00) > 0)
                this.memController.SetBit(0x03, 0);
            else
                this.memController.ClearBit(0x03, 0);

            //Set Z Bit
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.Wreg = (byte)result;

            this.memController.PC++;
        }

        private void andlw()
        {
            byte literal = (byte)(this.GetOpcode() & 0x00FF);
            this.Wreg &= literal;

            if (this.Wreg == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void call()
        {
            this.twoCycles = true;
            // Dokumentation sagt PCLATH<4:3> nach PC<12:11>
            // Dadurch wird Bit 2 komplett übergangen.
            ushort pclath = (ushort)(this.memController.GetFile(0x0A) & 0x18);
            ushort address = (ushort)(this.GetOpcode() & 0x07FF);
            this.Stack.Push((ushort)(this.memController.PC + 1));
            this.memController.PC = ((ushort)(address | (pclath << 8)));
        }

        private void clrwdt()
        {
            // TO und PD Bits im Statusregister auf 1 Setzen
            this.memController.SetBit(0x03, 4);
            this.memController.SetBit(0x03, 3);

            //TODO WDT auf 0x00 setzen
            //TODO WDT prescaler auf 0 setzen

            this.memController.PC++;
        }

        private void goto_f()
        {
            this.twoCycles = true;
            ushort pclath = (ushort)(this.memController.GetFile(0x0A) & 0x18);
            ushort address = (ushort)(this.GetOpcode() & 0x07FF);
            this.memController.PC = ((ushort)(address | (pclath << 8)));
        }

        private void iorlw()
        {
            byte literal = (byte)(this.GetOpcode() & 0x00FF);
            this.Wreg = (byte)(literal | this.Wreg);

            // Z-Flag handling
            if (this.Wreg == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }

        private void movlw()
        {
            byte literal = (byte)(this.GetOpcode() & 0x00FF);
            this.Wreg = literal;

            this.memController.PC++;
        }

        private void retfie()
        {
            this.twoCycles = true;
            // Set GIE bit:
            this.memController.SetBit(0x0B, 7);
            this.memController.PC = (this.Stack.Pop());
        }

        private void retlw()
        {
            this.twoCycles = true;
            this.Wreg = (byte)(this.GetOpcode() & 0x00FF);
            this.memController.PC = (this.Stack.Pop());
        }

        private void return_f()
        {
            this.twoCycles = true;
            this.memController.PC = (this.Stack.Pop());
        }

        private void sleep()
        {
            // TODO 0x00 -> WDT
            // TODO 0 -> WDT prescaler
            // TODO PIC in Sleep Modus versetzen!
            this.memController.SetBit(0x03, 4);
            this.memController.ClearBit(0x03, 3);
        }

        private void sublw()
        {
            byte literal = (byte)(this.GetOpcode() & 0x00FF);
            byte result = (byte)(literal - this.Wreg);

            // C-Flag handling
            if (this.Wreg > literal)
                this.memController.ClearBit(0x03, 0);
            else
                this.memController.SetBit(0x03, 0);

            // DC-Flag handling
            if ((this.Wreg & 0x0F) > (literal & 0x0F))
                this.memController.ClearBit(0x03, 1);
            else
                this.memController.SetBit(0x03, 1);

            // Z-Flag handling
            if (result == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.Wreg = result;

            this.memController.PC++;
        }

        private void xorlw()
        {
            byte literal = (byte)(this.GetOpcode() & 0x00FF);
            this.Wreg = (byte)(literal ^ this.Wreg);

            // Z-Flag handling
            if (this.Wreg == 0)
                this.memController.SetZeroFlag();
            else
                this.memController.ClearZeroFlag();

            this.memController.PC++;
        }
#pragma warning restore IDE1006 // Benennungsstile
        #endregion
    }

    interface ISourcecodeViewInterface
    {
        void SetCurrentSourcecodeLine(int line);
        void SetIsProgrammRunning(bool value);
        void IncreaseStopwatch(TimeSpan value);
        void ResetStopwatch();
    }


    internal class MemoryController : INotifyPropertyChanged
    {
        Action EnableWaitCyclesCallback;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        internal byte StatusRegister { get { return this.GetFile(0x03); } }

        internal ObservableCollection<Utility.BindableByte> Memory = new ObservableCollection<Utility.BindableByte>();
        private ushort pc = 0;
        internal ushort PC
        {
            get
            {
                return this.pc;
            }
            set
            {
                byte lowerBits = (byte)(value & 0x00FF);
                this.SetFile(0x02, lowerBits);
                this.pc = value;
                // GUI update callback
                this.OnPropertyChanged("PC");
            }
        }

        internal MemoryController(Action EnableWaitCyclesCallback)
        {
            this.InitializeMemory();
            this.EnableWaitCyclesCallback = EnableWaitCyclesCallback;
        }

        internal byte GetFile(ushort address)
        {
            int index = DecodeAddress(address)[0];
            return this.Memory[index];
        }

        internal void SetFile(ushort address, byte value)
        {
            // Nicht-implementierte memory locations nicht beschreiben!
            if (address == 0x00 || address == 0x80) return;
            if (address == 0x07 || address == 0x87) return;
            if (address >= 0x50 && address <= 0x7F) return;
            if (address >= 0xD0 && address <= 0xFF) return;

            if (address == 0x01)
            {
                // Wenn Timer direkt bearbeitet wird
                if (this.GetBit(0x81, 5) == 0)
                {
                    this.EnableWaitCyclesCallback();
                    this.SetTimer(value);
                }
            }

            if (address == 0x02) // Set PCL
            {
                ushort pclath = (ushort)(this.GetFile(0x0A) & 0x1F);
                this.pc = (ushort)(value | (pclath << 8));
            }
            
            ushort[] addresses = DecodeAddress(address);

            foreach (ushort element in addresses)
            {
                this.Memory[element] = value;
            }

            // Statusregister in GUI updaten
            if (address == 0x03) this.OnPropertyChanged("StatusRegister");
        }

        internal void SetTimer(byte value)
        {
            this.Memory[0x01] = value;
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
            // Gibt entweder 1 oder 0 zurück, also den Wert des Bits, nicht nur das maskierte Byte
            return (byte)((this.GetFile(address) & (1 << bit)) >> bit);
        }

        internal void SetBit(ushort address, byte bit)
        {
            byte value = this.GetFile(address);
            value |= (byte)(1 << bit); // Wert mit einer geshifteten 1 verodern, um ein Bit zu setzen
            this.SetFile(address, value);
        }

        internal void ClearBit(ushort address, byte bit)
        {
            byte value = this.GetFile(address);
            value &= (byte)~(1 << bit); // Wert '1' shiften und danach umkehren - Das dann mit value verunden, um ein Bit zu clearen
            this.SetFile(address, value);
        }

        private ushort[] DecodeAddress(ushort address)
        {
            // Bankselect (rp0) holen um bei nichtgemappten Adressen die richtige Bank zu wählen
            ushort rp0 = (ushort)((this.Memory[0x03] & 0x20) >> 5);
            ushort bAddress = (ushort)(address & 0x7F);

            // Indirekte Addressierung:
            // Bei Zugriff auf Adresse 0x00 wird die Adresse in File 0x04 zurückgegeben und mit IRP Bit des Statusregisters verodert
            if (bAddress == 0x00) { return DecodeAddress((ushort)((this.GetBit(0x03, 7) << 7) | this.GetFile(0x04))); }

            // Direkte Addressierung:
            // GPRs und SFRs, die gemapped sind
            if ((bAddress >= 0x0A && bAddress <= 0x7F) || (bAddress >= 0x02 && bAddress <= 0x04)) return new ushort[] { bAddress, (ushort)(address | 0x80) };

            // Der ganze Rest
            return new ushort[] { (ushort)(address | (rp0 << 7)) };
        }

        internal void ClearMemory()
        {
            for (int i = 0; i <= 0xFF; i++)
            {
                this.Memory[i] = 0;
            }
            this.OnPropertyChanged("StatusRegister");
        }

        private void InitializeMemory()
        {
            for (int i = 0; i <= 0xFF; i++)
            {
                this.Memory.Add(0);
            }
        }
    }
}
