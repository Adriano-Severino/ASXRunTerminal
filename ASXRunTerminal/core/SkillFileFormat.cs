using System.Text.RegularExpressions;

namespace ASXRunTerminal.Core;

internal static class SkillFileFormat
{
    internal const string SkillFileName = "SKILL.md";
    internal const string NameMetadataKey = "name";
    internal const string DescriptionMetadataKey = "description";
    internal const string InstructionMetadataKey = "instruction";
    private const string MetadataDelimiter = "---";
    private const string NameMetadataPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
    private static readonly Regex NameMetadataRegex = new(
        NameMetadataPattern,
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static readonly IReadOnlyList<string> RequiredMetadataKeys =
    [
        NameMetadataKey,
        DescriptionMetadataKey,
        InstructionMetadataKey
    ];

    private static readonly HashSet<string> RequiredMetadataKeySet =
        new(RequiredMetadataKeys, StringComparer.OrdinalIgnoreCase);

    internal static SkillDefinition Parse(string fileContent, string? sourceLabel = null)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            throw new InvalidOperationException(
                BuildInvalidSkillMessage(sourceLabel, "O conteudo esta vazio."));
        }

        var lines = NormalizeLineEndings(fileContent).Split('\n');
        lines[0] = lines[0].TrimStart('\uFEFF');

        if (!IsMetadataDelimiter(lines[0]))
        {
            throw new InvalidOperationException(
                BuildInvalidSkillMessage(
                    sourceLabel,
                    "O cabecalho de metadados deve iniciar com '---'."));
        }

        var closingDelimiterIndex = FindClosingMetadataDelimiter(lines);
        if (closingDelimiterIndex < 0)
        {
            throw new InvalidOperationException(
                BuildInvalidSkillMessage(
                    sourceLabel,
                    "O cabecalho de metadados deve terminar com '---'."));
        }

        var metadata = ParseMetadata(lines, closingDelimiterIndex, sourceLabel);
        SkillDefinition skill = metadata;
        return skill;
    }

    internal static string BuildTemplate(
        string name = "my-skill",
        string description = "Explique em uma frase o objetivo da skill.",
        string instruction = "Descreva como o modelo deve agir ao usar esta skill.")
    {
        var metadata = new RawSkillMetadata(
            Name: NormalizeRequiredValue(name, NameMetadataKey),
            Description: NormalizeRequiredValue(description, DescriptionMetadataKey),
            Instruction: NormalizeRequiredValue(instruction, InstructionMetadataKey));
        ValidateMetadataSchema(metadata, SkillFileName);

        var outputLines = new List<string>
        {
            MetadataDelimiter,
            $"{NameMetadataKey}: {metadata.Name}",
            $"{DescriptionMetadataKey}: {metadata.Description}",
            $"{InstructionMetadataKey}: |"
        };

        foreach (var line in NormalizeLineEndings(metadata.Instruction).Split('\n'))
        {
            outputLines.Add($"  {line.TrimEnd()}");
        }

        outputLines.Add(MetadataDelimiter);
        return string.Join(Environment.NewLine, outputLines) + Environment.NewLine;
    }

    private static RawSkillMetadata ParseMetadata(
        IReadOnlyList<string> lines,
        int closingDelimiterIndex,
        string? sourceLabel)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var lineIndex = 1; lineIndex < closingDelimiterIndex; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmedLine = rawLine.TrimEnd();
            var separatorIndex = trimmedLine.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException(
                    BuildInvalidSkillMessage(
                        sourceLabel,
                        $"Linha de metadado invalida na linha {lineIndex + 1}. Use o formato chave: valor."));
            }

            var key = trimmedLine[..separatorIndex].Trim();
            var value = trimmedLine[(separatorIndex + 1)..].TrimStart();

            if (!RequiredMetadataKeySet.Contains(key))
            {
                throw new InvalidOperationException(
                    BuildInvalidSkillMessage(
                        sourceLabel,
                        $"O metadado '{key}' na linha {lineIndex + 1} nao faz parte do schema suportado. Use apenas: {string.Join(", ", RequiredMetadataKeys)}."));
            }

            if (metadata.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    BuildInvalidSkillMessage(
                        sourceLabel,
                        $"O metadado '{key}' foi definido mais de uma vez."));
            }

            var resolvedValue = string.Equals(value, "|", StringComparison.Ordinal)
                ? ParseMultilineValue(lines, closingDelimiterIndex, ref lineIndex)
                : Unquote(value).Trim();

            metadata[key] = resolvedValue;
        }

        var parsedMetadata = new RawSkillMetadata(
            Name: ReadRequiredMetadata(metadata, NameMetadataKey, sourceLabel),
            Description: ReadRequiredMetadata(metadata, DescriptionMetadataKey, sourceLabel),
            Instruction: ReadRequiredMetadata(metadata, InstructionMetadataKey, sourceLabel));

        ValidateMetadataSchema(parsedMetadata, sourceLabel);
        return parsedMetadata;
    }

    private static string ParseMultilineValue(
        IReadOnlyList<string> lines,
        int closingDelimiterIndex,
        ref int currentLineIndex)
    {
        var values = new List<string>();
        var hasContent = false;

        for (var nextLineIndex = currentLineIndex + 1;
            nextLineIndex < closingDelimiterIndex;
            nextLineIndex++)
        {
            var nextLine = lines[nextLineIndex];
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                if (hasContent)
                {
                    values.Add(string.Empty);
                }

                currentLineIndex = nextLineIndex;
                continue;
            }

            if (!IsMultilineValue(nextLine))
            {
                break;
            }

            values.Add(nextLine.TrimStart());
            hasContent = true;
            currentLineIndex = nextLineIndex;
        }

        return string.Join('\n', values).Trim();
    }

    private static bool IsMultilineValue(string line)
    {
        return line.StartsWith("  ", StringComparison.Ordinal)
            || line.StartsWith('\t');
    }

    private static string ReadRequiredMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string metadataKey,
        string? sourceLabel)
    {
        if (!metadata.TryGetValue(metadataKey, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                BuildInvalidSkillMessage(
                    sourceLabel,
                    $"O metadado obrigatorio '{metadataKey}' esta ausente ou vazio."));
        }

        return value.Trim();
    }

    private static string NormalizeRequiredValue(string value, string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"O metadado obrigatorio '{metadataKey}' nao pode estar vazio.");
        }

        return value.Trim();
    }

    private static void ValidateMetadataSchema(RawSkillMetadata metadata, string? sourceLabel)
    {
        if (!NameMetadataRegex.IsMatch(metadata.Name))
        {
            throw new InvalidOperationException(
                BuildInvalidSkillMessage(
                    sourceLabel,
                    $"O metadado obrigatorio '{NameMetadataKey}' deve usar kebab-case (letras minusculas, numeros e '-'). Valor recebido: '{metadata.Name}'."));
        }
    }

    private static int FindClosingMetadataDelimiter(IReadOnlyList<string> lines)
    {
        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            if (IsMetadataDelimiter(lines[lineIndex]))
            {
                return lineIndex;
            }
        }

        return -1;
    }

    private static bool IsMetadataDelimiter(string line)
    {
        return string.Equals(line.Trim(), MetadataDelimiter, StringComparison.Ordinal);
    }

    private static string BuildInvalidSkillMessage(string? sourceLabel, string detail)
    {
        var resolvedSource = string.IsNullOrWhiteSpace(sourceLabel)
            ? SkillFileName
            : sourceLabel.Trim();

        return
            $"Arquivo de skill invalido em '{resolvedSource}'. {detail} Schema esperado: delimitadores '---' com metadados obrigatorios '{NameMetadataKey}', '{DescriptionMetadataKey}' e '{InstructionMetadataKey}'.";
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private readonly record struct RawSkillMetadata(
        string Name,
        string Description,
        string Instruction)
    {
        public static implicit operator SkillDefinition(RawSkillMetadata metadata)
        {
            return new SkillDefinition(
                Name: metadata.Name,
                Description: metadata.Description,
                Instruction: metadata.Instruction);
        }
    }
}
