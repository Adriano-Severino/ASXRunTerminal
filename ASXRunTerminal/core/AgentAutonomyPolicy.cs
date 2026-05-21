namespace ASXRunTerminal.Core;

internal enum AgentAutonomyLevel
{
    Assisted,
    SemiAutonomous,
    Autonomous
}

internal readonly record struct AgentAutonomyLevelName(string Value)
{
    private const string SupportedValuesLabel = "assistido, semi-autonomo, autonomo";

    public static implicit operator string(AgentAutonomyLevelName levelName)
    {
        return levelName.Value;
    }

    public static implicit operator AgentAutonomyLevelName(AgentAutonomyLevel level)
    {
        return level switch
        {
            AgentAutonomyLevel.Assisted => new AgentAutonomyLevelName("assistido"),
            AgentAutonomyLevel.SemiAutonomous => new AgentAutonomyLevelName("semi-autonomo"),
            AgentAutonomyLevel.Autonomous => new AgentAutonomyLevelName("autonomo"),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Nivel de autonomia invalido.")
        };
    }

    public static implicit operator AgentAutonomyLevel(AgentAutonomyLevelName levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName.Value))
        {
            throw new InvalidOperationException(
                $"O valor 'autonomyLevel' deve ser um entre: {SupportedValuesLabel}.");
        }

        var normalized = levelName.Value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "assistido" or "assisted" => AgentAutonomyLevel.Assisted,
            "semi-autonomo" or "semi_autonomo" or "semiautonomo" or "semi-autonomous" or "semi_autonomous" =>
                AgentAutonomyLevel.SemiAutonomous,
            "autonomo" or "autonomous" => AgentAutonomyLevel.Autonomous,
            _ => throw new InvalidOperationException(
                $"O valor 'autonomyLevel' deve ser um entre: {SupportedValuesLabel}.")
        };
    }
}

internal readonly record struct AgentAutonomyPolicy(AgentAutonomyLevel Level)
{
    public static AgentAutonomyPolicy Default => new(AgentAutonomyLevel.Autonomous);

    public string LevelName => (string)(AgentAutonomyLevelName)Level;

    public bool AllowsAutomaticValidation =>
        Level is AgentAutonomyLevel.SemiAutonomous or AgentAutonomyLevel.Autonomous;

    public bool AllowsAutoCorrection =>
        Level is AgentAutonomyLevel.Autonomous;

    public static implicit operator AgentAutonomyPolicy(AgentAutonomyLevel level)
    {
        return new AgentAutonomyPolicy(level);
    }
}
