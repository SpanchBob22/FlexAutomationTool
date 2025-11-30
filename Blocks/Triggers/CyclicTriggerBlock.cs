using FlexAutomator.Blocks.Base;

namespace FlexAutomator.Blocks.Triggers;

public class CyclicTriggerBlock : TriggerBlock
{
    public override string Type => "CyclicTrigger";
    
    private DateTime _nextTriggerTime = DateTime.MinValue;

    public override async Task<bool> ShouldTriggerAsync(DateTime? lastExecuted = null)
    {
        await Task.CompletedTask;

        if (!Parameters.TryGetValue("Interval", out var intervalStr) || string.IsNullOrEmpty(intervalStr))
            return false;

        if (!int.TryParse(intervalStr, out var interval) || interval <= 0)
            return false;

        var unit = Parameters.TryGetValue("Unit", out var unitStr) ? unitStr : "Хвилини";

        TimeSpan intervalSpan = unit.ToLower() switch
        {
            "секунди" or "seconds" => TimeSpan.FromSeconds(interval),
            "хвилини" or "minutes" => TimeSpan.FromMinutes(interval),
            "години" or "hours" => TimeSpan.FromHours(interval),
            "дні" or "days" => TimeSpan.FromDays(interval),
            _ => TimeSpan.FromMinutes(interval)
        };

        var now = DateTime.Now;

        if (_nextTriggerTime == DateTime.MinValue)
        {
            _nextTriggerTime = lastExecuted?.Add(intervalSpan) ?? now;
        }

        if (now >= _nextTriggerTime)
        {
            _nextTriggerTime = now.Add(intervalSpan);
            return true;
        }

        return false;
    }
}
