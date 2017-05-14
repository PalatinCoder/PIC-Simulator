using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace PIC_Simulator
{
    /// <summary>
    /// Die Hauptseite des PIC Simulators, auf der das Sourcecode Listing und die Register angezeigt werden.
    /// </summary>
    public sealed partial class MainPage : Page, ISourcecodeViewInterface
    {
        private MainPageViewModel ViewModel;

        private Processor processor;

        public MainPage()
        {
            this.InitializeComponent();
            this.processor = new Processor(this);
            this.ViewModel = new MainPageViewModel();
            this.DataContext = this.ViewModel;
        }

        private async void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Hier kommt das PDF.");
            await dialog.ShowAsync();
        }

        private async void OpenFileChooser_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(".LST");

            StorageFile file = await picker.PickSingleFileAsync();

            if (file == null) return; // Benutzer hat abbrechen gedrückt

            this.statusbar.Text = "Programm " + file.DisplayName + " wird geöffnet";

            try
            {
                this.processor.ProgramMemory.Clear();
                this.ViewModel.Sourcecode.Clear();
                await ListingFileParser.Parse(file, SourcecodeLineParsed);
                this.processor.Reset();
                this.statusbar.Text = file.DisplayName + " geladen";
                this.ViewModel.IsSimInitialized = true;
            }
            catch (ArgumentOutOfRangeException)
            {
                var dialog = new MessageDialog(
                    "Beim Einlesen der Datei ist ein Fehler aufgetreten.\r\nMeistens hilft es die Datei nach UTF-8 zu konvertieren",
                    "Programm kann nicht geladen werden");
#pragma warning disable CS4014 // Da dieser Aufruf nicht abgewartet wird, wird die Ausführung der aktuellen Methode fortgesetzt, bevor der Aufruf abgeschlossen ist
                dialog.ShowAsync();
#pragma warning restore CS4014
            }
#if !DEBUG
            catch (Exception ex)
            {
                var dialog = new MessageDialog(
                    "Beim Einlesen der Datei ist ein Fehler aufgetreten:\r\n" + ex.Message,
                    "Programm kann nicht geladen werden");
#pragma warning disable CS4014 // Da dieser Aufruf nicht abgewartet wird, wird die Ausführung der aktuellen Methode fortgesetzt, bevor der Aufruf abgeschlossen ist
                dialog.ShowAsync();
#pragma warning restore CS4014
            }
#endif
        }

        /// <summary>
        /// Callback wenn eine Zeile geparst wurde. Fügt die Instruction dem Listing hinzu.
        /// </summary>
        /// <param name="instruction">Die generierte Instruction</param>
        private void SourcecodeLineParsed(Instruction instruction)
        {
            this.ViewModel.Sourcecode.Add(instruction);
            if (instruction is ProcessorInstruction) processor.ProgramMemory.Add((ProcessorInstruction)instruction);
        }

        public void SetCurrentSourcecodeLine(int line)
        {
            this.SourcecodeListingView.SelectedIndex = line;
            this.SourcecodeListingView.ScrollIntoView(this.SourcecodeListingView.SelectedItem, ScrollIntoViewAlignment.Leading);
        }

        public void SetIsProgrammRunning(bool value)
        {
            this.ViewModel.IsProgramRunning = value;
        }

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            this.processor.Step();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            this.processor.Clock.Start();
            this.ViewModel.IsProgramRunning = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.processor.Clock.Stop();
            this.ViewModel.IsProgramRunning = false;
        }

        private void BreakpointToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            ProcessorInstruction instruction = (ProcessorInstruction)button.DataContext;
            instruction.IsBreakpoint = (bool)button.IsChecked;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            this.processor.Reset();
        }
    }
}
