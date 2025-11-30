using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FlexAutomator.Data;
using FlexAutomator.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexAutomator.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly BootConfigService _bootConfigService;

    public DatabaseService(BootConfigService bootConfigService)
    {
        _bootConfigService = bootConfigService;
        var config = _bootConfigService.Load();

        if (config == null || string.IsNullOrWhiteSpace(config.DbPath))
        {
            throw new InvalidOperationException("Не знайдено конфігурацію бази даних.");
        }

        var selectedPath = config.DbPath;
        string dbFilePath;

        try
        {
            if (!Directory.Exists(selectedPath))
            {
                Directory.CreateDirectory(selectedPath);
            }

            var rootDbFile = Path.Combine(selectedPath, "flex.db");
            var subFolder = Path.Combine(selectedPath, "database");
            var subDbFile = Path.Combine(subFolder, "flex.db");


            if (File.Exists(subDbFile))
            {
                dbFilePath = subDbFile;
            }

            else if (File.Exists(rootDbFile))
            {
                dbFilePath = rootDbFile;
            }

            else
            {
                var dirName = new DirectoryInfo(selectedPath).Name;

                if (string.Equals(dirName, "database", StringComparison.OrdinalIgnoreCase))
                {
                    dbFilePath = rootDbFile;
                }
                else
                {
                    if (!Directory.Exists(subFolder))
                    {
                        Directory.CreateDirectory(subFolder);
                    }
                    dbFilePath = subDbFile;
                }
            }

            var finalDir = Path.GetDirectoryName(dbFilePath);
            if (finalDir != null && !Directory.Exists(finalDir))
            {
                Directory.CreateDirectory(finalDir);
            }

            _connectionString = $"Data Source={dbFilePath}";

            InitializeDatabase();
        }
        catch (Exception ex)
        {
            ResetConfigAndThrow(ex.Message);
            throw;
        }
    }

    private void InitializeDatabase()
    {
        try
        {
            using var context = new AppDbContext(_connectionString);
            context.Database.EnsureCreated();
        }
        catch (Exception)
        {
            throw;
        }
    }

    private void ResetConfigAndThrow(string reason)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = Path.Combine(appData, "FlexAutomator", "app_config.json");
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
        catch { }

        throw new Exception($"Помилка ініціалізації БД: {reason}. Налаштування скинуто.");
    }


    public async Task<List<Scenario>> GetAllScenariosAsync()
    {
        using var context = new AppDbContext(_connectionString);
        return await context.Scenarios.AsNoTracking().ToListAsync();
    }

    public async Task<Scenario?> GetScenarioAsync(Guid id)
    {
        using var context = new AppDbContext(_connectionString);
        return await context.Scenarios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddScenarioAsync(Scenario scenario)
    {
        using var context = new AppDbContext(_connectionString);
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
    }

    public async Task UpdateScenarioAsync(Scenario scenario)
    {
        using var context = new AppDbContext(_connectionString);
        context.Scenarios.Update(scenario);
        await context.SaveChangesAsync();
    }

    public async Task DeleteScenarioAsync(Guid id)
    {
        using var context = new AppDbContext(_connectionString);
        var scenario = await context.Scenarios.FindAsync(id);
        if (scenario != null)
        {
            context.Scenarios.Remove(scenario);
            await context.SaveChangesAsync();
        }
    }

    public async Task<AppSettings?> GetSettingsAsync()
    {
        using var context = new AppDbContext(_connectionString);
        return await context.Settings.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var context = new AppDbContext(_connectionString);

        var existing = await context.Settings.FirstOrDefaultAsync();
        if (existing == null)
        {
            context.Settings.Add(settings);
        }
        else
        {
            existing.YouTubeApiKey = settings.YouTubeApiKey;
            existing.TMDbApiKey = settings.TMDbApiKey;
            existing.TelegramBotToken = settings.TelegramBotToken;
            existing.TelegramChatId = settings.TelegramChatId;
            existing.PairingCode = settings.PairingCode;
            existing.IsTelegramBotEnabled = settings.IsTelegramBotEnabled;
        }

        await context.SaveChangesAsync();
    }
}