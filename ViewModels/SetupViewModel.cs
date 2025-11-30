using System;
using System.IO;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexAutomator.Models;
using FlexAutomator.Services;
namespace FlexAutomator.ViewModels;
public partial class SetupViewModel : ObservableObject
{
    private readonly BootConfigService _bootConfigService;
    [ObservableProperty]
    private string _dbPath = "";

    public event Action? RequestClose;

    public SetupViewModel(BootConfigService bootConfigService)
    {
        _bootConfigService = bootConfigService;

        DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FlexAutomatorData");
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Оберіть папку для зберігання бази даних";
        dialog.UseDescriptionForTitle = true;
        dialog.ShowNewFolderButton = true;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            DbPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(DbPath))
            return;

        try
        {
            if (!Directory.Exists(DbPath))
            {
                Directory.CreateDirectory(DbPath);
            }

            var config = new BootConfig { DbPath = DbPath };
            _bootConfigService.Save(config);

            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Помилка доступу до папки: {ex.Message}", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}