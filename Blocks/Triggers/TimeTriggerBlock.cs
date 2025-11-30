using FlexAutomator.Blocks.Base;

namespace FlexAutomator.Blocks.Triggers;

public class TimeTriggerBlock : TriggerBlock
{
    public override string Type => "TimeTrigger";

    public override async Task<bool> ShouldTriggerAsync(DateTime? lastExecuted = null)
    {
        await Task.CompletedTask;

        if (!Parameters.TryGetValue("Time", out var timeStr) || string.IsNullOrEmpty(timeStr))
            return false;

        if (!TimeOnly.TryParse(timeStr, out var targetTime))
            return false;

        var now = DateTime.Now;
        var currentTime = TimeOnly.FromDateTime(now);

        if (Parameters.TryGetValue("Days", out var daysStr) && !string.IsNullOrEmpty(daysStr))
        {
            var allowedDays = daysStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => Enum.TryParse<DayOfWeek>(d.Trim(), out var day) ? day : (DayOfWeek?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();

            if (allowedDays.Count > 0 && !allowedDays.Contains(now.DayOfWeek))
                return false;
        }

        if (lastExecuted.HasValue)
        {
            var lastExecTime = TimeOnly.FromDateTime(lastExecuted.Value);
            
            if (lastExecTime <= targetTime && currentTime >= targetTime)
            {
                return true;
            }
        }
        else
        {
            if (currentTime >= targetTime && currentTime < targetTime.AddMinutes(1))
            {
                return true;
            }
        }

        return false;
    }
}
