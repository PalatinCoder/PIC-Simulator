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
        /// Zur internen Verwendung
        /// </summary>
        private bool _isSimInitialized = false;

        /// <summary>
        /// Public und read only, daran ist das ListView gebunden
        /// </summary>
        public ObservableCollection<Instruction> Sourcecode { get { return this._sourcecodeListing; } }

        /// <summary>
        /// Wird auf true gesetzt, wenn ein Programm geladen ist
        /// </summary>
        public bool IsSimInitialized
        {
            get { return this._isSimInitialized; }
            set
            {
                this._isSimInitialized = value;
                this.OnPropertyChanged("ControlButtonState");
                this.OnPropertyChanged("InverseControlButtonState");
            }
        }

        public bool IsProgramRunning
        {
            get { return this._isProgramRunning; }
            set {
                this._isProgramRunning = value;
                this.OnPropertyChanged("ControlButtonState");
                this.OnPropertyChanged("InverseControlButtonState");
            }
        }

        public bool ControlButtonState
        {
            get {
                if (IsSimInitialized) return this._isProgramRunning;
                return false;
            }
        }
        public bool InverseControlButtonState {
            get {
                if (IsSimInitialized) return !this.ControlButtonState;
                return false;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
