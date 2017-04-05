namespace PIC_Simulator
{
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
