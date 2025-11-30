namespace FlexAutomator.Blocks.Base;

public abstract class TriggerBlock : Block
{
    public abstract Task<bool> ShouldTriggerAsync(DateTime? lastExecuted = null);

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        var shouldTrigger = await ShouldTriggerAsync();
        return BlockResult.Successful(shouldTrigger);
    }
}
