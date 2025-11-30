using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using FlexAutomator.ViewModels;

namespace FlexAutomator.Views;

public partial class ScenarioEditorWindow : Window
{
    public ScenarioEditorViewModel ViewModel { get; }

    public ScenarioEditorWindow(ScenarioEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveCommand.CanExecute(null))
        {
            await ViewModel.SaveCommand.ExecuteAsync(null);

            System.Windows.MessageBox.Show("Сценарій успішно збережено!", "Збереження", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }


    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (ViewModel.Blocks == null) return;

        foreach (var block in ViewModel.Blocks)
        {
            if (block is HotkeyTriggerViewModel hotkeyVm && hotkeyVm.IsRecording)
            {

                var key = (e.Key == Key.System) ? e.SystemKey : e.Key;

                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LWin || key == Key.RWin ||
                    key == Key.System)
                {
                    return;
                }

                hotkeyVm.ApplyKey(key, Keyboard.Modifiers);

                e.Handled = true;
                return;
            }
        }
    }
}
