using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;

namespace PIC_Simulator
{
    class Processor : INotifyPropertyChanged
    {
        private ISourcecodeViewInterface ViewInterface;

        internal Collection<ProcessorInstruction> ProgramMemory = new Collection<ProcessorInstruction>();
        internal MemoryController memController;
        internal ObservableStack<ushort> Stack = new ObservableStack<ushort>();

        private byte wreg;
        internal byte Wreg { get { return wreg; } set { wreg = value; this.OnPropertyChanged(); } }
        private bool twoCycles;
        private bool _isSleeping;
        private ushort timerPrescalerCounter = 0;
        private short timer_waitcycles = 0;

        // Häufig genutzte GPR Adressen
        internal static ushort PORTA = 0x05;
        internal static ushort PORTB = 0x06;
        internal static ushort INTCON = 0x0B;
        internal static ushort OPTION_REG = 0x81;
        // Temporäre Werte der PORTA bzw. INTCON Register
        private byte tmpPORTA, tmpINTCON;
        // Watchdog timer Zählvariable, zählt die vergangenen Ticks
        internal long Watchdog = 0;
        /// <summary>
        /// Der interne Takt für (!)µC-Zyklen
        /// Beachten: f_PIC = f_Quarz / 4, also 4 Quarz Schwingungen
        /// ergeben einen µC-Takt
        /// </summary>
        public DispatcherTimer Clock = new DispatcherTimer();

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
            tmpPORTA = tmpINTCON = 0;
        }

        /// <summary>
        /// Methode die den aktuellen OpCode zurückgibt
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Methode um einen Opcode einzulesen und auszuführen
        /// </summary>
        public void Step()
        {
            if (Interrupt()) return;
            this.ViewInterface.IncreaseStopwatch(this.Clock.Interval);
            WatchdogTick();
            if (this._isSleeping) return;

            Tmr0Tick();
            this.Decode();
            if (this.twoCycles)
            {
                this.ViewInterface.IncreaseStopwatch(this.Clock.Interval);
                WatchdogTick();
                Tmr0Tick();
                this.twoCycles = false;
            }

            tmpINTCON = this.memController.GetFile(INTCON);
            tmpPORTA = this.memController.GetFile(PORTA);
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[memController.PC].LineNumber - 1);
        }

        /// <summary>
        /// Methode um Watchdog Timer zu erhöhen
        /// </summary>
        private void WatchdogTick()
        {
            this.Watchdog += this.Clock.Interval.Ticks;

            ushort PostscalerRateSelect = (ushort)(this.memController.GetFile(0x81) & 0x07);
            int PostscalerRatio = 1;
            ushort psa = (this.memController.GetBit(0x81, 3));

            if (psa == 1) PostscalerRatio = (ushort)(1 << (PostscalerRateSelect)); // = 2 ^ PrescalerRateSelect

            long BaseTicks = 180000; // 18ms
#if DEBUG_WDT
            BaseTicks = 1800; // damit's beim debuggen schneller geht ;)
#endif
            if (this.Watchdog >= (BaseTicks * PostscalerRatio))
                if (this._isSleeping)
                {
                    this._isSleeping = false;
                    this.Watchdog = 0;
                    this.memController.PC++;
                }
                else
                {
                    this.Reset();
                    this.memController.ClearBit(0x03, 4); // PD bit
                }
        }

        /// <summary>
        /// Methode um Speicher zurückzusetzen
        /// </summary>
        internal void Reset()
        {
            // Memory auf Standardwerte zurücksetzen
            this.memController.ClearMemory();

            // Spezielle Variablen zurücksetzen
            this.memController.PC = 0;
            this.Wreg = 0;
            this.Watchdog = 0;
            this._isSleeping = false;

            // Stopwatch zurücksetzen und erste Zeile highlighten
            ViewInterface.ResetStopwatch();
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[0].LineNumber - 1);
        }

        /// <summary>
        /// Methode um den Timer0 ticken zu lassen
        /// </summary>
        private void Tmr0Tick()
        {
            // Prescaler holen
            ushort PrescalerRateSelect = (ushort)(this.memController.GetFile(0x81) & 0x07);
            ushort PrescalerRatio = (ushort)(1 << (PrescalerRateSelect + 1)); // = 2 ^ (PrescalerRateSelect + 1)
            ushort psa = (ushort)(this.memController.GetBit(0x81, 3));
            if (this.memController.GetBit(OPTION_REG, 5) == 0)
            {
                // Timer Modus wenn T0CS clear
                if (timer_waitcycles <= 0 && ((psa == 0 && this.timerPrescalerCounter >= PrescalerRatio) || !(psa == 0)))
                {
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
                    if (((tmpPORTA & 0x10) == 0) && (this.memController.GetBit(PORTA, 4) == 1))
                    {
                        if (this.timerPrescalerCounter >= PrescalerRatio)
                        {
                            IncTimer();
                            this.timerPrescalerCounter = 0;
                        }
                        tmpPORTA = this.memController.GetFile(PORTA);
                        this.timerPrescalerCounter++;
                    }
                }
                else
                {
                    // Counter schaltet bei falling edge
                    if (((tmpPORTA & 0x10) > 0) && (this.memController.GetBit(PORTA, 4) == 0))
                    {
                        if (this.timerPrescalerCounter >= PrescalerRatio)
                        {
                            IncTimer();
                            this.timerPrescalerCounter = 0;
                        }
                        tmpPORTA = this.memController.GetFile(PORTA);
                        this.timerPrescalerCounter++;
                    }
                }
            }
        }

        /// <summary>
        /// Methode um den Timer zu erhöhen
        /// </summary>
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

        /// <summary>
        /// Methode um auf Interrupts zu prüfen und diese zu behandlen
        /// </summary>
        /// <returns>Liegt ein Interrupt vor</returns>
        private bool Interrupt()
        {
            if (CheckForInterrupts())
            {
                // Aktuellen PC auf Stack pushen
                this.Stack.Push(this.memController.PC);
                // GIE löschen
                this.memController.ClearBit(INTCON, 7);
                // Bei Interrupt wird an Stelle 0x04 gesprungen
                this.memController.PC = 0x04;
                ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[memController.PC].LineNumber - 1);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Methode, die prüft, ob interrupts vorliegen
        /// </summary>
        /// <returns>Liegt ein Interrupt vor</returns>
        private bool CheckForInterrupts()
        {
            // Testen ob GIE Bit gesetzt ist (ansonsten nicht auf Interrupts prüfen)
            if (this.memController.GetBit(INTCON, 7) == 1)
            {
                // RB Port Change
                if (this.memController.GetBit(INTCON, 3) == 1 && this.memController.GetBit(INTCON, 0) == 1) return true;
                // RB0 / INT
                if (this.memController.GetBit(INTCON, 4) == 1 && this.memController.GetBit(INTCON, 1) == 1) return true;
                // TMR0 overflow
                if (this.memController.GetBit(INTCON, 5) == 1 && this.memController.GetBit(INTCON, 2) == 1) return true;
                // EE Write Complete
                if (this.memController.GetBit(INTCON, 6) == 1 && this.memController.GetBit(0x88, 4) == 1) return true;
            }

            return false;
        }

        /// <summary>
        /// Methode um Timer-Waitcycles zu aktivieren
        /// </summary>
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
            if (((value & 0x0F) + (this.Wreg & 0x0F)) > 15)
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

            //WDT auf 0x00 setzen
            this.Watchdog = 0;
            //WDT prescaler auf 0 setzen
            this.memController.ClearBit(0x81, 0);
            this.memController.ClearBit(0x81, 1);
            this.memController.ClearBit(0x81, 2);

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
            this.memController.SetBit(INTCON, 7);
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
            // 0x00 -> WDT
            this.Watchdog = 0;
            // 0 -> WDT prescaler
            this.memController.ClearBit(0x81, 0);
            this.memController.ClearBit(0x81, 1);
            this.memController.ClearBit(0x81, 2);
            // Set TO (Time out bit)
            this.memController.SetBit(0x03, 4);
            // Clear PD (Power Down bit)
            this.memController.ClearBit(0x03, 3);

            this._isSleeping = true;
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

    /// <summary>
    /// Memorycontroller, der die Verwaltung des Speichers & EEPROMS übernimmt
    /// </summary>
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
        internal Collection<Byte> EEPROM = new Collection<byte>();
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
                // PCL setzen
                this.SetFile(0x02, lowerBits);
                this.pc = value;
                // GUI update callback
                this.OnPropertyChanged("PC");
            }
        }
        internal Collection<byte> PortA
        {
            get
            {
                Utility.BindableByte portA = this.Memory[0x05];
                Collection<byte> BitVector = new Collection<byte>();
                for (int i = 0; i < 5; i++)
                {
                    if ((portA & (1 << i)) > 0)
                        BitVector.Add(1);
                    else
                        BitVector.Add(0);
                }
                return BitVector;
            }
        }
        internal Collection<byte> PortB
        {
            get
            {
                Utility.BindableByte portB = this.Memory[0x06];
                Collection<byte> BitVector = new Collection<byte>();
                for (int i = 0; i < 8; i++)
                {
                    if ((portB & (1 << i)) > 0)
                        BitVector.Add(1);
                    else
                        BitVector.Add(0);
                }
                return BitVector;
            }
        }
        internal Collection<byte> TrisA
        {
            get
            {
                Utility.BindableByte trisA = this.Memory[0x85];
                Collection<byte> BitVector = new Collection<byte>();
                for (int i = 0; i < 5; i++)
                {
                    if ((trisA & (1 << i)) > 0)
                        BitVector.Add(1);
                    else
                        BitVector.Add(0);
                }
                return BitVector;
            }
        }
        internal Collection<byte> TrisB
        {
            get
            {
                Utility.BindableByte trisB = this.Memory[0x86];
                Collection<byte> BitVector = new Collection<byte>();
                for (int i = 0; i < 8; i++)
                {
                    if ((trisB & (1 << i)) > 0)
                        BitVector.Add(1);
                    else
                        BitVector.Add(0);
                }
                return BitVector;
            }
        }

        internal MemoryController(Action EnableWaitCyclesCallback)
        {
            this.InitializeMemory();
            this.InitializeEEPROM();
            this.EnableWaitCyclesCallback = EnableWaitCyclesCallback;
        }

        /// <summary>
        /// Methode um Wert von Speicheradresse zu lesen
        /// </summary>
        /// <param name="address">Adresse</param>
        /// <returns>Wert an der angegebenen Speicheradresse</returns>
        internal byte GetFile(ushort address)
        {
            int index = DecodeAddress(address)[0];
            if (index == 0x08)
                return ReadEEPROM();
            return this.Memory[index];
        }

        /// <summary>
        /// Methode um Wert an Speicheradresse zu schreiben
        /// </summary>
        /// <param name="address">Speichersterre</param>
        /// <param name="value">Wert</param>
        internal void SetFile(ushort address, byte value)
        {
            // Nicht-implementierte memory locations dürfen nicht beschrieben werden!
            if (address == 0x00 || address == 0x80) return;
            if (address == 0x07 || address == 0x87) return;
            if (address >= 0x50 && address <= 0x7F) return;
            if (address >= 0xD0 && address <= 0xFF) return;
            
            if (address == 0x01)
            {
                // Wenn Timer direkt (vom ausgeführten Code) bearbeitet wird
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
                if (address == 0x06)
                {
                    byte TRISB = 0x86;
                    byte tmpPORTB = GetFile(0x06);
                    byte INTEDG = GetBit(0x81, 6);
                    // Wenn Rising edge Bit gesetzt & Veränderung an RB0 von 0 -> 1
                    bool risingEdge = (INTEDG == 1 && (tmpPORTB & 0x01) == 0 && (value & 0x01) == 1);
                    // Wenn falling edge Bit gesetzt & Veränderung an RB0 von 1 -> 0
                    bool fallingEdge = (INTEDG == 0 && (tmpPORTB & 0x01) == 1 && (value & 0x01) == 0);

                    // Wenn an RB0 Flanke erkannt wird, RB0 Interrupt Bit setzen
                    if (risingEdge || fallingEdge)
                        SetBit(0x0B, 1);
                    else if ((tmpPORTB & 0xF0) != (value & 0xF0))
                    {
                        for (int bit = 4; bit <= 7; bit++)
                        {
                            byte mask = (byte)(1 << bit);
                            // Wenn Pin auf input gestellt && (vorher eine 1, dann eine 0 || vorher eine 0, dann eine 1), dann Interrupt
                            if (GetBit(TRISB, (byte)bit) == 1 && (((tmpPORTB & mask) > 0 && (value & mask) == 0) || ((tmpPORTB & mask) == 0 && (value & mask) > 0)))
                            {
                                SetBit(0x0B, 0);
                                break;
                            }
                        }
                    }
                }

                this.Memory[element] = value;

                if (element == 0x88)
                {
                    WriteEEPROM();
                }

                // Verschiedene Register in GUI updaten
                if (element == 0x03) this.OnPropertyChanged("StatusRegister");
                if (element == 0x05) this.OnPropertyChanged("PortA");
                if (element == 0x06) this.OnPropertyChanged("PortB");
                if (element == 0x85) this.OnPropertyChanged("TrisA");
                if (element == 0x86) this.OnPropertyChanged("TrisB");
            }
        }

        /// <summary>
        /// Methode um vom EEPROM zu lesen
        /// </summary>
        /// <returns>Wert an der abgefragten Adresse im EEPROM</returns>
        internal byte ReadEEPROM()
        {
            if (GetBit(0x88, 0) == 1)
                return EEPROM[Memory[0x09]];
            return 0;
        }

        /// <summary>
        /// Methode um EEPROM zu beschreiben
        /// </summary>
        internal void WriteEEPROM()
        {
            byte EECON = GetFile(0x88);
            byte WR = GetBit(0x88, 1);
            byte WREN = GetBit(0x88, 2);
            if (GetBit(0x88, 1) == 1 && GetBit(0x88, 2) == 1)
            {
                EEPROM[Memory[0x09]] = Memory[0x08];
                ClearBit(0x88, 1);
                SetBit(0x88, 4);
            }
        }

        /// <summary>
        /// Methode um den Timer zu setzen. Soll nur vom Prozessor aufgerufen werden. Hierbei gibt es keine Waitcycles!
        /// </summary>
        /// <param name="value">Wert, auf den der Timer gesetzt wird</param>
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
            value |= (byte)(1 << bit); // Wert mit einer um 'bit' geshifteten 1 verodern, um ein Bit zu setzen
            this.SetFile(address, value);
        }

        /// <summary>
        /// Setzt ein Bit an einer Adresse auf 0
        /// </summary>
        /// <param name="address">Speicheradresse</param>
        /// <param name="bit">Bit</param>
        internal void ClearBit(ushort address, byte bit)
        {
            byte value = this.GetFile(address);
            value &= (byte)~(1 << bit); // Wert '1' um 'bit' shiften und danach umkehren - Das dann mit value verunden, um ein Bit zu clearen
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

        /// <summary>
        /// Gibt den Wert bei Reset zurück
        /// </summary>
        /// <param name="address">Speicheradresse</param>
        /// <returns>Standardwert an bestimmter Adresse</returns>
        internal byte GetResetValue(byte address)
        {
            switch (address)
            {
                case 0x03:
                    return 0x18;
                case 0x81:
                    return 0xFF;
                case 0x83:
                    return 0x18;
                case 0x85:
                    return 0x1F;
                case 0x86:
                    return 0xFF;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Methode um Speicher mit Standardwerten zu beschreiben
        /// </summary>
        internal void ClearMemory()
        {
            for (int i = 0; i <= 0xFF; i++)
            {
                this.Memory[i] = GetResetValue((byte)i);
            }
            this.OnPropertyChanged("StatusRegister");
        }

        /// <summary>
        /// Methode um Speicher zu initialisieren
        /// </summary>
        private void InitializeMemory()
        {
            for (int i = 0; i <= 0xFF; i++)
            {
                this.Memory.Add(GetResetValue((byte)i));
            }
        }

        private void InitializeEEPROM()
        {
            for (int i = 0; i <= 0x3F; i++)
            {
                this.EEPROM.Add(0);
            }
        }
    }
}
