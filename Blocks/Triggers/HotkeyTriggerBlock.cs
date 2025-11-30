using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using FlexAutomator.Blocks.Base;

namespace FlexAutomator.Blocks.Triggers;

public class HotkeyTriggerBlock : TriggerBlock
{
    public override string Type => "HotkeyTrigger";
    private bool _isTriggered;

    public void Trigger()
    {
        _isTriggered = true;
    }

    public override async Task<bool> ShouldTriggerAsync(DateTime? lastExecuted = null)
    {
        await Task.CompletedTask;

        if (_isTriggered)
        {
            _isTriggered = false;
            return true;
        }

        return false;
    }
}
