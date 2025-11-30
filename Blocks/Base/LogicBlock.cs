namespace FlexAutomator.Blocks.Base;

public abstract class LogicBlock : Block
{
    public List<Block> ChildBlocks { get; set; } = new();

    protected async Task<BlockResult> ExecuteChildBlocksAsync(ExecutionContext context)
    {
        foreach (var block in ChildBlocks)
        {
            var result = await block.ExecuteAsync(context);
            if (!result.Success)
            {
                return result;
            }
        }
        return BlockResult.Successful();
    }
}
