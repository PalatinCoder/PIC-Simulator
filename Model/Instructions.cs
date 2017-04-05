namespace PIC_Simulator
{
    public abstract class Instruction
    {
        /// <summary>
        /// ToString muss für jede Klasse implementiert werden. 
        /// Wenn die Instructions im ListView angezeigt werden, wird ToString() aufgerufen
        /// um die Darstellung der Instruction abzurufen.
        /// </summary>
        /// <returns>Menschenlesbare Darstellung der Instruction</returns>
        public abstract override string ToString();
    }

    /// <summary>
    /// Klasse um Kommentare zu beschreiben
    /// </summary>
    public class CommentLine : Instruction
    {
        string text;

        public CommentLine(string text)
        {
            this.text = text;
        }

        public override string ToString()
        {
            return this.text;
        }
    }

    /// <summary>
    /// Klasse um Anweisungen für den Compiler zu beschreiben
    /// </summary>
    public class CompilerInstruction : Instruction
    {
        public override string ToString()
        {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Klasse um eine Instruction für den µC zu beschreiben
    /// </summary>
    public class ProcessorInstruction : Instruction
    {
        public string Mnemonic { get; }
        public int[] Operands { get; } = new int[2];

        public ProcessorInstruction(string mnemonic, int operand0 = 0, int operand1 = 0)
        {
            this.Mnemonic = mnemonic;
            this.Operands[0] = operand0;
            this.Operands[1] = operand1;
        }

        public override string ToString()
        {
            return System.String.Format("{0}\t0x{1,2:X},0x{2,2:X}", this.Mnemonic, this.Operands[0], this.Operands[1]);
        }
    }
}