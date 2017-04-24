using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PIC_Simulator
{
    class MainPageViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Zur internen Verwendung
        /// </summary>
        private ObservableCollection<Instruction> _sourcecodeListing = new ObservableCollection<Instruction>();
        /// <summary>
        /// Zur internen Verwendung
        /// </summary>
        private bool _isProgramRunning = false;

        /// <summary>
        /// Public und read only, daran ist das ListView gebunden
        /// </summary>
        public ObservableCollection<Instruction> Sourcecode { get { return this._sourcecodeListing; } }

        public bool IsProgramRunning
        {
            get { return this._isProgramRunning; }
            set { this._isProgramRunning = value; this.OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
