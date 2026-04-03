using OllamaSharp.Models;

namespace ASXRunTerminal.Core;

internal readonly record struct OllamaLocalModel(string Name)
{
    public static implicit operator OllamaLocalModel(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var resolvedName = string.IsNullOrWhiteSpace(model.Name)
            ? model.ModelName
            : model.Name;

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            throw new ArgumentException(
                "O payload de modelos retornado pelo Ollama e invalido.",
                nameof(model));
        }

        return new OllamaLocalModel(resolvedName.Trim());
    }
}
