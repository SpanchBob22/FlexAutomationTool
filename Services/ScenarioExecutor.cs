using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FlexAutomator.Blocks.Base;
using FlexAutomator.Blocks.Triggers;
using FlexAutomator.Blocks.Actions;
using FlexAutomator.Models;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Services;

public class ScenarioExecutor
{
    private readonly LogService _logService;
    private readonly IServiceProvider _serviceProvider;

    public ScenarioExecutor(LogService logService, IServiceProvider serviceProvider)
    {
        _logService = logService;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ExecuteScenarioAsync(Scenario scenario, bool forceRun = false, Dictionary<Guid, object>? initialContext = null)
    {
        try
        {
            var blocks = DeserializeBlocks(scenario.BlocksJson);

            if (blocks.Count == 0)
            {
                _logService.Warning($"Сценарій '{scenario.Name}' не містить блоків");
                return false;
            }


            if (!forceRun)
            {
                var triggerBlock = blocks[0] as TriggerBlock;
                if (triggerBlock == null)
                {
                    _logService.Error($"Перший блок сценарію '{scenario.Name}' має бути тригером");
                    return false;
                }

                var shouldTrigger = await triggerBlock.ShouldTriggerAsync(scenario.LastExecuted);
                if (!shouldTrigger)
                {
                    return false;
                }
            }

            _logService.Info($"Запуск сценарію '{scenario.Name}'");

            try
            {
                var context = new ExecutionContext(_serviceProvider);


                if (initialContext != null)
                {
                    foreach (var kvp in initialContext)
                    {
                        context.SetVariable(kvp.Key, kvp.Value);
                    }
                }


                for (int i = 1; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    var result = await block.ExecuteAsync(context);

                    if (!result.Success)
                    {
                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            _logService.Error($"Помилка у блоці {i} ({block.Type}) сценарію '{scenario.Name}': {result.ErrorMessage}");
                        }
                        else
                        {
                            _logService.Info($"Сценарій '{scenario.Name}' зупинено на блоці {block.Type} (умова не виконана або немає даних)");
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error($"Помилка під час виконання блоків: {ex.Message}");
                return false;
            }

            _logService.Info($"Сценарій '{scenario.Name}' виконано успішно");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error($"Критична помилка виконання сценарію '{scenario.Name}': {ex.Message}");
            return false;
        }
    }

    public List<Block> DeserializeBlocks(string json)
    {
        var blocks = new List<Block>();

        if (string.IsNullOrWhiteSpace(json))
            return blocks;

        try
        {
            var array = JArray.Parse(json);

            foreach (var item in array)
            {
                var block = DeserializeBlock(item);
                if (block != null)
                {
                    blocks.Add(block);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Помилка десеріалізації блоків: {ex.Message}");
        }

        return blocks;
    }

    private Block? DeserializeBlock(JToken token)
    {
        var typeStr = token["Type"]?.ToString();
        if (string.IsNullOrEmpty(typeStr))
            return null;

        Block? block = typeStr switch
        {
            // Triggers
            "TimeTrigger" => token.ToObject<TimeTriggerBlock>(),
            "CyclicTrigger" => token.ToObject<CyclicTriggerBlock>(),
            "HotkeyTrigger" => token.ToObject<HotkeyTriggerBlock>(),
            "FileChangeTrigger" => token.ToObject<FileChangeTriggerBlock>(),
            "TelegramCommandTrigger" => token.ToObject<TelegramCommandTriggerBlock>(),

            // Actions
            "MouseClick" => token.ToObject<MouseClickActionBlock>(),
            "KeyboardInput" => token.ToObject<KeyboardInputActionBlock>(),
            "ProcessAction" => token.ToObject<ProcessActionBlock>(),
            "Delay" => token.ToObject<DelayActionBlock>(),
            "YouTubeCheck" => token.ToObject<YouTubeCheckActionBlock>(),
            "TMDbCheck" => token.ToObject<TMDbCheckActionBlock>(),
            "TelegramSend" => token.ToObject<TelegramSendActionBlock>(),


            _ => null
        };

        return block;
    }

    public string SerializeBlocks(List<Block> blocks)
    {
        return JsonConvert.SerializeObject(blocks, Formatting.Indented);
    }
}