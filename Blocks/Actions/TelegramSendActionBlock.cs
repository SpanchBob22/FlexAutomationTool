using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FlexAutomator.Blocks.Base;
using FlexAutomator.Services.API;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class TelegramSendActionBlock : ActionBlock
{
    public override string Type => "TelegramSend";

    private const string FormatNewLine = "NewLine";
    private const string FormatComma = "Comma";
    private const string FormatSpace = "Space";

    private static readonly Regex VariableRegex = new Regex(@"\{([a-fA-F0-9\-]{36})\}", RegexOptions.Compiled);

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        try
        {
            if (!Parameters.TryGetValue("Message", out var messageTemplate) || string.IsNullOrEmpty(messageTemplate))
                return BlockResult.Failed("Повідомлення не вказано");

            var listFormat = Parameters.TryGetValue("ListFormat", out var format) ? format : FormatNewLine;

            var (processedMessage, shouldSkip) = ProcessMessage(messageTemplate, context, listFormat);

            if (shouldSkip)
            {
                return BlockResult.Successful();
            }

            if (string.IsNullOrWhiteSpace(processedMessage))
                return BlockResult.Successful();

            var telegramService = context.ServiceProvider.GetRequiredService<TelegramBotService>();

            if (!telegramService.IsConfigured)
                return BlockResult.Failed("Telegram бот не налаштовано");

            await telegramService.SendMessageAsync(processedMessage);

            return BlockResult.Successful();
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка відправки в Telegram: {ex.Message}");
        }
    }

    private (string Result, bool ShouldSkip) ProcessMessage(string template, ExecutionContext context, string listFormat)
    {
        bool missingData = false;

        if (template.Contains("{LastResult}"))
        {
            var lastData = context.LastOutput;

            if (lastData == null)
            {
                missingData = true;
                return (string.Empty, true);
            }

            var formattedData = FormatData(lastData, listFormat);
            template = template.Replace("{LastResult}", formattedData);
        }


        template = VariableRegex.Replace(template, match =>
        {
            if (Guid.TryParse(match.Groups[1].Value, out var varId))
            {
                var value = context.GetVariable<object>(varId);
               
                if (value == null) return string.Empty;

                return FormatData(value, listFormat);
            }
            return match.Value;
        });

        return (template, missingData);
    }


    private string FormatData(object data, string format)
    {
        if (data is List<string> list)
        {
            if (list.Count == 0) return string.Empty;

            return format switch
            {
                FormatComma => string.Join(", ", list),
                FormatSpace => string.Join(" ", list),
                FormatNewLine or _ => string.Join("\n", list)
            };
        }

        return data?.ToString() ?? string.Empty;
    }
}