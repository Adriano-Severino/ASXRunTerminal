using System.Text.RegularExpressions;

namespace ASXRunTerminal.Core;

internal readonly record struct AgentExecutionPlanStep(
    int Order,
    string Stage,
    string Action,
    string ExpectedOutput);

internal readonly record struct AgentExecutionPlan(
    string Objective,
    IReadOnlyList<AgentExecutionPlanStep> Steps);

internal static partial class AgentObjectivePlanner
{
    private const int MaxObjectiveActionSteps = 4;
    private static readonly string[] ActionPairConnectors =
    [
        " e ",
        " and "
    ];
    private static readonly HashSet<string> ActionVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "analisar",
        "avaliar",
        "corrigir",
        "criar",
        "definir",
        "documentar",
        "executar",
        "implementar",
        "investigar",
        "mapear",
        "migrar",
        "otimizar",
        "planejar",
        "refatorar",
        "revisar",
        "review",
        "testar",
        "validar"
    };

    public static AgentExecutionPlan Build(string objective)
    {
        if (string.IsNullOrWhiteSpace(objective))
        {
            throw new ArgumentException(
                "O objetivo do agente nao pode estar vazio.",
                nameof(objective));
        }

        var normalizedObjective = NormalizeWhitespace(objective);
        var actionStages = ExtractActionStages(normalizedObjective);
        var steps = BuildSteps(normalizedObjective, actionStages);

        return new AgentExecutionPlan(normalizedObjective, steps);
    }

    private static IReadOnlyList<string> ExtractActionStages(string objective)
    {
        var fragments = PlanSplitRegex()
            .Split(objective)
            .Select(NormalizeFragment)
            .Where(static fragment => fragment.Length > 0)
            .ToArray();
        if (fragments.Length == 0)
        {
            return [objective];
        }

        var stages = new List<string>(fragments.Length);

        foreach (var fragment in fragments)
        {
            AppendFragmentStages(fragment, stages);
        }

        if (stages.Count == 0)
        {
            stages.Add(objective);
        }

        return stages
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxObjectiveActionSteps)
            .ToArray();
    }

    private static void AppendFragmentStages(string fragment, List<string> stages)
    {
        var pendingFragments = new Queue<string>();
        pendingFragments.Enqueue(fragment);

        while (pendingFragments.Count > 0)
        {
            var currentFragment = pendingFragments.Dequeue();

            if (TrySplitActionPair(currentFragment, out var firstAction, out var remainingAction))
            {
                pendingFragments.Enqueue(firstAction);
                pendingFragments.Enqueue(remainingAction);
                continue;
            }

            var normalizedFragment = NormalizeFragment(currentFragment);
            if (normalizedFragment.Length > 0)
            {
                stages.Add(normalizedFragment);
            }
        }
    }

    private static bool TrySplitActionPair(
        string fragment,
        out string firstAction,
        out string remainingAction)
    {
        foreach (var connector in ActionPairConnectors)
        {
            var connectorIndex = fragment.IndexOf(
                connector,
                StringComparison.OrdinalIgnoreCase);
            if (connectorIndex <= 0)
            {
                continue;
            }

            var left = NormalizeFragment(fragment[..connectorIndex]);
            var right = NormalizeFragment(fragment[(connectorIndex + connector.Length)..]);

            if (left.Length == 0 || right.Length == 0)
            {
                continue;
            }

            if (!LooksLikeAction(left) || !LooksLikeAction(right))
            {
                continue;
            }

            firstAction = left;
            remainingAction = right;
            return true;
        }

        firstAction = string.Empty;
        remainingAction = string.Empty;
        return false;
    }

    private static bool LooksLikeAction(string value)
    {
        var firstWord = ExtractFirstWord(value);
        if (firstWord.Length == 0)
        {
            return false;
        }

        if (ActionVerbs.Contains(firstWord))
        {
            return true;
        }

        return firstWord.EndsWith("ar", StringComparison.OrdinalIgnoreCase)
            || firstWord.EndsWith("er", StringComparison.OrdinalIgnoreCase)
            || firstWord.EndsWith("ir", StringComparison.OrdinalIgnoreCase)
            || firstWord.EndsWith("ing", StringComparison.OrdinalIgnoreCase)
            || firstWord.EndsWith("ize", StringComparison.OrdinalIgnoreCase)
            || firstWord.EndsWith("ise", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractFirstWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var firstToken = value
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
            ?? string.Empty;

        return firstToken.Trim('.', ',', ';', ':', '-', '_', '(', ')', '[', ']', '{', '}', '"', '\'');
    }

    private static IReadOnlyList<AgentExecutionPlanStep> BuildSteps(
        string objective,
        IReadOnlyList<string> actionStages)
    {
        var steps = new List<AgentExecutionPlanStep>(actionStages.Count + 3);
        var order = 1;

        steps.Add(new AgentExecutionPlanStep(
            Order: order++,
            Stage: "Mapear contexto e restricoes",
            Action: $"Entender o escopo tecnico do objetivo '{objective}'.",
            ExpectedOutput: "Premissas, dependencias e riscos priorizados."));

        foreach (var actionStage in actionStages)
        {
            var normalizedAction = EnsureSentence(actionStage);
            steps.Add(new AgentExecutionPlanStep(
                Order: order++,
                Stage: $"Executar subobjetivo {order - 2}",
                Action: normalizedAction,
                ExpectedOutput: $"Evidencia concreta de progresso para '{TrimTrailingPeriod(normalizedAction)}'."));
        }

        steps.Add(new AgentExecutionPlanStep(
            Order: order++,
            Stage: "Verificar resultados",
            Action: "Validar corretude com testes, comandos ou inspecoes objetivas.",
            ExpectedOutput: "Falhas identificadas, impactos avaliados e status de qualidade."));

        steps.Add(new AgentExecutionPlanStep(
            Order: order,
            Stage: "Refinar e concluir",
            Action: "Aplicar ajustes finais e consolidar a entrega.",
            ExpectedOutput: "Resumo final com mudancas realizadas, riscos residuais e proximos passos."));

        return steps;
    }

    private static string NormalizeFragment(string value)
    {
        return NormalizeWhitespace(value)
            .Trim('.', ',', ';', ':');
    }

    private static string NormalizeWhitespace(string value)
    {
        return MultiWhitespaceRegex().Replace(value.Trim(), " ");
    }

    private static string EnsureSentence(string value)
    {
        var normalized = NormalizeFragment(value);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var firstCharacter = char.ToUpperInvariant(normalized[0]);
        var sentence = normalized.Length == 1
            ? firstCharacter.ToString()
            : $"{firstCharacter}{normalized[1..]}";

        return sentence.EndsWith(".", StringComparison.Ordinal)
            ? sentence
            : $"{sentence}.";
    }

    private static string TrimTrailingPeriod(string value)
    {
        return value.TrimEnd('.');
    }

    [GeneratedRegex(@"\s*(?:;|->|=>|\bdepois\b|\bem seguida\b|\bentao\b|\bthen\b)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlanSplitRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiWhitespaceRegex();
}
