using System;
using System.Collections.ObjectModel;

namespace PIC_Simulator
{
    class Processor
    {
        internal Collection<ProcessorInstruction> ProgramMemory = new Collection<ProcessorInstruction>();
        private ushort pc;

        private ISourcecodeViewInterface ViewInterface;


        public Processor(ISourcecodeViewInterface viewInterface)
        {
            this.ViewInterface = viewInterface;
        }

        public void Run()
        {

        }

        public void Step()
        {
            this.pc++;
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[pc].LineNumber);
        }
        
        internal void Reset()
        {
            this.pc = 0;
            ViewInterface.SetCurrentSourcecodeLine(this.ProgramMemory[0].LineNumber - 1);
        }
    }

    interface ISourcecodeViewInterface
    {
        void SetCurrentSourcecodeLine(int line);
    }
}
