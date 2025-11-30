using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FlexAutomator.Models;
using FlexAutomator.ViewModels;

namespace FlexAutomator.Views;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private System.Windows.Forms.NotifyIcon _notifyIcon = null!;

    private bool _isExplicitExit = false;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "FlexAutomator.exe"),
            Visible = true,
            Text = "Flex Automator"
        };

        _notifyIcon.DoubleClick += (s, args) => ShowWindow();

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        contextMenu.Items.Add("Відкрити", null, (s, e) => ShowWindow());

        contextMenu.Items.Add("Вихід", null, (s, e) =>
        {
            _isExplicitExit = true;

            Close();

            _notifyIcon.Dispose();

            System.Windows.Application.Current.Shutdown();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }


    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
        base.OnStateChanged(e);
    }

    private void Scenario_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Scenario scenario)
        {
            if (ViewModel.EditScenarioCommand.CanExecute(scenario))
            {
                ViewModel.EditScenarioCommand.Execute(scenario);
            }
        }
    }
}