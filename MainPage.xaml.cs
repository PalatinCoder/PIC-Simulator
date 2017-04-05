using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace PIC_Simulator
{
    /// <summary>
    /// Die Hauptseite des PIC Simulators, auf der das Sourcecode Listing und die Register angezeigt werden.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private ObservableCollection<ProcessorInstruction> _sourcecodeListing = new ObservableCollection<ProcessorInstruction>();
        public ObservableCollection<ProcessorInstruction> Sourcecode { get { return this._sourcecodeListing; } }
        
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Hier kommt das PDF.");
            await dialog.ShowAsync();
        }

        private async void OpenFileChooser_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.FileTypeFilter.Add(".LST");

            StorageFile file = await picker.PickSingleFileAsync();

            this.statusbar.Text = "Programm " + file.DisplayName + " wird geladen.";
            // TODO hier das file an den Parser übergeben
        }
    }
}
