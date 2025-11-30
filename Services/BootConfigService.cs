using System.IO;
using System.Text.Json;
using FlexAutomator.Models;
namespace FlexAutomator.Services;
public class BootConfigService
{
    private readonly string _configFolderPath;
    private readonly string _configFilePath;
    public BootConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configFolderPath = Path.Combine(appData, "FlexAutomator");
        _configFilePath = Path.Combine(_configFolderPath, "app_config.json");
    }

    public BootConfig? Load()
    {
        if (!File.Exists(_configFilePath))
            return null;

        try
        {
            var json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<BootConfig>(json);
        }
        catch
        {
            return null;
        }
    }
    public void Save(BootConfig config)
    {
        if (!Directory.Exists(_configFolderPath))
        {
            Directory.CreateDirectory(_configFolderPath);
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configFilePath, json);
    }
}