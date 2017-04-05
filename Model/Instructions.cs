namespace PIC_Simulator
{
    public abstract class Instruction
    {
        internal string StringRepresentation;

        public override string ToString()
        {
            return this.StringRepresentation;
        }
    }

    public class CommentLine : Instruction
    {
        public CommentLine(string text)
        {
            this.StringRepresentation = text;
        }
    }

    /// <summary>
    /// Klasse um Anweisungen für den Compiler zu beschreiben
    /// </summary>
    public class CompilerInstruction : Instruction
    {

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
    }
}