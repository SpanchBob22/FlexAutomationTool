using System;
using CommunityToolkit.Mvvm.Messaging.Messages;
namespace FlexAutomator.Models;
public class ScenarioExecutedMessage : ValueChangedMessage<Guid>
{
    public DateTime ExecutedAt { get; }
    public ScenarioExecutedMessage(Guid scenarioId, DateTime executedAt) : base(scenarioId)
    {
        ExecutedAt = executedAt;
    }
}