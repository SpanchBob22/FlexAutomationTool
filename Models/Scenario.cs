using System;
using CommunityToolkit.Mvvm.ComponentModel;
namespace FlexAutomator.Models;

public class Scenario : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _name = "Новий сценарій";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public string BlocksJson { get; set; } = "[]";

    public string? LastContextJson { get; set; }

    private DateTime? _lastExecuted;
    public DateTime? LastExecuted
    {
        get => _lastExecuted;
        set => SetProperty(ref _lastExecuted, value);
    }

    private string _color = "#673AB7";
    public string Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }
}