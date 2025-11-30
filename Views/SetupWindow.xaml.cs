using System.Windows;
using FlexAutomator.ViewModels;

namespace FlexAutomator.Views;

public partial class SetupWindow : Window
{
    public SetupViewModel ViewModel { get; }

    public SetupWindow(SetupViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestClose += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}