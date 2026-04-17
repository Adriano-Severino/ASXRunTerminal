namespace ASXRunTerminal.Core;

internal static class SkillCatalog
{
    private const string ConfigDirectoryName = ".asxrun";
    private const string SkillsDirectoryName = "skills";
    private static readonly EnumerationOptions RecursiveFileEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };

    private static readonly IReadOnlyList<SkillDefinition> BuiltInSkills =
    [
        new SkillDefinition(
            Name: "code-review",
            Description: "Revisa codigo com foco em bugs, riscos e testes faltantes.",
            Instruction:
                """
                Atue como um engenheiro de software senior em code review.
                Priorize corretude, regressao, seguranca e cobertura de testes.
                Liste os problemas por severidade, com recomendacoes objetivas.
                """),
        new SkillDefinition(
            Name: "bugfix",
            Description: "Foca em diagnosticar causa raiz e corrigir defeitos.",
            Instruction:
                """
                Atue como um especialista em correcoes de bugs.
                Identifique a causa raiz, proponha a menor mudanca segura e valide com testes.
                Explique o risco de regressao e como mitiga-lo.
                """),
        new SkillDefinition(
            Name: "refactor",
            Description: "Refatora mantendo comportamento e melhorando estrutura.",
            Instruction:
                """
                Atue como um engenheiro senior em refatoracao.
                Preserve comportamento observavel, reduza complexidade e melhore legibilidade.
                Inclua ajustes de testes quando necessario para manter confiabilidade.
                """),
        new SkillDefinition(
            Name: "test-writer",
            Description: "Gera testes unitarios e de integracao para cenarios criticos.",
            Instruction:
                """
                Atue como especialista em testes automatizados.
                Crie testes focados em comportamento, casos de borda e regressao.
                Prefira casos deterministas, rapidos e de facil manutencao.
                """),
        new SkillDefinition(
            Name: "docs-writer",
            Description: "Escreve documentacao tecnica e de uso para features.",
            Instruction:
                """
                Atue como redator tecnico para engenharia de software.
                Produza documentacao clara com objetivo, pre-requisitos, exemplos e limites.
                Priorize instrucoes acionaveis e consistentes com o codigo.
                """)
    ];

    internal static readonly IReadOnlyList<string> SupportedSkillFileExtensions = [".md"];

    public static IReadOnlyList<SkillDefinition> List()
    {
        return BuiltInSkills;
    }

    public static IReadOnlyList<string> GetDiscoveryDirectories(
        Func<string?>? currentDirectoryResolver = null,
        Func<string?>? userHomeResolver = null)
    {
        var currentDirectory = ResolveCurrentDirectory(currentDirectoryResolver);
        var userHomeDirectory = ResolveUserHome(userHomeResolver);
        var localSkillsDirectory = Path.GetFullPath(
            Path.Combine(currentDirectory, ConfigDirectoryName, SkillsDirectoryName));
        var userSkillsDirectory = Path.GetFullPath(
            Path.Combine(userHomeDirectory, ConfigDirectoryName, SkillsDirectoryName));

        if (string.Equals(
            localSkillsDirectory,
            userSkillsDirectory,
            GetPathComparison()))
        {
            return [localSkillsDirectory];
        }

        return [localSkillsDirectory, userSkillsDirectory];
    }

    public static IReadOnlyList<string> DiscoverSkillFiles(
        IReadOnlyList<string>? discoveryDirectories = null,
        IReadOnlyList<string>? supportedFileExtensions = null)
    {
        var resolvedDirectories = discoveryDirectories ?? GetDiscoveryDirectories();
        var normalizedSupportedExtensions = NormalizeSupportedExtensions(
            supportedFileExtensions ?? SupportedSkillFileExtensions);
        if (resolvedDirectories.Count == 0 || normalizedSupportedExtensions.Count == 0)
        {
            return [];
        }

        var discoveredFiles = new List<string>();
        var uniqueFiles = new HashSet<string>(GetPathComparer());

        foreach (var directory in resolvedDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var resolvedDirectory = Path.GetFullPath(directory.Trim());
            if (!Directory.Exists(resolvedDirectory))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(
                         resolvedDirectory,
                         "*",
                         RecursiveFileEnumerationOptions))
            {
                if (!normalizedSupportedExtensions.Contains(Path.GetExtension(filePath)))
                {
                    continue;
                }

                var resolvedFilePath = Path.GetFullPath(filePath);
                if (uniqueFiles.Add(resolvedFilePath))
                {
                    discoveredFiles.Add(resolvedFilePath);
                }
            }
        }

        discoveredFiles.Sort(GetPathComparer());
        return discoveredFiles;
    }

    public static bool TryFind(string skillName, out SkillDefinition skill)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            skill = default;
            return false;
        }

        var trimmedName = skillName.Trim();
        foreach (var candidate in BuiltInSkills)
        {
            if (string.Equals(candidate.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
            {
                skill = candidate;
                return true;
            }
        }

        skill = default;
        return false;
    }

    private static string ResolveCurrentDirectory(Func<string?>? currentDirectoryResolver)
    {
        var resolver = currentDirectoryResolver ?? ResolveCurrentDirectoryFromEnvironment;
        var currentDirectory = resolver();
        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio atual para descobrir skills.");
        }

        return currentDirectory.Trim();
    }

    private static string ResolveUserHome(Func<string?>? userHomeResolver)
    {
        var resolver = userHomeResolver ?? ResolveUserHomeFromEnvironment;
        var userHome = resolver();
        if (string.IsNullOrWhiteSpace(userHome))
        {
            throw new InvalidOperationException(
                "Nao foi possivel resolver o diretorio home do usuario para descobrir skills.");
        }

        return userHome.Trim();
    }

    private static HashSet<string> NormalizeSupportedExtensions(
        IReadOnlyList<string> supportedFileExtensions)
    {
        var normalizedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in supportedFileExtensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                continue;
            }

            var normalizedExtension = extension.Trim();
            if (!normalizedExtension.StartsWith('.'))
            {
                normalizedExtension = $".{normalizedExtension}";
            }

            normalizedExtensions.Add(normalizedExtension);
        }

        return normalizedExtensions;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static string? ResolveCurrentDirectoryFromEnvironment()
    {
        return Directory.GetCurrentDirectory();
    }

    private static string? ResolveUserHomeFromEnvironment()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
