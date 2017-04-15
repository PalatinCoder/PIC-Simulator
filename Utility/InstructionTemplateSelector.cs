using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PIC_Simulator
{
    public class InstructionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ProcessorInstructionTemplate { get; set; }
        public DataTemplate GeneralInstructionTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ProcessorInstruction) return ProcessorInstructionTemplate;
            else return GeneralInstructionTemplate;
        }
    }
}
