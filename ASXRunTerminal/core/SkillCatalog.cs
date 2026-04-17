namespace ASXRunTerminal.Core;

internal static class SkillCatalog
{
    private const string ConfigDirectoryName = ".asxrun";
    private const string SkillsDirectoryName = "skills";
    private static readonly object DefaultCacheLock = new();
    private static readonly EnumerationOptions RecursiveFileEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };
    private static IReadOnlyList<SkillDefinition>? _cachedDefaultSkills;

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

    public static IReadOnlyList<SkillDefinition> List(
        IReadOnlyList<string>? discoveryDirectories = null,
        IReadOnlyList<string>? supportedFileExtensions = null,
        Func<string, string>? fileContentReader = null)
    {
        if (ShouldUseDefaultCache(
                discoveryDirectories: discoveryDirectories,
                supportedFileExtensions: supportedFileExtensions,
                fileContentReader: fileContentReader))
        {
            return GetOrCreateCachedDefaultSkills();
        }

        return ResolveSkillsWithPrecedence(
            discoveryDirectories: discoveryDirectories,
            supportedFileExtensions: supportedFileExtensions,
            fileContentReader: fileContentReader);
    }

    public static void ReloadCache()
    {
        lock (DefaultCacheLock)
        {
            _cachedDefaultSkills = null;
        }
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

    public static IReadOnlyList<SkillDefinition> LoadFileSkills(
        IReadOnlyList<string>? discoveryDirectories = null,
        IReadOnlyList<string>? supportedFileExtensions = null,
        Func<string, string>? fileContentReader = null)
    {
        var discoveredFiles = DiscoverSkillFiles(
            discoveryDirectories: discoveryDirectories,
            supportedFileExtensions: supportedFileExtensions);
        if (discoveredFiles.Count == 0)
        {
            return [];
        }

        var resolvedFileContentReader = fileContentReader ?? ReadSkillFileContent;
        var loadedSkills = new List<SkillDefinition>(discoveredFiles.Count);
        var sourceBySkillName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skillFilePath in discoveredFiles)
        {
            var resolvedSkillFilePath = Path.GetFullPath(skillFilePath);
            var fileContent = ReadSkillFileWithFriendlyErrors(
                resolvedSkillFilePath,
                resolvedFileContentReader);

            var skill = SkillFileFormat.Parse(
                fileContent,
                sourceLabel: resolvedSkillFilePath);

            if (sourceBySkillName.TryGetValue(skill.Name, out var existingSourcePath))
            {
                throw new InvalidOperationException(
                    BuildInvalidSkillFileMessage(
                        resolvedSkillFilePath,
                        $"O metadado obrigatorio '{SkillFileFormat.NameMetadataKey}' esta duplicado com o arquivo '{existingSourcePath}'. Cada skill de arquivo deve declarar um nome unico."));
            }

            sourceBySkillName[skill.Name] = resolvedSkillFilePath;
            loadedSkills.Add(skill);
        }

        return loadedSkills;
    }

    public static bool TryFind(
        string skillName,
        out SkillDefinition skill,
        IReadOnlyList<string>? discoveryDirectories = null,
        IReadOnlyList<string>? supportedFileExtensions = null,
        Func<string, string>? fileContentReader = null)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            skill = default;
            return false;
        }

        var trimmedName = skillName.Trim();
        foreach (var candidate in List(
                     discoveryDirectories: discoveryDirectories,
                     supportedFileExtensions: supportedFileExtensions,
                     fileContentReader: fileContentReader))
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

    private static IReadOnlyList<SkillDefinition> GetOrCreateCachedDefaultSkills()
    {
        lock (DefaultCacheLock)
        {
            if (_cachedDefaultSkills is not null)
            {
                return _cachedDefaultSkills;
            }

            _cachedDefaultSkills = ResolveSkillsWithPrecedence(
                    discoveryDirectories: null,
                    supportedFileExtensions: null,
                    fileContentReader: null)
                .ToArray();
            return _cachedDefaultSkills;
        }
    }

    private static bool ShouldUseDefaultCache(
        IReadOnlyList<string>? discoveryDirectories,
        IReadOnlyList<string>? supportedFileExtensions,
        Func<string, string>? fileContentReader)
    {
        return discoveryDirectories is null
            && supportedFileExtensions is null
            && fileContentReader is null;
    }

    private static IReadOnlyList<SkillDefinition> ResolveSkillsWithPrecedence(
        IReadOnlyList<string>? discoveryDirectories,
        IReadOnlyList<string>? supportedFileExtensions,
        Func<string, string>? fileContentReader)
    {
        var resolvedSkills = new List<SkillDefinition>(BuiltInSkills.Count);
        var resolvedSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileSkill in ResolveFileSkillsWithDirectoryPrecedence(
                     discoveryDirectories: discoveryDirectories,
                     supportedFileExtensions: supportedFileExtensions,
                     fileContentReader: fileContentReader))
        {
            if (resolvedSkillNames.Add(fileSkill.Name))
            {
                resolvedSkills.Add(fileSkill);
            }
        }

        foreach (var builtInSkill in BuiltInSkills)
        {
            if (resolvedSkillNames.Add(builtInSkill.Name))
            {
                resolvedSkills.Add(builtInSkill);
            }
        }

        return resolvedSkills;
    }

    private static IReadOnlyList<SkillDefinition> ResolveFileSkillsWithDirectoryPrecedence(
        IReadOnlyList<string>? discoveryDirectories,
        IReadOnlyList<string>? supportedFileExtensions,
        Func<string, string>? fileContentReader)
    {
        var resolvedDirectories = discoveryDirectories ?? GetDiscoveryDirectories();
        if (resolvedDirectories.Count == 0)
        {
            return [];
        }

        var resolvedSkills = new List<SkillDefinition>();
        var resolvedSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedDirectories = new HashSet<string>(GetPathComparer());

        foreach (var directory in resolvedDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var resolvedDirectory = Path.GetFullPath(directory.Trim());
            if (!processedDirectories.Add(resolvedDirectory))
            {
                continue;
            }

            var fileSkills = LoadFileSkills(
                discoveryDirectories: [resolvedDirectory],
                supportedFileExtensions: supportedFileExtensions,
                fileContentReader: fileContentReader);

            foreach (var fileSkill in fileSkills)
            {
                if (resolvedSkillNames.Add(fileSkill.Name))
                {
                    resolvedSkills.Add(fileSkill);
                }
            }
        }

        return resolvedSkills;
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

    private static string ReadSkillFileContent(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    private static string ReadSkillFileWithFriendlyErrors(
        string skillFilePath,
        Func<string, string> fileContentReader)
    {
        try
        {
            return fileContentReader(skillFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                BuildInvalidSkillFileMessage(
                    skillFilePath,
                    $"Nao foi possivel ler o arquivo. {ex.Message}"),
                ex);
        }
    }

    private static string BuildInvalidSkillFileMessage(string skillFilePath, string detail)
    {
        return $"Arquivo de skill invalido em '{skillFilePath}'. {detail}";
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
