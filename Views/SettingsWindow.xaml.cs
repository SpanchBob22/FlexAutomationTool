using System;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using FlexAutomator.ViewModels;

namespace FlexAutomator.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveSettingsCommand.CanExecute(null))
        {
            await ViewModel.SaveSettingsCommand.ExecuteAsync(null);

            System.Windows.MessageBox.Show("Налаштування успішно збережено!", "Налаштування", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}