namespace ASXRunTerminal.Core;

internal readonly record struct OllamaHealthcheckResult(
    bool IsHealthy,
    string? Version,
    string? Error)
{
    public static OllamaHealthcheckResult Healthy(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new OllamaHealthcheckResult(IsHealthy: true, Version: version, Error: null);
    }

    public static OllamaHealthcheckResult Unhealthy(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new OllamaHealthcheckResult(IsHealthy: false, Version: null, Error: error);
    }

    public static implicit operator OllamaHealthcheckResult(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Healthy(version.ToString());
    }

    public static implicit operator OllamaHealthcheckResult(string version)
    {
        return Healthy(version);
    }
}
