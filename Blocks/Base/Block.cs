namespace FlexAutomator.Blocks.Base;

public abstract class Block
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public abstract string Type { get; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    
    public virtual string Description => string.Join(", ", Parameters.Select(p => $"{p.Key}: {p.Value}"));

    public abstract Task<BlockResult> ExecuteAsync(ExecutionContext context);
}

public class BlockResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Value { get; set; }

    public static BlockResult Successful(object? value = null) => new() { Success = true, Value = value };
    public static BlockResult Failed(string error) => new() { Success = false, ErrorMessage = error };
}
