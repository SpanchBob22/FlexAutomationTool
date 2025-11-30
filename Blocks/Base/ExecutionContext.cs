namespace FlexAutomator.Blocks.Base;

public class ExecutionContext
{
    private readonly Dictionary<Guid, object> _variables = new();

    public object? LastOutput { get; private set; }

    public IServiceProvider ServiceProvider { get; }

    public ExecutionContext(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }


    public void SetVariable(Guid blockId, object value)
    {
        _variables[blockId] = value;
        LastOutput = value;
    }

    public T? GetVariable<T>(Guid blockId)
    {
        if (_variables.TryGetValue(blockId, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public T? GetLastOutput<T>()
    {
        if (LastOutput is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public bool HasVariable(Guid blockId)
    {
        return _variables.ContainsKey(blockId);
    }

    public Dictionary<Guid, object> GetAllVariables()
    {
        return new Dictionary<Guid, object>(_variables);
    }
}