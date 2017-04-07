namespace PIC_Simulator
{
    /// <summary>
    /// Basisklasse um Instructions zu beschreiben
    /// </summary>
    public class Instruction
    {
        /// <summary>
        /// Die Sourcecode Zeile
        /// </summary>
        private string Value;

        /// <summary>
        /// Die Zeilennummer in der diese Instruction im Sourcecode Listing steht
        /// Wird abgerufen, um die Zeile zu markieren falls ein Breakpoint gehittet wird
        /// oder nach einem Jump
        /// </summary>
        public int LineNumber { get; }

        public Instruction(int line, string value)
        {
            this.Value = value;
            this.LineNumber = line;
        }

        /// <summary> 
        /// Wenn die Instructions im ListView angezeigt werden, wird ToString() aufgerufen
        /// um die Darstellung der Instruction abzurufen.
        /// </summary>
        /// <returns>Menschenlesbare Darstellung der Instruction</returns>
        public override string ToString()
        {
            return this.Value;
        }
    }

    /// <summary>
    /// Spezielle Klasse um eine Instruction für den µC zu beschreiben
    /// </summary>
    public class ProcessorInstruction : Instruction
    {
        /// <summary>
        /// Der 14bit Opcode der Instruction (Datentyp unsigned short ^= 16 bit)
        /// </summary>
        public ushort Opcode { get; }

        public ProcessorInstruction(int line, string value, ushort opcode) : base(line, value)
        {
            this.Opcode = opcode;
        }
    }
}