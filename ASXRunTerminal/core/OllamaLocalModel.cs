namespace ASXRunTerminal.Core;

/// <summary>
/// Representa um modelo Ollama instalado localmente.
/// </summary>
internal readonly record struct OllamaLocalModel(string Name)
{
    public static OllamaLocalModel FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "O payload de modelos retornado pelo Ollama e invalido.",
                nameof(name));
        }

        return new OllamaLocalModel(name.Trim());
    }
}
