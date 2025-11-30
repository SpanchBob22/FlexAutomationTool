using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms; 
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Native;
using FlexAutomator.Blocks.Base;
using FlexAutomator.Services;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class KeyboardInputActionBlock : ActionBlock
{
    public override string Type => "KeyboardInput";
    private const string FormatNewLine = "NewLine";
    private const string FormatComma = "Comma";
    private const string FormatSpace = "Space";
    private static readonly Regex VariableRegex = new Regex(@"\{([a-fA-F0-9\-]{36})\}", RegexOptions.Compiled);

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        await Task.CompletedTask;
        try
        {
            if (!Parameters.TryGetValue("Text", out var textTemplate) || string.IsNullOrEmpty(textTemplate))
                return BlockResult.Failed("Текст не вказано");

            var listFormat = Parameters.TryGetValue("ListFormat", out var format) ? format : FormatSpace;
            var inputMethod = Parameters.TryGetValue("InputMethod", out var method) ? method : "Type";
            var inputMode = Parameters.TryGetValue("InputMode", out var mode) ? mode : "CharByChar"; 
            var forceEscape = Parameters.TryGetValue("EscapeBackslashes", out var escapeStr) && bool.TryParse(escapeStr, out var esc) && esc;

            var logService = context.ServiceProvider.GetService<LogService>();
            logService?.Info($"[KeyboardInput] Шаблон: '{textTemplate}'");

            var (processedText, shouldSkip) = ProcessText(textTemplate, context, listFormat, logService);
            if (shouldSkip || string.IsNullOrEmpty(processedText))
            {
                logService?.Info($"[KeyboardInput] Пропущено (немає або порожньо)");
                return BlockResult.Successful();
            }

            var preview = processedText.Length > 100 ? processedText.Substring(0, 100) + "..." : processedText;
            logService?.Info($"[KeyboardInput] Фінальний текст ({inputMethod}, {inputMode}): '{preview}'");

            var simulator = new InputSimulator();

            if (string.Equals(inputMethod, "Paste", StringComparison.OrdinalIgnoreCase))
            {
                PasteTextRobust(simulator, processedText);
            }
            else
            {
                if (forceEscape)
                {
                    processedText = processedText.Replace("\\", "\\\\");
                    logService?.Info("[KeyboardInput] Екранування слешей ввімкнено");
                }

                if (string.Equals(inputMode, "CharByChar", StringComparison.OrdinalIgnoreCase))
                {
                    SendTextCharByChar(simulator, processedText); 
                }
                else
                {
                    simulator.Keyboard.TextEntry(processedText); 
                }
                Thread.Sleep(50); 
            }

            logService?.Info("[KeyboardInput] Текст введено успішно");
            return BlockResult.Successful();
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка введення тексту: {ex.Message}");
        }
    }

    private void PasteTextRobust(InputSimulator simulator, string text)
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null)
        {
            throw new Exception("Не вдалося записати в буфер обміну. Спробуйте метод 'Type'.", threadEx);
        }
        simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
        Thread.Sleep(150);
    }

    private void SendTextCharByChar(InputSimulator simulator, string text)
    {
        foreach (char c in text)
        {
            simulator.Keyboard.TextEntry(c.ToString());
            Thread.Sleep(10); 
        }
    }

    private (string Result, bool ShouldSkip) ProcessText(string template, ExecutionContext context, string listFormat, LogService? logService)
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
            logService?.Info($"[KeyboardInput] Замінено {{LastResult}} на: '{formattedData}'");
        }

        var allVars = context.GetAllVariables();
        logService?.Info($"[KeyboardInput] Змінних у контексті: {allVars.Count}");
        foreach (var kvp in allVars)
        {
            var valuePreview = kvp.Value?.ToString() ?? "null";
            if (valuePreview.Length > 50) valuePreview = valuePreview.Substring(0, 50) + "...";
            logService?.Info($"[KeyboardInput] {kvp.Key} = '{valuePreview}'");
        }

        int replacementCount = 0;
        template = VariableRegex.Replace(template, match =>
        {
            if (Guid.TryParse(match.Groups[1].Value, out var varId))
            {
                var value = context.GetVariable<object>(varId);
                if (value == null)
                {
                    logService?.Info($"[KeyboardInput] GUID {match.Groups[1].Value}: НЕ ЗНАЙДЕНО → ''");
                    return string.Empty;
                }
                var formatted = FormatData(value, listFormat);
                replacementCount++;
                logService?.Info($"[KeyboardInput] Заміна #{replacementCount}: GUID {match.Groups[1].Value} → '{formatted}'");
                return formatted;
            }
            logService?.Info($"[KeyboardInput] Невалідний GUID: {match.Groups[1].Value} -> залишаємо як є");
            return match.Value;
        });

        logService?.Info($"[KeyboardInput] Всього замін: {replacementCount}");
        logService?.Info($"[KeyboardInput] Результат після обробки: '{template}'");

        return (template, missingData);
    }

    private string FormatData(object? data, string format)
    {
        if (data == null) return string.Empty;
        if (data is List<string> list)
        {
            if (list.Count == 0) return string.Empty;
            return format switch
            {
                FormatNewLine => string.Join("\n", list),
                FormatComma => string.Join(", ", list),
                FormatSpace or _ => string.Join(" ", list)
            };
        }
        return data.ToString() ?? string.Empty;
    }
}