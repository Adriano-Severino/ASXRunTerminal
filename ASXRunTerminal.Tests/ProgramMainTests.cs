using System.Text.RegularExpressions;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class ProgramMainTests
{
    [Fact]
    public void Main_WithoutArguments_ReturnsSuccess_AndWritesInfoLog()
    {
        var result = ExecuteMain();

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] ASXRunTerminal CLI inicializado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void RunForTests_UsesInjectedUserConfigInitializer()
    {
        var invocationCount = 0;

        var exitCode = Program.RunForTests(
            ["--help"],
            () => invocationCount++);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(1, invocationCount);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Main_HelpArgument_ReturnsSuccess_AndWritesUsage(string argument)
    {
        var result = ExecuteMain(argument);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("ASXRunTerminal CLI", result.StdOut);
        Assert.Contains("Uso:", result.StdOut);
        Assert.Contains("Opcoes:", result.StdOut);
        Assert.Contains("Comandos:", result.StdOut);
        Assert.Contains("ask \"prompt\"", result.StdOut);
        Assert.Contains("asxrun agent [--model <modelo>] \"objetivo\"", result.StdOut);
        Assert.Contains("asxrun chat", result.StdOut);
        Assert.Contains("asxrun doctor", result.StdOut);
        Assert.Contains("asxrun models", result.StdOut);
        Assert.Contains("asxrun context", result.StdOut);
        Assert.Contains("asxrun patch [--dry-run] <arquivo-json>", result.StdOut);
        Assert.Contains("asxrun history", result.StdOut);
        Assert.Contains("asxrun history [--clear]", result.StdOut);
        Assert.Contains("asxrun resume [<session-id>]", result.StdOut);
        Assert.Contains("asxrun mcp list", result.StdOut);
        Assert.Contains("asxrun mcp add <nome> --command <cmd> [--arg <valor>]...", result.StdOut);
        Assert.Contains("asxrun mcp add <nome> --url <endpoint> [--transport http|sse]", result.StdOut);
        Assert.Contains("asxrun mcp remove <nome>", result.StdOut);
        Assert.Contains("asxrun mcp test <nome>", result.StdOut);
        Assert.Contains("asxrun config get <chave>", result.StdOut);
        Assert.Contains("asxrun config set <chave> <valor>", result.StdOut);
        Assert.Contains("asxrun skills", result.StdOut);
        Assert.Contains("asxrun skills init", result.StdOut);
        Assert.Contains("asxrun skills reload", result.StdOut);
        Assert.Contains("asxrun skill <nome> [--model <modelo>] \"prompt\"", result.StdOut);
        Assert.Contains("--model <nome>", result.StdOut);
        Assert.Contains("--max-steps <n>", result.StdOut);
        Assert.Contains("--max-time <v>", result.StdOut);
        Assert.Contains("--max-cost <v>", result.StdOut);
        Assert.Contains("asxrun ask [--model <modelo>] \"prompt\"", result.StdOut);
        Assert.Contains("padrao: qwen3.5:4b", result.StdOut);
        Assert.Contains("ASXRUN_DEFAULT_MODEL=<nome>", result.StdOut);
        Assert.Contains("Codigos de saida:", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Main_VersionArgument_ReturnsSuccess_AndWritesVersion(string argument)
    {
        var result = ExecuteMain(argument);
        var output = result.StdOut.Trim();

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal("asxrun 0.1.0", output);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_UnknownArgument_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("--foo");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A opcao '--foo' nao e reconhecida.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun --help' para ver as opcoes disponiveis.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_HelpAndVersionTogether_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("--help", "--version");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. Escolha apenas uma opcao por execucao: --help ou --version.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun --help' para ver as opcoes disponiveis.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AskCommand_WithQuotedPrompt_ReturnsSuccess_AndWritesPrompt()
    {
        const string prompt = "explique o padrao repository";
        var result = ExecuteMainWithExecutor(
            static value => $"Prompt: {value}",
            "ask",
            prompt);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: tool call.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: diff.", result.StdOut);
        Assert.Contains("[CONN]", result.StdOut);
        Assert.Contains("[TOOL]", result.StdOut);
        Assert.Contains("[WORK]", result.StdOut);
        Assert.Contains("[DIFF]", result.StdOut);
        Assert.Contains($"Prompt: {prompt}", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Contains("[DONE]", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithMultipleArguments_ReturnsSuccess_AndJoinsPrompt()
    {
        var result = ExecuteMainWithExecutor(
            static value => $"Prompt: {value}",
            "ask",
            "gerar",
            "teste",
            "unitario");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("Prompt: gerar teste unitario", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithStreamingExecutor_ReturnsSuccess_AndWritesPromptInChunks()
    {
        var result = ExecuteMainWithStreamingExecutor(
            static (value, _) => StreamPromptInChunks(value),
            "ask",
            "resposta",
            "em",
            "stream");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: tool call.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: diff.", result.StdOut);
        Assert.Contains("Prompt: resposta em stream", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_RetriesPromptExecutor_WhenTransientFailureOccursBeforeStreaming()
    {
        var attempts = 0;
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            (_, _, cancellationToken) => StreamPromptWithTransientFailure(cancellationToken),
            "ask",
            "retentativa");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(2, attempts);
        Assert.Contains("Resposta apos retry", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);

        return;

        async IAsyncEnumerable<string> StreamPromptWithTransientFailure(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            attempts++;

            if (attempts == 1)
            {
                throw new TimeoutException("timeout transitorio");
            }

            cancellationToken.ThrowIfCancellationRequested();
            yield return "Resposta apos retry";
            await Task.CompletedTask;
        }
    }

    [Fact]
    public void Main_AskCommand_WithDiffStream_WritesDiffStateWithDetectionMessage()
    {
        var result = ExecuteMainWithStreamingExecutor(
            static (_, _) => StreamPromptWithDiffInChunks(),
            "ask",
            "gerar",
            "diff");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Estado de execucao: diff. Diff identificado na resposta.", result.StdOut);
        Assert.Contains("```diff", result.StdOut);
        Assert.Contains("+nova linha", result.StdOut);
        Assert.Contains("-linha antiga", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithCodeFenceStream_KeepsFenceAndCodeContent()
    {
        var result = ExecuteMainWithStreamingExecutor(
            static (value, _) => StreamPromptWithCodeBlockInChunks(value),
            "ask",
            "gerar",
            "exemplo");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("Resposta para: gerar exemplo", result.StdOut);
        Assert.Contains("```csharp", result.StdOut);
        Assert.Contains("Console.WriteLine(\"ok\");", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithModelFlag_ReturnsSuccess_AndForwardsModelToExecutor()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "ask",
            "--model",
            "qwen2.5-coder:7b",
            "gerar",
            "teste");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: gerar teste", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithModelFlagUsingEqualsSyntax_ReturnsSuccess_AndForwardsModelToExecutor()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "ask",
            "--model=qwen2.5-coder:7b",
            "gerar",
            "teste");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: gerar teste", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_FallsBackToAvailableModel_WhenSelectedModelIsUnavailable()
    {
        var attemptedModels = new List<string?>();
        var result = ExecuteMainWithModelAwareStreamingExecutorAndModels(
            (prompt, model, _) => StreamPromptWithModelFallback(prompt, model),
            _ => Task.FromResult<IReadOnlyList<OllamaLocalModel>>(
            [
                new OllamaLocalModel("llama3.2:latest")
            ]),
            "ask",
            "--model",
            "qwen2.5-coder:7b",
            "gerar",
            "fallback");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Collection(
            attemptedModels,
            static candidate => Assert.Equal("qwen2.5-coder:7b", candidate),
            static candidate => Assert.Equal(OllamaModelDefaults.DefaultModel, candidate),
            static candidate => Assert.Equal("llama3.2:latest", candidate));
        Assert.Contains("Modelo: llama3.2:latest | Prompt: gerar fallback", result.StdOut);
        Assert.Contains(
            "Modelo 'qwen2.5-coder:7b' indisponivel. Tentando fallback para 'qwen3.5:4b'.",
            result.StdOut);
        Assert.Contains(
            "Modelo 'qwen3.5:4b' indisponivel. Tentando fallback para 'llama3.2:latest'.",
            result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);

        return;

        async IAsyncEnumerable<string> StreamPromptWithModelFallback(
            string prompt,
            string? model)
        {
            attemptedModels.Add(model);

            if (string.Equals(model, "qwen2.5-coder:7b", StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, OllamaModelDefaults.DefaultModel, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("model not found");
            }

            yield return $"Modelo: {model ?? "<padrao>"} | Prompt: {prompt}";
            await Task.CompletedTask;
        }
    }

    [Fact]
    public void Main_AskCommand_DoesNotFallbackModel_WhenFailureIsNotModelUnavailability()
    {
        var modelsExecutorCalls = 0;
        var result = ExecuteMainWithModelAwareStreamingExecutorAndModels(
            (_, _, _) => throw new InvalidOperationException("falha simulada"),
            _ =>
            {
                modelsExecutorCalls++;
                return Task.FromResult<IReadOnlyList<OllamaLocalModel>>(
                [
                    new OllamaLocalModel("llama3.2:latest")
                ]);
            },
            "ask",
            "--model",
            "qwen2.5-coder:7b",
            "gerar",
            "erro");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Equal(0, modelsExecutorCalls);
        Assert.Contains(
            "[ERROR] Estado de execucao: erro. Nao foi possivel executar o prompt: falha simulada",
            result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithDuplicateModelFlag_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain(
            "ask",
            "--model",
            "qwen2.5-coder:7b",
            "--model",
            "llama3.2:latest",
            "gerar",
            "teste");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A opcao '--model' foi informada mais de uma vez no comando 'ask'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun ask --model qwen3.5:4b \"seu prompt\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AskCommand_WithDoubleDashSeparator_KeepsRemainingArgumentsAsPrompt()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "ask",
            "--model",
            "qwen2.5-coder:7b",
            "--",
            "--model",
            "literal");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: --model literal", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WithModelFlagWithoutValue_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("ask", "--model");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A opcao '--model' exige um nome de modelo.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun ask --model qwen3.5:4b \"seu prompt\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AskCommand_WithoutPrompt_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("ask");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. Voce precisa informar um prompt para o comando 'ask'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun ask \"seu prompt\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AskCommand_WithEmptyPrompt_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("ask", "    ");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O prompt informado para o comando 'ask' esta vazio.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun ask \"seu prompt\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_Smoke_AskCommand_EndToEnd_ReturnsSuccess_AndWritesExecutionLifecycle()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "ask",
            "--model",
            "qwen3.5:4b",
            "gerar",
            "smoke",
            "test");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("Modelo: qwen3.5:4b | Prompt: gerar smoke test", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithObjective_ReturnsSuccess_AndWritesAutonomousPrompt()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "agent",
            "planejar",
            "migracao",
            "de",
            "dados");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Iniciando modo agente autonomo por objetivo.", result.StdOut);
        Assert.Contains("Modelo: <padrao> | Prompt: [MODO: AGENTE AUTONOMO]", result.StdOut);
        Assert.Contains("[OBJETIVO]", result.StdOut);
        Assert.Contains("[CONTEXTO DE ENGENHARIA DO PROJETO]", result.StdOut);
        Assert.Contains("codigo (", result.StdOut);
        Assert.Contains("testes (", result.StdOut);
        Assert.Contains("docs (", result.StdOut);
        Assert.Contains("historico git recente:", result.StdOut);
        Assert.Contains("[PLANO DE EXECUCAO POR ETAPAS]", result.StdOut);
        Assert.Contains("1. [Mapear contexto e restricoes]", result.StdOut);
        Assert.Contains("Acao: Planejar migracao de dados.", result.StdOut);
        Assert.Contains("planejar migracao de dados", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_InjectsEngineeringContextIntoFirstPlanPrompt()
    {
        var capturedPrompts = new List<string>();

        var result = ExecuteMainWithModelAwareStreamingExecutor(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);
                var response = prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                    ? "VERIFICATION_STATUS=done\nValidacao aprovada."
                    : "ok";
                return StreamSingleChunk(response);
            },
            "agent",
            "revisar",
            "arquitetura");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var firstPlanPrompt = Assert.Single(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Iteracao: 1", StringComparison.Ordinal)
                && prompt.Contains("Fase atual: plan", StringComparison.Ordinal));
        Assert.Contains("[CONTEXTO DE ENGENHARIA DO PROJETO]", firstPlanPrompt);
        Assert.Contains("codigo (", firstPlanPrompt);
        Assert.Contains("testes (", firstPlanPrompt);
        Assert.Contains("docs (", firstPlanPrompt);
        Assert.Contains("historico git recente:", firstPlanPrompt);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_ExecutePhasePrompt_RequiresDiffAndTechnicalJustificationPerChange()
    {
        var capturedPrompts = new List<string>();

        var result = ExecuteMainWithModelAwareStreamingExecutor(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);
                var response = prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                    ? "VERIFICATION_STATUS=done\nValidacao aprovada."
                    : "ok";
                return StreamSingleChunk(response);
            },
            "agent",
            "implementar",
            "ajuste",
            "em",
            "api");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var executePrompt = Assert.Single(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Iteracao: 1", StringComparison.Ordinal)
                && prompt.Contains("Fase atual: execute", StringComparison.Ordinal));
        Assert.Contains("CODE_CHANGE_STATUS=<changed|no-change>", executePrompt);
        Assert.Contains("CHANGE_FILE=<caminho-relativo>", executePrompt);
        Assert.Contains("```diff", executePrompt);
        Assert.Contains(
            "TECHNICAL_JUSTIFICATION=<justificativa tecnica curta da mudanca>",
            executePrompt);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_AfterChangedBlock_RunsBuildTestLintBeforeVerify()
    {
        var capturedPrompts = new List<string>();
        var validationScripts = new List<string>();
        var toolRuntime = new StubToolRuntime((request, _) =>
        {
            validationScripts.Add(request.Arguments["script"]);
            return ToolExecutionResult.Success("validacao ok", TimeSpan.FromMilliseconds(5));
        });

        var result = ExecuteMainWithModelAwareStreamingExecutorAndToolRuntime(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=ASXRunTerminal/Program.cs
                        CHANGE_KIND=edit
                        ```diff
                        @@ -1 +1 @@
                        -old
                        +new
                        ```
                        TECHNICAL_JUSTIFICATION=Ajusta fluxo do agente.
                        """);
                }

                if (prompt.Contains("Fase atual: verify", StringComparison.Ordinal))
                {
                    return StreamSingleChunk("VERIFICATION_STATUS=done\nValidacao aprovada.");
                }

                return StreamSingleChunk("ok");
            },
            toolRuntime,
            "agent",
            "implementar",
            "validacao",
            "automatica");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(3, validationScripts.Count);
        Assert.Contains("dotnet build", validationScripts[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet test", validationScripts[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet format", validationScripts[2], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Validacao automatica 'build' passou", result.StdOut);
        Assert.Contains("Validacao automatica 'test' passou", result.StdOut);
        Assert.Contains("Validacao automatica 'lint' passou", result.StdOut);

        var verifyPrompt = Assert.Single(
            capturedPrompts,
            static prompt => prompt.Contains("Fase atual: verify", StringComparison.Ordinal));
        Assert.Contains("[VALIDACAO AUTOMATICA POS-MUDANCA]", verifyPrompt);
        Assert.Contains("Status geral: passed.", verifyPrompt);
        Assert.Contains("- build: passed", verifyPrompt);
        Assert.Contains("- test: passed", verifyPrompt);
        Assert.Contains("- lint: passed", verifyPrompt);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WhenAutomaticValidationFails_ForcesRefine()
    {
        var capturedPrompts = new List<string>();
        var validationScripts = new List<string>();
        var toolRuntime = new StubToolRuntime((request, _) =>
        {
            var script = request.Arguments["script"];
            validationScripts.Add(script);

            if (script.Contains("dotnet test", StringComparison.OrdinalIgnoreCase))
            {
                return ToolExecutionResult.Failure(
                    error: "teste quebrado",
                    exitCode: 1,
                    duration: TimeSpan.FromMilliseconds(7),
                    stdOut: "falha em AgentTests");
            }

            return ToolExecutionResult.Success("validacao ok", TimeSpan.FromMilliseconds(5));
        });

        var result = ExecuteMainWithModelAwareStreamingExecutorAndToolRuntime(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal)
                    && prompt.Contains("Iteracao: 1", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=ASXRunTerminal/Program.cs
                        CHANGE_KIND=edit
                        ```diff
                        @@ -1 +1 @@
                        -old
                        +new
                        ```
                        TECHNICAL_JUSTIFICATION=Ajusta fluxo do agente.
                        """);
                }

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal)
                    && prompt.Contains("Iteracao: 2", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=no-change
                        Ajuste replanejado sem novo bloco de mudancas.
                        """);
                }

                if (prompt.Contains("Fase atual: verify", StringComparison.Ordinal))
                {
                    return StreamSingleChunk("VERIFICATION_STATUS=done\nValidacao aprovada.");
                }

                return StreamSingleChunk("ok");
            },
            toolRuntime,
            "agent",
            "--max-steps",
            "3",
            "corrigir",
            "falha",
            "de",
            "teste");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(3, validationScripts.Count);
        Assert.Contains(
            "Verificacao marcou status 'done', mas a validacao automatica pos-mudanca falhou. Forcando refine.",
            result.StdOut);
        Assert.Contains(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Fase atual: refine", StringComparison.Ordinal)
                && prompt.Contains("Iteracao: 1", StringComparison.Ordinal));

        var firstVerifyPrompt = Assert.Single(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                && prompt.Contains("Iteracao: 1", StringComparison.Ordinal));
        Assert.Contains("Status geral: failed.", firstVerifyPrompt);
        Assert.Contains("- test: failed", firstVerifyPrompt);
        Assert.Contains("stdout: falha em AgentTests", firstVerifyPrompt);
        Assert.Contains("stderr: teste quebrado", firstVerifyPrompt);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WhenValidationFails_AutoCorrectsAndRevalidatesBeforeVerify()
    {
        var capturedPrompts = new List<string>();
        var validationScripts = new List<string>();
        var testRunCount = 0;
        var toolRuntime = new StubToolRuntime((request, _) =>
        {
            var script = request.Arguments["script"];
            validationScripts.Add(script);

            if (script.Contains("dotnet test", StringComparison.OrdinalIgnoreCase))
            {
                testRunCount++;
                if (testRunCount == 1)
                {
                    return ToolExecutionResult.Failure(
                        error: "teste quebrado",
                        exitCode: 1,
                        duration: TimeSpan.FromMilliseconds(9),
                        stdOut: "falha em AgentTests");
                }
            }

            return ToolExecutionResult.Success("validacao ok", TimeSpan.FromMilliseconds(4));
        });

        var result = ExecuteMainWithModelAwareStreamingExecutorAndToolRuntime(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=ASXRunTerminal/core/AgentValidation.cs
                        CHANGE_KIND=edit
                        ```diff
                        @@ -1 +1 @@
                        -old
                        +new
                        ```
                        TECHNICAL_JUSTIFICATION=Adiciona validacao inicial.
                        """);
                }

                if (prompt.Contains("Fase atual: auto-correct", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=ASXRunTerminal.Tests/AgentValidationCommandCatalogTests.cs
                        CHANGE_KIND=test
                        ```diff
                        @@ -1 +1 @@
                        -broken
                        +fixed
                        ```
                        TECHNICAL_JUSTIFICATION=Corrige o teste que falhou na validacao automatica.
                        """);
                }

                if (prompt.Contains("Fase atual: verify", StringComparison.Ordinal))
                {
                    return StreamSingleChunk("VERIFICATION_STATUS=done\nValidacao aprovada.");
                }

                return StreamSingleChunk("ok");
            },
            toolRuntime,
            "agent",
            "corrigir",
            "teste",
            "falhando");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(6, validationScripts.Count);
        Assert.Equal(2, testRunCount);
        Assert.Contains(
            "Auto-correcao de validacao: tentativa 1/2 apos falha em build/test/lint.",
            result.StdOut);
        Assert.Contains(
            "Auto-correcao de validacao: validacao passou apos tentativa 1/2.",
            result.StdOut);
        Assert.DoesNotContain("Forcando refine", result.StdOut);

        var autoCorrectionPrompt = Assert.Single(
            capturedPrompts,
            static prompt => prompt.Contains("Fase atual: auto-correct", StringComparison.Ordinal));
        Assert.Contains("Tentativa: 1/2", autoCorrectionPrompt);
        Assert.Contains("falha em AgentTests", autoCorrectionPrompt);

        var verifyPrompt = Assert.Single(
            capturedPrompts,
            static prompt => prompt.Contains("Fase atual: verify", StringComparison.Ordinal));
        Assert.Contains("[AUTO-CORRECAO POS-VALIDACAO]", verifyPrompt);
        Assert.Contains("Status geral: passed.", verifyPrompt);
        Assert.Contains("- test: passed", verifyPrompt);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WhenAutoCorrectionKeepsFailing_StopsAtAttemptLimit()
    {
        var capturedPrompts = new List<string>();
        var validationScripts = new List<string>();
        var toolRuntime = new StubToolRuntime((request, _) =>
        {
            var script = request.Arguments["script"];
            validationScripts.Add(script);

            if (script.Contains("dotnet test", StringComparison.OrdinalIgnoreCase))
            {
                return ToolExecutionResult.Failure(
                    error: "teste ainda quebrado",
                    exitCode: 1,
                    duration: TimeSpan.FromMilliseconds(8),
                    stdOut: "falha persistente em AgentTests");
            }

            return ToolExecutionResult.Success("validacao ok", TimeSpan.FromMilliseconds(4));
        });

        var result = ExecuteMainWithModelAwareStreamingExecutorAndToolRuntime(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal)
                    || prompt.Contains("Fase atual: auto-correct", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=ASXRunTerminal/Program.cs
                        CHANGE_KIND=edit
                        ```diff
                        @@ -1 +1 @@
                        -old
                        +new
                        ```
                        TECHNICAL_JUSTIFICATION=Tenta corrigir a falha de teste.
                        """);
                }

                if (prompt.Contains("Fase atual: verify", StringComparison.Ordinal))
                {
                    return StreamSingleChunk("VERIFICATION_STATUS=done\nValidacao aprovada.");
                }

                return StreamSingleChunk("ok");
            },
            toolRuntime,
            "agent",
            "--max-steps",
            "1",
            "corrigir",
            "teste",
            "persistente");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Equal(9, validationScripts.Count);
        Assert.Equal(
            2,
            capturedPrompts.Count(
                static prompt => prompt.Contains("Fase atual: auto-correct", StringComparison.Ordinal)));
        Assert.Contains(
            "Auto-correcao de validacao: limite de 2 tentativa(s) atingido; mantendo falha para verify/refine.",
            result.StdOut);
        Assert.Contains(
            "Verificacao marcou status 'done', mas a validacao automatica pos-mudanca falhou. Forcando refine.",
            result.StdOut);
        Assert.Contains(
            "[ERROR] Nao foi possivel concluir a execucao. O modo agente nao conseguiu concluir o objetivo com seguranca.",
            result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithCompositeObjective_WritesDecomposedExecutionPlan()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "agent",
            "planejar",
            "e",
            "executar",
            "migracao",
            "incremental");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[PLANO DE EXECUCAO POR ETAPAS]", result.StdOut);
        Assert.Contains("Acao: Planejar.", result.StdOut);
        Assert.Contains("Acao: Executar migracao incremental.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithModelFlag_ReturnsSuccess_AndForwardsModelToExecutor()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "agent",
            "--model=qwen2.5-coder:7b",
            "criar",
            "plano");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Iniciando modo agente autonomo por objetivo.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: [MODO: AGENTE AUTONOMO]", result.StdOut);
        Assert.Contains("criar plano", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithModelFlagSeparated_ReturnsSuccess_AndForwardsModelToExecutor()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "agent",
            "--model",
            "qwen2.5-coder:7b",
            "criar",
            "plano");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Iniciando modo agente autonomo por objetivo.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: [MODO: AGENTE AUTONOMO]", result.StdOut);
        Assert.Contains("criar plano", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithDuplicateModelFlag_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain(
            "agent",
            "--model",
            "qwen2.5-coder:7b",
            "--model",
            "llama3.2:latest",
            "gerar",
            "plano");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A opcao '--model' foi informada mais de uma vez no comando 'agent'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun agent --model qwen3.5:4b \"seu objetivo\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AgentCommand_WithoutObjective_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("agent");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. Voce precisa informar um objetivo para o comando 'agent'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun agent \"seu objetivo\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AgentCommand_WithEmptyObjective_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("agent", "   ");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O objetivo informado para o comando 'agent' esta vazio.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun agent \"seu objetivo\".", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AgentCommand_WhenVerifyRequestsRefine_ExecutesRefineAndConcludesNextIteration()
    {
        var capturedPrompts = new List<string>();

        var result = ExecuteMainWithModelAwareStreamingExecutor(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);

                var response = prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                    && prompt.Contains("Iteracao: 1", StringComparison.Ordinal)
                    ? "VERIFICATION_STATUS=refine\nLacuna: faltam validacoes."
                    : prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                        ? "VERIFICATION_STATUS=done\nValidacao aprovada."
                        : "ok";
                return StreamSingleChunk(response);
            },
            "agent",
            "corrigir",
            "pipeline");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(
            capturedPrompts,
            static prompt => prompt.Contains("Fase atual: refine", StringComparison.Ordinal));
        Assert.Contains(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Iteracao: 2", StringComparison.Ordinal)
                && prompt.Contains("Fase atual: plan", StringComparison.Ordinal));
        Assert.Contains(
            "Verificacao marcou status 'refine'. Iniciando fase refine.",
            result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WhenVerifyReturnsDoneButExecuteEvidenceIsIncomplete_ForcesRefine()
    {
        var capturedPrompts = new List<string>();
        var toolRuntime = new StubToolRuntime(static (_, _) =>
            ToolExecutionResult.Success("validacao ok", TimeSpan.FromMilliseconds(1)));

        var result = ExecuteMainWithModelAwareStreamingExecutorAndToolRuntime(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal)
                    && prompt.Contains("Iteracao: 1", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=src/Program.cs
                        CHANGE_KIND=edit
                        """);
                }

                if (prompt.Contains("Fase atual: execute", StringComparison.Ordinal)
                    && prompt.Contains("Iteracao: 2", StringComparison.Ordinal))
                {
                    return StreamSingleChunk(
                        """
                        CODE_CHANGE_STATUS=changed
                        CHANGE_FILE=src/Program.cs
                        CHANGE_KIND=edit
                        ```diff
                        @@ -1 +1 @@
                        -old
                        +new
                        ```
                        TECHNICAL_JUSTIFICATION=Atualiza validacao de parametros.
                        """);
                }

                if (prompt.Contains("Fase atual: verify", StringComparison.Ordinal))
                {
                    return StreamSingleChunk("VERIFICATION_STATUS=done\nValidacao aprovada.");
                }

                return StreamSingleChunk("ok");
            },
            toolRuntime,
            "agent",
            "--max-steps",
            "3",
            "corrigir",
            "validador");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(
            "Verificacao marcou status 'done', mas as evidencias de diff/justificativa por mudanca estao incompletas. Forcando refine.",
            result.StdOut);
        Assert.Contains(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Fase atual: refine", StringComparison.Ordinal)
                && prompt.Contains("Iteracao: 1", StringComparison.Ordinal));
        Assert.Contains(
            capturedPrompts,
            static prompt =>
                prompt.Contains("Fase atual: plan", StringComparison.Ordinal)
                && prompt.Contains("Iteracao: 2", StringComparison.Ordinal));
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WhenVerifyNeverConcludes_ReturnsRuntimeErrorAfterInternalLimit()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, _, _) =>
            {
                var response = prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                    ? "VERIFICATION_STATUS=refine\nAinda pendente."
                    : "ok";
                return StreamSingleChunk(response);
            },
            "agent",
            "mapear",
            "riscos");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains(
            "Loop autonomo interrompido apos 8 iteracao(oes) sem sinal explicito de conclusao na fase verify.",
            result.StdErr);
        Assert.Contains(
            "[ERROR] Nao foi possivel concluir a execucao. O modo agente nao conseguiu concluir o objetivo com seguranca.",
            result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithConfiguredMaxStepsLimit_StopsAtConfiguredLimit()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, _, _) =>
            {
                var response = prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                    ? "VERIFICATION_STATUS=refine\nAinda pendente."
                    : "ok";
                return StreamSingleChunk(response);
            },
            "agent",
            "--max-steps",
            "2",
            "mapear",
            "riscos");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains(
            "Loop autonomo interrompido apos 2 iteracao(oes) sem sinal explicito de conclusao na fase verify.",
            result.StdErr);
        Assert.Contains(
            "[ERROR] Nao foi possivel concluir a execucao. O modo agente nao conseguiu concluir o objetivo com seguranca.",
            result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WithInvalidMaxStepsValue_ReturnsInvalidArguments()
    {
        var result = ExecuteMain(
            "agent",
            "--max-steps",
            "0",
            "planejar",
            "migracao");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(
            "[ERROR] Nao foi possivel executar o comando. A opcao '--max-steps' exige um numero inteiro positivo.",
            result.StdErr);
        Assert.Contains(
            "[ERROR] Sugestao: Exemplo: asxrun agent --max-steps 6 \"seu objetivo\".",
            result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AgentCommand_WhenMaxTimeBudgetIsExceeded_ReturnsRuntimeError()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (_, _, cancellationToken) => StreamSingleChunkAfterDelay(
                "ok",
                delayMilliseconds: 120,
                cancellationToken),
            "agent",
            "--max-time",
            "0.05",
            "planejar",
            "migracao");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains(
            "Loop autonomo interrompido por limite de tempo da sessao",
            result.StdErr);
        Assert.Contains(
            "[ERROR] Nao foi possivel concluir a execucao. O modo agente atingiu o limite de tempo da sessao.",
            result.StdErr);
    }

    [Fact]
    public void Main_AgentCommand_WhenMaxCostBudgetIsExceeded_ReturnsRuntimeError()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (_, _, _) => StreamSingleChunk(new string('x', 800)),
            "agent",
            "--max-cost",
            "120",
            "planejar",
            "migracao");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains(
            "Loop autonomo interrompido por limite de custo da sessao",
            result.StdErr);
        Assert.Contains(
            "[ERROR] Nao foi possivel concluir a execucao. O modo agente atingiu o limite de custo da sessao.",
            result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInputs_ReturnsSuccess_AndProcessesPrompts()
    {
        var result = ExecuteMainWithInput(
            "gerar classe service\n   \nreview de codigo\nexit\n",
            static value => $"Prompt: {value}",
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Modo interativo iniciado. Digite 'exit' para sair.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("Prompt: gerar classe service", result.StdOut);
        Assert.Contains("Prompt: review de codigo", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithModelFlag_ReturnsSuccess_AndForwardsModelToExecutor()
    {
        var result = ExecuteMainWithInputAndModelAwareStreamingExecutor(
            "gerar classe service\nexit\n",
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "chat",
            "--model=qwen2.5-coder:7b");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Modo interativo iniciado. Digite 'exit' para sair.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: gerar classe service", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInteractiveHelpCommand_ReturnsSuccess_AndWritesInteractiveHelp()
    {
        var result = ExecuteMainWithInput(
            "/help\n/exit\n",
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Comandos interativos do chat:", result.StdOut);
        Assert.Contains("- /help", result.StdOut);
        Assert.Contains("- /clear", result.StdOut);
        Assert.Contains("- /models", result.StdOut);
        Assert.Contains("- /tools", result.StdOut);
        Assert.Contains("- /exit", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInteractiveClearCommand_ReturnsSuccess_AndContinuesProcessingPrompts()
    {
        var result = ExecuteMainWithInput(
            "/clear\ngerar classe service\n/exit\n",
            static value => $"Prompt: {value}",
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Comando /clear executado.", result.StdOut);
        Assert.Contains("Prompt: gerar classe service", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInteractiveModelsCommand_ReturnsSuccess_AndListsModels()
    {
        var models = new[]
        {
            new OllamaLocalModel("qwen3.5:4b"),
            new OllamaLocalModel("llama3.2:latest")
        };

        var result = ExecuteMainWithInputAndModels(
            "/models\n/exit\n",
            _ => Task.FromResult<IReadOnlyList<OllamaLocalModel>>(models),
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Listando modelos locais do Ollama.", result.StdOut);
        Assert.Contains("- llama3.2:latest", result.StdOut);
        Assert.Contains("- qwen3.5:4b", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInteractiveToolsCommand_ReturnsSuccess_AndWritesToolsSummary()
    {
        var result = ExecuteMainWithInput(
            "/tools\n/exit\n",
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Ferramentas registradas no runtime local:", result.StdOut);
        Assert.Contains("- echo:", result.StdOut);
        Assert.Contains("- shell:", result.StdOut);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Contains("- powershell:", result.StdOut);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Contains("- bash:", result.StdOut);
            Assert.Contains("- zsh:", result.StdOut);
        }

        Assert.Contains("Skills built-in", result.StdOut);
        Assert.Contains("/help, /clear, /models, /tools, /exit", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInteractiveExitCommand_ReturnsSuccess_WithoutExecutingPrompt()
    {
        var promptExecutionCount = 0;
        var result = ExecuteMainWithInput(
            "/exit\n",
            _ =>
            {
                promptExecutionCount++;
                return "Prompt: nao deveria executar";
            },
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(0, promptExecutionCount);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_Smoke_ChatCommand_EndToEnd_ReturnsSuccess_AndProcessesConversationUntilExit()
    {
        var result = ExecuteMainWithInputAndModelAwareStreamingExecutor(
            "planejar smoke test\najustar asserts\nexit\n",
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "chat",
            "--model=qwen3.5:4b");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Modo interativo iniciado. Digite 'exit' para sair.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("Modelo: qwen3.5:4b | Prompt: planejar smoke test", result.StdOut);
        Assert.Contains("Modelo: qwen3.5:4b | Prompt: ajustar asserts", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithExtraArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("chat", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'chat' nao aceita argumentos adicionais.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun chat.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ChatCommand_WithDuplicateModelFlag_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain(
            "chat",
            "--model",
            "qwen2.5-coder:7b",
            "--model=llama3.2:latest");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A opcao '--model' foi informada mais de uma vez no comando 'chat'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun chat --model qwen3.5:4b.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_DoctorCommand_WhenOllamaIsHealthy_ReturnsSuccess_AndWritesDiagnostic()
    {
        var result = ExecuteMainWithHealthcheck(
            _ => Task.FromResult(OllamaHealthcheckResult.Healthy("0.6.5")),
            "doctor");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando diagnostico de conectividade com Ollama.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Ollama disponivel. Versao: 0.6.5.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_DoctorCommand_WhenOllamaIsUnavailable_ReturnsRuntimeError_AndWritesError()
    {
        var result = ExecuteMainWithHealthcheck(
            _ => Task.FromResult(OllamaHealthcheckResult.Unhealthy("Conexao recusada.")),
            "doctor");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains("[INFO] Executando diagnostico de conectividade com Ollama.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[ERROR] Estado de execucao: erro. Ollama indisponivel. Conexao recusada.", result.StdErr);
        Assert.Contains("[ERROR] Nao foi possivel concluir a execucao. Nao foi possivel validar a disponibilidade do Ollama.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Verifique se o servico Ollama esta em execucao e tente novamente.", result.StdErr);
    }

    [Fact]
    public void Main_DoctorCommand_RetriesHealthcheck_WhenFirstAttemptReturnsUnhealthy()
    {
        var attempts = 0;
        var result = ExecuteMainWithHealthcheck(
            _ =>
            {
                attempts++;
                return Task.FromResult(
                    attempts == 1
                        ? OllamaHealthcheckResult.Unhealthy("Conexao recusada.")
                        : OllamaHealthcheckResult.Healthy("0.6.5"));
            },
            "doctor");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(2, attempts);
        Assert.Contains("[INFO] Estado de execucao: concluido. Ollama disponivel. Versao: 0.6.5.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_DoctorCommand_WithExtraArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("doctor", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'doctor' nao aceita argumentos adicionais.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun doctor.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ModelsCommand_WhenLocalModelsExist_ReturnsSuccess_AndWritesModelNames()
    {
        var models = new[]
        {
            new OllamaLocalModel("qwen2.5-coder:7b"),
            new OllamaLocalModel("llama3.2:latest")
        };

        var result = ExecuteMainWithModels(_ => Task.FromResult<IReadOnlyList<OllamaLocalModel>>(models), "models");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Listando modelos locais do Ollama.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. 2 modelo(s) local(is) encontrado(s).", result.StdOut);
        Assert.Contains("- llama3.2:latest", result.StdOut);
        Assert.Contains("- qwen2.5-coder:7b", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ModelsCommand_WhenNoLocalModelExists_ReturnsSuccess_AndWritesEmptyMessage()
    {
        var result = ExecuteMainWithModels(
            _ => Task.FromResult<IReadOnlyList<OllamaLocalModel>>(Array.Empty<OllamaLocalModel>()),
            "models");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Listando modelos locais do Ollama.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Nenhum modelo local encontrado.", result.StdOut);
        Assert.DoesNotContain("- ", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ModelsCommand_WithExtraArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("models", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'models' nao aceita argumentos adicionais.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun models.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ModelsCommand_RetriesExecutor_WhenTransientFailureOccurs()
    {
        var attempts = 0;
        var models = new[]
        {
            new OllamaLocalModel("qwen3.5:4b")
        };

        var result = ExecuteMainWithModels(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new TimeoutException("timeout transitorio");
                }

                return Task.FromResult<IReadOnlyList<OllamaLocalModel>>(models);
            },
            "models");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(2, attempts);
        Assert.Contains("- qwen3.5:4b", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WithInteractiveModelsCommand_OpensCircuitBreaker_AfterConsecutiveTransientFailures()
    {
        var attempts = 0;
        var result = ExecuteMainWithInputAndModels(
            "/models\n/models\n/exit\n",
            _ =>
            {
                attempts++;
                throw new TimeoutException("timeout transitorio");
            },
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(3, attempts);
        Assert.Contains(
            "Circuit breaker aberto para 'Ollama/models'.",
            result.StdErr,
            StringComparison.Ordinal);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
    }

    [Fact]
    public void Main_ContextCommand_ReturnsSuccess_AndWritesWorkspaceSummary()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(nestedDirectory);
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            File.WriteAllText(Path.Combine(workspaceRootDirectory, "README.md"), "# workspace");
            File.WriteAllText(Path.Combine(nestedDirectory, "Program.cs"), "internal static class Program { }");
            WorkspaceContextFileIndexCatalog.ClearCache();
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMain("context");

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Contains("[INFO] Inspecionando contexto do workspace atual.", result.StdOut);
            Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
            Assert.Contains("[INFO] Estado de execucao: concluido. Resumo do workspace atual gerado.", result.StdOut);
            Assert.Contains($"- raiz-workspace: {Path.GetFullPath(workspaceRootDirectory)}", result.StdOut);
            Assert.Contains("- tipo-raiz: git", result.StdOut);
            Assert.Contains("- entradas-indexadas:", result.StdOut);
            Assert.Contains("- diretorios-mapeados:", result.StdOut);
            Assert.Contains("- arquivos-mapeados:", result.StdOut);
            Assert.Contains("- limite-aplicado:", result.StdOut);
            Assert.Contains("- truncado: ", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            WorkspaceContextFileIndexCatalog.ClearCache();
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_ContextCommand_WithExtraArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("context", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'context' nao aceita argumentos adicionais.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun context.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_PatchCommand_WithDryRun_ReturnsSuccess_AndDoesNotPersistChanges()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(targetFilePath, "linha 1\nlinha 2");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("edit", "src/Program.cs", "linha 1\nlinha 2 atualizada", null)));
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMain("patch", "--dry-run", patchFilePath);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Equal("linha 1\nlinha 2", File.ReadAllText(targetFilePath));
            Assert.Contains("[INFO] Aplicando patch de workspace.", result.StdOut);
            Assert.Contains("[INFO] Estado de execucao: diff. Modo --dry-run habilitado. Diff gerado sem alterar arquivos.", result.StdOut);
            Assert.Contains("--- a/src/Program.cs", result.StdOut);
            Assert.Contains("+++ b/src/Program.cs", result.StdOut);
            Assert.Contains("+linha 2 atualizada", result.StdOut);
            Assert.Contains("- modo-dry-run: sim", result.StdOut);
            Assert.Contains("- alteracoes-aplicadas: 0", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WithoutDryRun_ReturnsSuccess_AndPersistsChanges()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(targetFilePath, "linha 1\nlinha 2");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("edit", "src/Program.cs", "linha 1\nlinha 2 atualizada", null)));
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMain("patch", patchFilePath);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Equal("linha 1\nlinha 2 atualizada", File.ReadAllText(targetFilePath));
            Assert.Contains("[INFO] Estado de execucao: diff. Diff gerado para alteracoes aplicadas no workspace.", result.StdOut);
            Assert.Contains("[INFO] Estado de execucao: concluido. 1 alteracao(oes) aplicada(s) no workspace.", result.StdOut);
            Assert.Contains("- modo-dry-run: nao", result.StdOut);
            Assert.Contains("- alteracoes-aplicadas: 1", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WhenWorkspacePolicyDeniesEdit_ReturnsRuntimeError_AndKeepsFile()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var policyDirectoryPath = Path.Combine(workspaceRootDirectory, UserConfigFile.ConfigDirectoryName);
        var policyFilePath = Path.Combine(
            policyDirectoryPath,
            WorkspacePermissionPolicyFile.WorkspacePermissionPolicyFileName);
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            Directory.CreateDirectory(policyDirectoryPath);
            File.WriteAllText(targetFilePath, "linha 1\nlinha 2");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("edit", "src/Program.cs", "linha 1\nlinha 2 atualizada", null)));
            File.WriteAllText(
                policyFilePath,
                """
                {
                  "defaultMode": "deny",
                  "edit": {
                    "allow": ["tests/**"]
                  }
                }
                """);
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMain("patch", patchFilePath);

            Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
            Assert.Equal("linha 1\nlinha 2", File.ReadAllText(targetFilePath));
            Assert.Contains("[INFO] Aplicando patch de workspace.", result.StdOut);
            Assert.Contains("Nao foi possivel aplicar o patch informado.", result.StdErr);
            Assert.Contains("nao e permitida", result.StdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WithDryRun_WhenWorkspacePolicyDeniesEdit_ReturnsRuntimeError_AndKeepsFile()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var policyDirectoryPath = Path.Combine(workspaceRootDirectory, UserConfigFile.ConfigDirectoryName);
        var policyFilePath = Path.Combine(
            policyDirectoryPath,
            WorkspacePermissionPolicyFile.WorkspacePermissionPolicyFileName);
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            Directory.CreateDirectory(policyDirectoryPath);
            File.WriteAllText(targetFilePath, "linha 1\nlinha 2");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("edit", "src/Program.cs", "linha 1\nlinha 2 atualizada", null)));
            File.WriteAllText(
                policyFilePath,
                """
                {
                  "defaultMode": "deny",
                  "edit": {
                    "allow": ["tests/**"]
                  }
                }
                """);
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMain("patch", "--dry-run", patchFilePath);

            Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
            Assert.Equal("linha 1\nlinha 2", File.ReadAllText(targetFilePath));
            Assert.Contains("Nao foi possivel aplicar o patch informado.", result.StdErr);
            Assert.Contains("nao e permitida", result.StdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WithoutDryRun_AppendsAuditEntryWithSessionMetadata()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var expectedAuditPath = Path.Combine(temporaryDirectory, "patch-audit");
        var originalDirectory = Directory.GetCurrentDirectory();
        WorkspacePatchAuditEntry? capturedAuditEntry = null;
        var appendCount = 0;

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(targetFilePath, "linha 1\nlinha 2");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("edit", "src/Program.cs", "linha 1\nlinha 2 atualizada", null)));
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMainWithWorkspacePatchAudit(
                entry =>
                {
                    appendCount++;
                    capturedAuditEntry = entry;
                    return expectedAuditPath;
                },
                "patch",
                patchFilePath);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Equal(1, appendCount);
            Assert.True(capturedAuditEntry.HasValue);

            var auditEntry = capturedAuditEntry.Value;
            Assert.Equal("patch", auditEntry.Command);
            Assert.False(auditEntry.IsPreviewOnly);
            Assert.True(auditEntry.SessionSequence > 0);
            Assert.False(string.IsNullOrWhiteSpace(auditEntry.SessionId));
            Assert.Equal(Path.GetFullPath(workspaceRootDirectory), auditEntry.WorkspaceRootDirectory);
            Assert.Equal(Path.GetFullPath(patchFilePath), auditEntry.PatchRequestFilePath);
            Assert.Equal(1, auditEntry.PlannedChangeCount);
            Assert.Equal(1, auditEntry.AppliedChangeCount);
            Assert.Equal(0, auditEntry.SkippedChangeCount);
            Assert.Single(auditEntry.Files);
            Assert.Contains("- sessao-auditoria:", result.StdOut);
            Assert.Contains("- sequencia-sessao:", result.StdOut);
            Assert.Contains($"- arquivo-auditoria: {expectedAuditPath}", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WhenAuditAppenderFails_ReturnsSuccessAndKeepsPatchResult()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(targetFilePath, "linha 1\nlinha 2");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("edit", "src/Program.cs", "linha 1\nlinha 2 atualizada", null)));
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMainWithWorkspacePatchAudit(
                static _ => throw new IOException("falha ao persistir auditoria"),
                "patch",
                patchFilePath);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Equal("linha 1\nlinha 2 atualizada", File.ReadAllText(targetFilePath));
            Assert.Contains(
                "[ERROR] Nao foi possivel registrar a trilha de auditoria local do patch. falha ao persistir auditoria",
                result.StdErr);
            Assert.DoesNotContain("- arquivo-auditoria:", result.StdOut);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WithUnknownOption_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("patch", "--foo", "patch.json");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A opcao '--foo' nao e reconhecida no comando 'patch'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun patch --dry-run patch.json.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_PatchCommand_WithDeleteChangeAndRejectedConfirmation_ReturnsCancelled_AndKeepsFile()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(targetFilePath, "linha para excluir");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("delete", "src/Program.cs", null, null)));
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMainWithInput("nao\n", "patch", patchFilePath);

            Assert.Equal((int)CliExitCode.Cancelled, result.ExitCode);
            Assert.True(File.Exists(targetFilePath));
            Assert.Contains("ATENCAO: O patch contem operacoes destrutivas.", result.StdOut);
            Assert.Contains("- delete: src/Program.cs", result.StdOut);
            Assert.Contains("Confirme com 'sim' para aplicar as alteracoes destrutivas [sim/nao]: ", result.StdOut);
            Assert.Contains(
                "[ERROR] Estado de execucao: erro. Patch cancelado pelo usuario para evitar operacoes destrutivas.",
                result.StdErr);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_PatchCommand_WithDeleteChangeAndApprovedConfirmation_ReturnsSuccess_AndDeletesFile()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var workspaceRootDirectory = Path.Combine(temporaryDirectory, "workspace-root");
        var nestedDirectory = Path.Combine(workspaceRootDirectory, "src", "app");
        var targetFilePath = Path.Combine(workspaceRootDirectory, "src", "Program.cs");
        var patchFilePath = Path.Combine(workspaceRootDirectory, "patch.json");
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRootDirectory, ".git"));
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(targetFilePath, "linha para excluir");
            File.WriteAllText(
                patchFilePath,
                BuildPatchRequestFileContent(
                    ("delete", "src/Program.cs", null, null)));
            Directory.SetCurrentDirectory(nestedDirectory);

            var result = ExecuteMainWithInput("sim\n", "patch", patchFilePath);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.False(File.Exists(targetFilePath));
            Assert.Contains("ATENCAO: O patch contem operacoes destrutivas.", result.StdOut);
            Assert.Contains("- delete: src/Program.cs", result.StdOut);
            Assert.Contains("Confirme com 'sim' para aplicar as alteracoes destrutivas [sim/nao]: ", result.StdOut);
            Assert.Contains("--- a/src/Program.cs", result.StdOut);
            Assert.Contains("+++ /dev/null", result.StdOut);
            Assert.Contains("- alteracoes-aplicadas: 1", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_HistoryCommand_WhenEntriesExist_ReturnsSuccess_AndWritesEntries()
    {
        var historyEntries = new[]
        {
            new PromptHistoryEntry(
                TimestampUtc: new DateTimeOffset(2026, 3, 27, 12, 30, 0, TimeSpan.Zero),
                Command: "ask",
                Prompt: "gerar testes de unidade",
                Response: "Aqui estao os testes.",
                Model: "qwen3.5:4b"),
            new PromptHistoryEntry(
                TimestampUtc: new DateTimeOffset(2026, 3, 28, 8, 15, 0, TimeSpan.Zero),
                Command: "skill",
                Prompt: "revisar controller",
                Response: "Encontrei dois riscos.",
                Model: null)
        };

        var result = ExecuteMainWithHistory(() => historyEntries, "history");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Lendo historico local de prompts.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. 2 item(ns) de historico encontrado(s).", result.StdOut);
        Assert.Contains("comando=skill modelo=<padrao>", result.StdOut);
        Assert.Contains("comando=ask modelo=qwen3.5:4b", result.StdOut);
        Assert.Contains("prompt: revisar controller", result.StdOut);
        Assert.Contains("resposta: Encontrei dois riscos.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_HistoryCommand_WhenNoEntriesExist_ReturnsSuccess_AndWritesEmptyMessage()
    {
        var result = ExecuteMainWithHistory(
            static () => Array.Empty<PromptHistoryEntry>(),
            "history");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Lendo historico local de prompts.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Nenhum item de historico encontrado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_HistoryCommand_WithClearFlag_ReturnsSuccess_AndClearsHistory()
    {
        var historyWasRead = false;
        var historyWasCleared = false;
        var result = ExecuteMainWithHistory(
            () =>
            {
                historyWasRead = true;
                return Array.Empty<PromptHistoryEntry>();
            },
            () => historyWasCleared = true,
            "history",
            "--clear");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Limpando historico local de prompts.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Historico local limpo com sucesso.", result.StdOut);
        Assert.False(historyWasRead);
        Assert.True(historyWasCleared);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_HistoryCommand_WithUnsupportedArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("history", "--clear", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'history' aceita apenas a opcao '--clear'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplos: asxrun history | asxrun history --clear.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_McpListCommand_WhenNoServersConfigured_ReturnsSuccess_AndWritesEmptyMessage()
    {
        var result = ExecuteMainWithMcp(
            static () => Array.Empty<McpServerDefinition>(),
            _ => { },
            static (_, _) => Task.FromResult(McpServerTestResult.Success("ok")),
            "mcp",
            "list");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Listando servidores MCP configurados.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Nenhum servidor MCP configurado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_McpAddCommand_WithStdioConfiguration_ReturnsSuccess_AndPersistsServer()
    {
        IReadOnlyList<McpServerDefinition>? persistedServers = null;
        var result = ExecuteMainWithMcp(
            static () => Array.Empty<McpServerDefinition>(),
            servers => persistedServers = servers.ToArray(),
            static (_, _) => Task.FromResult(McpServerTestResult.Success("ok")),
            "mcp",
            "add",
            "filesystem",
            "--command",
            "node",
            "--arg",
            "server.js",
            "--cwd",
            ".",
            "--env",
            "NODE_ENV=production");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Adicionando servidor MCP 'filesystem'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Servidor MCP 'filesystem' adicionado com sucesso.", result.StdOut);
        Assert.NotNull(persistedServers);
        var persisted = Assert.Single(persistedServers!);
        Assert.Equal("filesystem", persisted.Name);
        Assert.NotNull(persisted.ProcessOptions);
        Assert.Null(persisted.RemoteOptions);
        Assert.Equal("node", persisted.ProcessOptions!.Command);
        Assert.Equal("server.js", Assert.Single(persisted.ProcessOptions.Arguments));
        Assert.Equal(".", persisted.ProcessOptions.WorkingDirectory);
        Assert.Equal("production", persisted.ProcessOptions.EnvironmentVariables["NODE_ENV"]);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_McpAddCommand_WhenNameAlreadyExists_ReturnsInvalidArguments_AndDoesNotPersist()
    {
        var saveWasCalled = false;
        var existingServers = new[]
        {
            McpServerDefinition.Stdio("filesystem", new McpServerProcessOptions("node"))
        };
        var result = ExecuteMainWithMcp(
            () => existingServers,
            _ => saveWasCalled = true,
            static (_, _) => Task.FromResult(McpServerTestResult.Success("ok")),
            "mcp",
            "add",
            "filesystem",
            "--command",
            "node");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[INFO] Adicionando servidor MCP 'filesystem'.", result.StdOut);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. Ja existe um servidor MCP com o nome 'filesystem'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun mcp remove filesystem' antes de adicionar novamente.", result.StdErr);
        Assert.False(saveWasCalled);
    }

    [Fact]
    public void Main_McpRemoveCommand_WhenServerExists_ReturnsSuccess_AndPersistsRemoval()
    {
        IReadOnlyList<McpServerDefinition>? persistedServers = null;
        var existingServers = new[]
        {
            McpServerDefinition.Stdio("filesystem", new McpServerProcessOptions("node")),
            McpServerDefinition.Remote("github", new McpServerRemoteOptions(new Uri("https://mcp.example.com/rpc")))
        };
        var result = ExecuteMainWithMcp(
            () => existingServers,
            servers => persistedServers = servers.ToArray(),
            static (_, _) => Task.FromResult(McpServerTestResult.Success("ok")),
            "mcp",
            "remove",
            "github");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Removendo servidor MCP 'github'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Servidor MCP 'github' removido com sucesso.", result.StdOut);
        Assert.NotNull(persistedServers);
        var persisted = Assert.Single(persistedServers!);
        Assert.Equal("filesystem", persisted.Name);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_McpRemoveCommand_WhenServerDoesNotExist_ReturnsInvalidArguments_AndWritesError()
    {
        var existingServers = new[]
        {
            McpServerDefinition.Stdio("filesystem", new McpServerProcessOptions("node"))
        };
        var result = ExecuteMainWithMcp(
            () => existingServers,
            _ => { },
            static (_, _) => Task.FromResult(McpServerTestResult.Success("ok")),
            "mcp",
            "remove",
            "github");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[INFO] Removendo servidor MCP 'github'.", result.StdOut);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O servidor MCP 'github' nao foi encontrado.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun mcp list' para listar servidores configurados.", result.StdErr);
    }

    [Fact]
    public void Main_McpTestCommand_WhenServerExistsAndTestSucceeds_ReturnsSuccess_AndWritesStates()
    {
        var existingServers = new[]
        {
            McpServerDefinition.Stdio("filesystem", new McpServerProcessOptions("node"))
        };
        string? testedServerName = null;
        var result = ExecuteMainWithMcp(
            () => existingServers,
            _ => { },
            (server, _) =>
            {
                testedServerName = server.Name;
                return Task.FromResult(McpServerTestResult.Success("Servidor MCP testado com sucesso."));
            },
            "mcp",
            "test",
            "filesystem");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal("filesystem", testedServerName);
        Assert.Contains("[INFO] Testando servidor MCP 'filesystem'.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Servidor MCP testado com sucesso.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_McpTestCommand_RetriesTester_WhenTransientFailureOccurs()
    {
        var attempts = 0;
        var existingServers = new[]
        {
            McpServerDefinition.Stdio("filesystem", new McpServerProcessOptions("node"))
        };

        var result = ExecuteMainWithMcp(
            () => existingServers,
            _ => { },
            (server, _) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new TimeoutException($"timeout no servidor {server.Name}");
                }

                return Task.FromResult(McpServerTestResult.Success("Servidor MCP testado com sucesso."));
            },
            "mcp",
            "test",
            "filesystem");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(2, attempts);
        Assert.Contains("[INFO] Estado de execucao: concluido. Servidor MCP testado com sucesso.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_McpTestCommand_WhenServerDoesNotExist_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMainWithMcp(
            static () => Array.Empty<McpServerDefinition>(),
            _ => { },
            static (_, _) => Task.FromResult(McpServerTestResult.Success("ok")),
            "mcp",
            "test",
            "nao-existe");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[INFO] Testando servidor MCP 'nao-existe'.", result.StdOut);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O servidor MCP 'nao-existe' nao foi encontrado.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun mcp list' para listar servidores configurados.", result.StdErr);
    }

    [Fact]
    public void Main_McpCommand_WithoutAction_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("mcp");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'mcp' exige uma acao: 'list', 'add', 'remove' ou 'test'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplos: asxrun mcp list | asxrun mcp add meu-servidor --command node --arg server.js.", result.StdErr);
    }

    [Fact]
    public void Main_ConfigGetCommand_WithSupportedKey_ReturnsSuccess_AndWritesConfiguredValue()
    {
        var configured = new UserRuntimeConfig(
            OllamaHost: new Uri("http://localhost:11434/", UriKind.Absolute),
            DefaultModel: "phi4-mini",
            PromptTimeout: TimeSpan.FromSeconds(31),
            HealthcheckTimeout: TimeSpan.FromSeconds(4),
            ModelsTimeout: TimeSpan.FromSeconds(6),
            Theme: TerminalThemeMode.Auto);
        var saveWasCalled = false;
        var result = ExecuteMainWithConfig(
            () => configured,
            _ => saveWasCalled = true,
            "config",
            "get",
            UserConfigFile.DefaultModelKey);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Lendo configuracao 'default_model'.", result.StdOut);
        Assert.Contains("default_model=phi4-mini", result.StdOut);
        Assert.False(saveWasCalled);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ConfigSetCommand_WithSupportedKey_ReturnsSuccess_AndPersistsValue()
    {
        var configured = new UserRuntimeConfig(
            OllamaHost: new Uri("http://localhost:11434/", UriKind.Absolute),
            DefaultModel: "qwen3.5:4b",
            PromptTimeout: TimeSpan.FromSeconds(30),
            HealthcheckTimeout: TimeSpan.FromSeconds(3),
            ModelsTimeout: TimeSpan.FromSeconds(5),
            Theme: TerminalThemeMode.Dark);
        UserRuntimeConfig? persisted = null;

        var result = ExecuteMainWithConfig(
            () => configured,
            config => persisted = config,
            "config",
            "set",
            UserConfigFile.PromptTimeoutSecondsKey,
            "45");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Atualizando configuracao 'prompt_timeout_seconds'.", result.StdOut);
        Assert.Contains("[INFO] Configuracao atualizada: prompt_timeout_seconds=45.", result.StdOut);
        Assert.NotNull(persisted);
        Assert.Equal(TimeSpan.FromSeconds(45), persisted.Value.PromptTimeout);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ConfigSetCommand_WithThemeKey_ReturnsSuccess_AndPersistsTheme()
    {
        UserRuntimeConfig? persisted = null;

        var result = ExecuteMainWithConfig(
            () => UserRuntimeConfig.Default,
            config => persisted = config,
            "config",
            "set",
            UserConfigFile.ThemeKey,
            "high-contrast");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Atualizando configuracao 'theme'.", result.StdOut);
        Assert.Contains("[INFO] Configuracao atualizada: theme=high-contrast.", result.StdOut);
        Assert.NotNull(persisted);
        Assert.Equal(TerminalThemeMode.HighContrast, persisted.Value.Theme);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ConfigCommand_WithoutAction_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("config");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'config' exige uma acao: 'set' ou 'get'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplos: asxrun config get default_model | asxrun config set default_model qwen3.5:4b.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ConfigGetCommand_WithInvalidArity_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("config", "get", UserConfigFile.DefaultModelKey, "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'config get' exige exatamente uma chave.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun config get default_model.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ConfigSetCommand_WithoutValue_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("config", "set", UserConfigFile.DefaultModelKey);

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'config set' exige uma chave e um valor.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun config set default_model qwen3.5:4b.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ConfigCommand_WithUnsupportedAction_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("config", "list", UserConfigFile.DefaultModelKey);

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A acao 'list' nao e suportada no comando 'config'. Use 'set' ou 'get'.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplos: asxrun config get default_model | asxrun config set default_model qwen3.5:4b.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ConfigGetCommand_WithUppercaseKey_ReturnsSuccess_AndNormalizesKey()
    {
        var configured = UserRuntimeConfig.Default with { DefaultModel = "phi4-mini" };

        var result = ExecuteMainWithConfig(
            () => configured,
            _ => { },
            "config",
            "get",
            "DEFAULT_MODEL");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Lendo configuracao 'default_model'.", result.StdOut);
        Assert.Contains("default_model=phi4-mini", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ConfigGetCommand_WithUnsupportedKey_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("config", "get", "nao_existe");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A chave de configuracao 'nao_existe' nao e suportada.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Chaves suportadas: ollama_host, default_model, prompt_timeout_seconds, healthcheck_timeout_seconds, models_timeout_seconds, theme.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_ConfigSetCommand_WithInvalidNumericValue_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMainWithConfig(
            () => UserRuntimeConfig.Default,
            _ => { },
            "config",
            "set",
            UserConfigFile.PromptTimeoutSecondsKey,
            "abc");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[INFO] Atualizando configuracao 'prompt_timeout_seconds'.", result.StdOut);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O valor 'prompt_timeout_seconds' no arquivo de configuracao deve ser um inteiro positivo em segundos.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun config set prompt_timeout_seconds 30.", result.StdErr);
    }

    [Fact]
    public void Main_SkillsCommand_ReturnsSuccess_AndWritesDefaultSkills()
    {
        var result = ExecuteMain("skills");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Listando skills disponiveis.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. 5 skill(s) disponivel(is).", result.StdOut);
        Assert.Contains("- code-review:", result.StdOut);
        Assert.Contains("- bugfix:", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_SkillsShowCommand_WhenSkillExists_ReturnsSuccess_AndWritesSkillDetails()
    {
        var result = ExecuteMain("skills", "show", "code-review");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Exibindo detalhes da skill 'code-review'.", result.StdOut);
        Assert.Contains("Skill: code-review", result.StdOut);
        Assert.Contains("Instrucoes:", result.StdOut);
        Assert.Contains("Priorize corretude, regressao, seguranca", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_SkillsShowCommand_WhenSkillDoesNotExist_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("skills", "show", "nao-existe");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A skill 'nao-existe' nao foi encontrada.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun skills' para listar as skills disponiveis.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_SkillsReloadCommand_ReturnsSuccess_AndWritesCompletion()
    {
        var result = ExecuteMain("skills", "reload");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Recarregando cache de skills.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido. Cache de skills recarregado.", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_SkillsInitCommand_WhenSkillTemplateDoesNotExist_ReturnsSuccess_AndCreatesSkillFile()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory);

            var result = ExecuteMain("skills", "init");
            var skillFilePath = Path.Combine(temporaryDirectory, SkillFileFormat.SkillFileName);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Contains("[INFO] Criando template de skill no diretorio atual.", result.StdOut);
            Assert.Contains("[INFO] Estado de execucao: concluido. Template de skill criado em", result.StdOut);
            Assert.Contains(skillFilePath, result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
            Assert.True(File.Exists(skillFilePath));

            var templateContent = File.ReadAllText(skillFilePath);
            var parsedSkill = SkillFileFormat.Parse(templateContent, skillFilePath);

            Assert.Equal("my-skill", parsedSkill.Name);
            Assert.Equal("Explique em uma frase o objetivo da skill.", parsedSkill.Description);
            Assert.Equal(
                "Descreva como o modelo deve agir ao usar esta skill.",
                parsedSkill.Instruction);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_SkillsInitCommand_WhenSkillTemplateAlreadyExists_ReturnsInvalidArguments_AndKeepsFile()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory);
            var skillFilePath = Path.Combine(temporaryDirectory, SkillFileFormat.SkillFileName);
            const string existingContent = "conteudo-anterior";
            File.WriteAllText(skillFilePath, existingContent);

            var result = ExecuteMain("skills", "init");

            Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
            Assert.Contains("[INFO] Criando template de skill no diretorio atual.", result.StdOut);
            Assert.Contains("[ERROR] Nao foi possivel executar o comando. O arquivo 'SKILL.md' ja existe no diretorio atual.", result.StdErr);
            Assert.Contains(skillFilePath, result.StdErr);
            Assert.Contains("[ERROR] Sugestao: Remova ou renomeie", result.StdErr);
            Assert.Contains("asxrun skills init", result.StdErr);
            Assert.Equal(existingContent, File.ReadAllText(skillFilePath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_SkillsInitCommand_WithAdditionalArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("skills", "init", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'skills init' nao aceita argumentos adicionais.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun skills init.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_SkillsReloadCommand_WithAdditionalArguments_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("skills", "reload", "extra");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. O comando 'skills reload' nao aceita argumentos adicionais.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Exemplo: asxrun skills reload.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_SkillsReloadCommand_RefreshesCachedSkillsWithoutRestartingProcess()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var originalDirectory = Directory.GetCurrentDirectory();
        var skillsDirectory = Path.Combine(temporaryDirectory, ".asxrun", "skills");
        var skillFilePath = Path.Combine(skillsDirectory, "cache-skill.md");
        var skillName = $"cache-skill-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(skillsDirectory);
            Directory.SetCurrentDirectory(temporaryDirectory);
            SkillCatalog.ReloadCache();

            File.WriteAllText(
                skillFilePath,
                BuildValidSkillFileContent(
                    skillName: skillName,
                    description: "Descricao em cache v1.",
                    instruction: "Instrucao v1."));

            var firstListingResult = ExecuteMain("skills");
            Assert.Equal((int)CliExitCode.Success, firstListingResult.ExitCode);
            Assert.Contains($"- {skillName}: Descricao em cache v1.", firstListingResult.StdOut);

            File.WriteAllText(
                skillFilePath,
                BuildValidSkillFileContent(
                    skillName: skillName,
                    description: "Descricao em cache v2.",
                    instruction: "Instrucao v2."));

            var staleListingResult = ExecuteMain("skills");
            Assert.Equal((int)CliExitCode.Success, staleListingResult.ExitCode);
            Assert.Contains($"- {skillName}: Descricao em cache v1.", staleListingResult.StdOut);
            Assert.DoesNotContain($"- {skillName}: Descricao em cache v2.", staleListingResult.StdOut);

            var reloadResult = ExecuteMain("skills", "reload");
            Assert.Equal((int)CliExitCode.Success, reloadResult.ExitCode);

            var refreshedListingResult = ExecuteMain("skills");
            Assert.Equal((int)CliExitCode.Success, refreshedListingResult.ExitCode);
            Assert.Contains($"- {skillName}: Descricao em cache v2.", refreshedListingResult.StdOut);
            Assert.DoesNotContain($"- {skillName}: Descricao em cache v1.", refreshedListingResult.StdOut);
        }
        finally
        {
            SkillCatalog.ReloadCache();
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_SkillsShowCommand_WhenFileSkillOverridesBuiltIn_PrefersLocalFileSkill()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var originalDirectory = Directory.GetCurrentDirectory();
        var localSkillsDirectory = Path.Combine(temporaryDirectory, ".asxrun", "skills");
        var localSkillPath = Path.Combine(localSkillsDirectory, "code-review-local.md");

        try
        {
            Directory.CreateDirectory(localSkillsDirectory);
            Directory.SetCurrentDirectory(temporaryDirectory);
            SkillCatalog.ReloadCache();

            File.WriteAllText(
                localSkillPath,
                BuildValidSkillFileContent(
                    skillName: "code-review",
                    description: "Code review local customizado.",
                    instruction: "Use checklist local de code review."));

            var result = ExecuteMain("skills", "show", "code-review");

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Contains("Skill: code-review", result.StdOut);
            Assert.Contains("Descricao: Code review local customizado.", result.StdOut);
            Assert.Contains("Use checklist local de code review.", result.StdOut);
            Assert.DoesNotContain("Priorize corretude, regressao, seguranca", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            SkillCatalog.ReloadCache();
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Main_SkillsShowCommand_WhenSkillExistsInLocalAndUserDirectories_PrefersProjectLocalSkill()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var originalDirectory = Directory.GetCurrentDirectory();
        var userHomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var testRootSuffix = $"asxrun-tests-{Guid.NewGuid():N}";
        var localSkillsDirectory = Path.Combine(temporaryDirectory, ".asxrun", "skills", "project");
        Assert.False(string.IsNullOrWhiteSpace(userHomeDirectory));
        var userSkillsRootDirectory = Path.GetFullPath(Path.Combine(userHomeDirectory, ".asxrun", "skills", testRootSuffix));
        var userSkillsDirectory = Path.Combine(userSkillsRootDirectory, "user");
        var skillName = $"precedence-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(localSkillsDirectory);
            Directory.CreateDirectory(userSkillsDirectory);
            Directory.SetCurrentDirectory(temporaryDirectory);
            SkillCatalog.ReloadCache();

            File.WriteAllText(
                Path.Combine(localSkillsDirectory, $"{skillName}.md"),
                BuildValidSkillFileContent(
                    skillName: skillName,
                    description: "Descricao local do projeto.",
                    instruction: "Instrucao local do projeto."));
            File.WriteAllText(
                Path.Combine(userSkillsDirectory, $"{skillName}.md"),
                BuildValidSkillFileContent(
                    skillName: skillName,
                    description: "Descricao do usuario.",
                    instruction: "Instrucao do usuario."));

            var result = ExecuteMain("skills", "show", skillName);

            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            Assert.Contains($"Skill: {skillName}", result.StdOut);
            Assert.Contains("Descricao: Descricao local do projeto.", result.StdOut);
            Assert.Contains("Instrucao local do projeto.", result.StdOut);
            Assert.DoesNotContain("Descricao do usuario.", result.StdOut);
            Assert.DoesNotContain("Instrucao do usuario.", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        }
        finally
        {
            SkillCatalog.ReloadCache();
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(temporaryDirectory, recursive: true);

            if (Directory.Exists(userSkillsRootDirectory))
            {
                Directory.Delete(userSkillsRootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Main_SkillCommand_WithModelFlag_ReturnsSuccess_AndForwardsPromptWithSkillContext()
    {
        var result = ExecuteMainWithModelAwareStreamingExecutor(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            "skill",
            "code-review",
            "--model",
            "qwen2.5-coder:7b",
            "revisar",
            "controller");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Executando skill 'code-review'.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: [SKILL: code-review]", result.StdOut);
        Assert.Contains("[TAREFA]", result.StdOut);
        Assert.Contains("revisar controller", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_SkillCommand_WhenSkillDoesNotExist_ReturnsInvalidArguments_AndWritesError()
    {
        var result = ExecuteMain("skill", "nao-existe", "fazer algo");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("[ERROR] Nao foi possivel executar o comando. A skill 'nao-existe' nao foi encontrada.", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Use 'asxrun skills' para listar as skills disponiveis.", result.StdErr);
        Assert.Equal(string.Empty, result.StdOut);
    }

    [Fact]
    public void Main_AskCommand_WhenPromptExecutorFails_ReturnsRuntimeError_AndWritesErrorState()
    {
        var result = ExecuteMainWithExecutor(
            _ => throw new InvalidOperationException("falha simulada"),
            "ask",
            "teste");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: tool call.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[ERROR] Estado de execucao: erro. Nao foi possivel executar o prompt: falha simulada", result.StdErr);
        Assert.Contains("[FAIL]", result.StdErr);
        Assert.Contains("[ERROR] Nao foi possivel concluir a execucao. Ocorreu um erro interno: falha simulada", result.StdErr);
        Assert.Contains("[ERROR] Sugestao: Tente novamente. Se o problema persistir, revise os logs acima.", result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_WhenPromptExecutorFailsWithSensitiveData_MasksSecretInLogs()
    {
        var result = ExecuteMainWithExecutor(
            _ => throw new InvalidOperationException("falha simulada token=abc123"),
            "ask",
            "teste");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.DoesNotContain("abc123", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("token=***", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_AskCommand_WhenCtrlCIsPressed_ReturnsCancelled_AndWritesCancellationState()
    {
        var result = ExecuteMainWithStreamingExecutorAndCancellation(
            StreamPromptRespectingCancellation,
            CreateImmediateCancellationRegistration(),
            "ask",
            "cancelar");

        Assert.Equal((int)CliExitCode.Cancelled, result.ExitCode);
        Assert.Contains("[INFO] Executando comando unico 'ask'.", result.StdOut);
        Assert.Contains("[INFO] Cancelamento solicitado via Ctrl+C. Interrompendo prompt em execucao.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: conectando.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: tool call.", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: processando.", result.StdOut);
        Assert.Contains("[ERROR] Estado de execucao: erro. Execucao cancelada pelo usuario.", result.StdErr);
        Assert.DoesNotContain("Ocorreu um erro interno", result.StdErr);
    }

    [Fact]
    public void Main_ChatCommand_WhenCtrlCIsPressedDuringPrompt_CancelsCurrentPrompt_AndContinues()
    {
        var result = ExecuteMainWithInputAndStreamingExecutorAndCancellation(
            "primeiro prompt\nsegundo prompt\nexit\n",
            StreamPromptRespectingCancellation,
            CreateCancelFirstPromptRegistration(),
            "chat");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Modo interativo iniciado. Digite 'exit' para sair.", result.StdOut);
        Assert.Contains("[INFO] Cancelamento solicitado via Ctrl+C. Interrompendo prompt em execucao.", result.StdOut);
        Assert.Contains("[INFO] Prompt cancelado. Digite outro prompt ou 'exit' para sair.", result.StdOut);
        Assert.Contains("Prompt: segundo prompt", result.StdOut);
        Assert.Contains("[INFO] Estado de execucao: concluido.", result.StdOut);
        Assert.Contains("[INFO] Modo interativo encerrado.", result.StdOut);
        Assert.Contains("[ERROR] Estado de execucao: erro. Execucao cancelada pelo usuario.", result.StdErr);
        Assert.DoesNotContain("Ocorreu um erro interno", result.StdErr);
    }

    [Fact]
    public void Main_AskCommand_RegistersExecutionCheckpointForEachStage()
    {
        var checkpoints = new List<ExecutionSessionCheckpoint>();

        var result = ExecuteMainWithCheckpoints(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            static () => Array.Empty<ExecutionSessionCheckpoint>(),
            checkpoint => checkpoints.Add(checkpoint),
            "ask",
            "--model",
            "qwen2.5-coder:7b",
            "gerar",
            "teste");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(5, checkpoints.Count);
        Assert.Equal(
            ["connecting", "tool-call", "processing", "diff", "completed"],
            checkpoints.Select(static checkpoint => checkpoint.Stage).ToArray());
        Assert.Equal(ExecutionCheckpointStatus.Completed, checkpoints[^1].Status);
        Assert.All(checkpoints, static checkpoint => Assert.Equal("ask", checkpoint.Command));
        Assert.All(checkpoints, static checkpoint => Assert.Equal("gerar teste", checkpoint.Prompt));
        Assert.All(checkpoints, static checkpoint => Assert.Equal("qwen2.5-coder:7b", checkpoint.Model));
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ResumeCommand_WhenInterruptedSessionExists_RestartsLatestSession()
    {
        var checkpoints = new[]
        {
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero),
                SessionId: "sessao-concluida",
                Command: "ask",
                Stage: "completed",
                Status: ExecutionCheckpointStatus.Completed,
                Prompt: "prompt concluido",
                Model: "qwen3.5:4b",
                SkillName: null,
                Detail: "ok"),
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 1, 0, TimeSpan.Zero),
                SessionId: "sessao-interrompida",
                Command: "ask",
                Stage: "error",
                Status: ExecutionCheckpointStatus.Failed,
                Prompt: "retomar este prompt",
                Model: "qwen2.5-coder:7b",
                SkillName: null,
                Detail: "falha")
        };

        var result = ExecuteMainWithCheckpoints(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            () => checkpoints,
            static _ => { },
            "resume");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("[INFO] Buscando checkpoint de sessao interrompida.", result.StdOut);
        Assert.Contains("Retomando sessao 'sessao-interrompida' do comando 'ask' a partir da etapa 'error'.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: retomar este prompt", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ResumeCommand_WhenInterruptedAgentSessionExists_RestartsLatestSession()
    {
        var checkpoints = new[]
        {
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 2, 0, TimeSpan.Zero),
                SessionId: "sessao-agent-interrompida",
                Command: "agent",
                Stage: "error",
                Status: ExecutionCheckpointStatus.Failed,
                Prompt: "otimizar pipeline de deploy",
                Model: "qwen2.5-coder:7b",
                SkillName: null,
                Detail: "falha")
        };

        var result = ExecuteMainWithCheckpoints(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            () => checkpoints,
            static _ => { },
            "resume");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("Retomando sessao 'sessao-agent-interrompida' do comando 'agent' a partir da etapa 'error'.", result.StdOut);
        Assert.Contains("[INFO] Iniciando modo agente autonomo por objetivo.", result.StdOut);
        Assert.Contains("Modelo: qwen2.5-coder:7b | Prompt: [MODO: AGENTE AUTONOMO]", result.StdOut);
        Assert.Contains("[OBJETIVO]", result.StdOut);
        Assert.Contains("otimizar pipeline de deploy", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ResumeCommand_WhenAgentLoopCheckpointExists_ResumesFromSavedIteration()
    {
        var capturedPrompts = new List<string>();
        var loopCheckpointDetail = JsonSerializer.Serialize(new
        {
            Kind = "agent-loop-resume-v1",
            Version = 1,
            NextIteration = 3,
            MaxSteps = 6,
            MaxTimeSeconds = 90d,
            MaxCost = 25000m,
            AccumulatedCost = 1450m,
            ElapsedSeconds = 12.5d,
            PreviousVerificationOutput = "falta validar rollback",
            PreviousRefinementOutput = "aplicar validacao em ambiente de staging"
        });
        var checkpoints = new[]
        {
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 3, 0, TimeSpan.Zero),
                SessionId: "sessao-agent-com-loop",
                Command: "agent",
                Stage: "error",
                Status: ExecutionCheckpointStatus.Failed,
                Prompt: "otimizar pipeline de deploy",
                Model: "qwen2.5-coder:7b",
                SkillName: null,
                Detail: "falha"),
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 2, 0, TimeSpan.Zero),
                SessionId: "sessao-agent-com-loop",
                Command: "agent",
                Stage: "agent-loop",
                Status: ExecutionCheckpointStatus.InProgress,
                Prompt: "otimizar pipeline de deploy",
                Model: "qwen2.5-coder:7b",
                SkillName: null,
                Detail: loopCheckpointDetail)
        };

        var result = ExecuteMainWithCheckpoints(
            (prompt, _, _) =>
            {
                capturedPrompts.Add(prompt);
                var response = prompt.Contains("Fase atual: verify", StringComparison.Ordinal)
                    ? "VERIFICATION_STATUS=done\nIteracao concluida."
                    : "ok";
                return StreamSingleChunk(response);
            },
            () => checkpoints,
            static _ => { },
            "resume");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(
            "Checkpoint incremental do agente identificado: iteracao 3/6.",
            result.StdOut);
        Assert.Contains(
            "Retomando loop autonomo a partir da iteracao 3/6.",
            result.StdOut);
        Assert.DoesNotContain(
            capturedPrompts,
            static prompt => prompt.Contains("Iteracao: 1", StringComparison.Ordinal));
        Assert.All(
            capturedPrompts,
            static prompt => Assert.Contains("Iteracao: 3", prompt));
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Main_ResumeCommand_WhenNoInterruptedSessionExists_ReturnsRuntimeError()
    {
        var checkpoints = new[]
        {
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero),
                SessionId: "sessao-concluida",
                Command: "ask",
                Stage: "completed",
                Status: ExecutionCheckpointStatus.Completed,
                Prompt: "prompt concluido",
                Model: "qwen3.5:4b",
                SkillName: null,
                Detail: "ok")
        };

        var result = ExecuteMainWithCheckpoints(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            () => checkpoints,
            static _ => { },
            "resume");

        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains("Nenhuma sessao interrompida de ask/agent/skill foi encontrada para retomar.", result.StdErr);
    }

    [Fact]
    public void Main_ResumeCommand_WithUnknownSessionId_ReturnsInvalidArguments()
    {
        var checkpoints = new[]
        {
            new ExecutionSessionCheckpoint(
                TimestampUtc: new DateTimeOffset(2026, 4, 20, 12, 1, 0, TimeSpan.Zero),
                SessionId: "sessao-interrompida",
                Command: "ask",
                Stage: "error",
                Status: ExecutionCheckpointStatus.Failed,
                Prompt: "retomar este prompt",
                Model: "qwen2.5-coder:7b",
                SkillName: null,
                Detail: "falha")
        };

        var result = ExecuteMainWithCheckpoints(
            static (prompt, model, _) => StreamPromptWithModel(prompt, model),
            () => checkpoints,
            static _ => { },
            "resume",
            "sessao-inexistente");

        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("Nenhum checkpoint foi encontrado para a sessao 'sessao-inexistente'.", result.StdErr);
    }

    private static ExecutionResult ExecuteMain(params string[] args)
    {
        return ExecuteMainWithInputInternal(null, null, null, null, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithExecutor(Func<string, string> promptExecutor, params string[] args)
    {
        return ExecuteMainWithInputInternal(null, promptExecutor, null, null, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithStreamingExecutor(
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(null, null, promptExecutor, null, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithModelAwareStreamingExecutor(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(null, null, null, promptExecutor, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithModelAwareStreamingExecutorAndToolRuntime(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        IToolRuntime toolRuntime,
        params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var stdOutWriter = new StringWriter();
        using var stdErrWriter = new StringWriter();

        Console.SetOut(stdOutWriter);
        Console.SetError(stdErrWriter);

        try
        {
            var exitCode = Program.RunForTests(args, promptExecutor, toolRuntime);
            return new ExecutionResult(
                ExitCode: exitCode,
                StdOut: stdOutWriter.ToString(),
                StdErr: stdErrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static ExecutionResult ExecuteMainWithModelAwareStreamingExecutorAndModels(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var stdOutWriter = new StringWriter();
        using var stdErrWriter = new StringWriter();

        Console.SetOut(stdOutWriter);
        Console.SetError(stdErrWriter);

        try
        {
            var exitCode = Program.RunForTests(args, promptExecutor, modelsExecutor);
            return new ExecutionResult(
                ExitCode: exitCode,
                StdOut: stdOutWriter.ToString(),
                StdErr: stdErrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static ExecutionResult ExecuteMainWithCheckpoints(
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<IReadOnlyList<ExecutionSessionCheckpoint>> checkpointLoader,
        Action<ExecutionSessionCheckpoint> checkpointAppender,
        params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var stdOutWriter = new StringWriter();
        using var stdErrWriter = new StringWriter();

        Console.SetOut(stdOutWriter);
        Console.SetError(stdErrWriter);

        try
        {
            var exitCode = Program.RunForTests(
                args,
                promptExecutor,
                checkpointAppender,
                checkpointLoader);

            return new ExecutionResult(
                ExitCode: exitCode,
                StdOut: stdOutWriter.ToString(),
                StdErr: stdErrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static ExecutionResult ExecuteMainWithStreamingExecutorAndCancellation(
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(
            null,
            null,
            promptExecutor,
            null,
            null,
            null,
            cancelSignalRegistration,
            null,
            null,
            args);
    }

    private static ExecutionResult ExecuteMainWithInput(string? stdIn, params string[] args)
    {
        return ExecuteMainWithInputInternal(stdIn, null, null, null, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithInput(
        string? stdIn,
        Func<string, string> promptExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(stdIn, promptExecutor, null, null, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithInputAndModelAwareStreamingExecutor(
        string? stdIn,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(stdIn, null, null, promptExecutor, null, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithInputAndStreamingExecutorAndCancellation(
        string? stdIn,
        Func<string, CancellationToken, IAsyncEnumerable<string>> promptExecutor,
        Func<CancellationTokenSource, Action, IDisposable> cancelSignalRegistration,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(
            stdIn,
            null,
            promptExecutor,
            null,
            null,
            null,
            cancelSignalRegistration,
            null,
            null,
            args);
    }

    private static ExecutionResult ExecuteMainWithHealthcheck(
        Func<CancellationToken, Task<OllamaHealthcheckResult>> healthcheckExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(null, null, null, null, healthcheckExecutor, null, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithModels(
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(null, null, null, null, null, modelsExecutor, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithInputAndModels(
        string? stdIn,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>> modelsExecutor,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(stdIn, null, null, null, null, modelsExecutor, null, null, null, args);
    }

    private static ExecutionResult ExecuteMainWithConfig(
        Func<UserRuntimeConfig> configLoader,
        Action<UserRuntimeConfig> configSaver,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(null, null, null, null, null, null, null, configLoader, configSaver, args);
    }

    private static ExecutionResult ExecuteMainWithMcp(
        Func<IReadOnlyList<McpServerDefinition>> mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>> mcpServersSaver,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>> mcpServerTester,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            mcpServersLoader,
            mcpServersSaver,
            mcpServerTester,
            args);
    }

    private static ExecutionResult ExecuteMainWithWorkspacePatchAudit(
        Func<WorkspacePatchAuditEntry, string> workspacePatchAuditAppender,
        params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var stdOutWriter = new StringWriter();
        using var stdErrWriter = new StringWriter();

        Console.SetOut(stdOutWriter);
        Console.SetError(stdErrWriter);

        try
        {
            var exitCode = Program.RunForTests(args, workspacePatchAuditAppender);
            return new ExecutionResult(
                ExitCode: exitCode,
                StdOut: stdOutWriter.ToString(),
                StdErr: stdErrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static ExecutionResult ExecuteMainWithHistory(
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader,
        params string[] args)
    {
        return ExecuteMainWithHistory(historyLoader, static () => { }, args);
    }

    private static ExecutionResult ExecuteMainWithHistory(
        Func<IReadOnlyList<PromptHistoryEntry>> historyLoader,
        Action historyClearer,
        params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var stdOutWriter = new StringWriter();
        using var stdErrWriter = new StringWriter();

        Console.SetOut(stdOutWriter);
        Console.SetError(stdErrWriter);

        try
        {
            var exitCode = Program.RunForTests(args, historyLoader, historyClearer);
            return new ExecutionResult(
                ExitCode: exitCode,
                StdOut: stdOutWriter.ToString(),
                StdErr: stdErrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static ExecutionResult ExecuteMainWithInputInternal(
        string? stdIn,
        Func<string, string>? promptExecutor,
        Func<string, CancellationToken, IAsyncEnumerable<string>>? promptStreamingExecutor,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>>? modelAwarePromptStreamingExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>>? healthcheckExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>>? modelsExecutor,
        Func<CancellationTokenSource, Action, IDisposable>? cancelSignalRegistration,
        Func<UserRuntimeConfig>? configLoader,
        Action<UserRuntimeConfig>? configSaver,
        params string[] args)
    {
        return ExecuteMainWithInputInternal(
            stdIn,
            promptExecutor,
            promptStreamingExecutor,
            modelAwarePromptStreamingExecutor,
            healthcheckExecutor,
            modelsExecutor,
            cancelSignalRegistration,
            configLoader,
            configSaver,
            mcpServersLoader: null,
            mcpServersSaver: null,
            mcpServerTester: null,
            args);
    }

    private static ExecutionResult ExecuteMainWithInputInternal(
        string? stdIn,
        Func<string, string>? promptExecutor,
        Func<string, CancellationToken, IAsyncEnumerable<string>>? promptStreamingExecutor,
        Func<string, string?, CancellationToken, IAsyncEnumerable<string>>? modelAwarePromptStreamingExecutor,
        Func<CancellationToken, Task<OllamaHealthcheckResult>>? healthcheckExecutor,
        Func<CancellationToken, Task<IReadOnlyList<OllamaLocalModel>>>? modelsExecutor,
        Func<CancellationTokenSource, Action, IDisposable>? cancelSignalRegistration,
        Func<UserRuntimeConfig>? configLoader,
        Action<UserRuntimeConfig>? configSaver,
        Func<IReadOnlyList<McpServerDefinition>>? mcpServersLoader,
        Action<IReadOnlyList<McpServerDefinition>>? mcpServersSaver,
        Func<McpServerDefinition, CancellationToken, Task<McpServerTestResult>>? mcpServerTester,
        params string[] args)
    {
        var configuredPromptExecutors =
            (promptExecutor is not null ? 1 : 0)
            + (promptStreamingExecutor is not null ? 1 : 0)
            + (modelAwarePromptStreamingExecutor is not null ? 1 : 0);

        if (configuredPromptExecutors > 1)
        {
            throw new InvalidOperationException(
                "Use apenas um executor de prompt por teste.");
        }

        if ((configLoader is null) != (configSaver is null))
        {
            throw new InvalidOperationException(
                "Informe configLoader e configSaver juntos.");
        }

        if ((mcpServersLoader is null) != (mcpServersSaver is null))
        {
            throw new InvalidOperationException(
                "Informe mcpServersLoader e mcpServersSaver juntos.");
        }

        if (mcpServerTester is not null && mcpServersLoader is null)
        {
            throw new InvalidOperationException(
                "mcpServerTester exige mcpServersLoader e mcpServersSaver.");
        }

        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalIn = Console.In;

        using var stdOutWriter = new StringWriter();
        using var stdErrWriter = new StringWriter();
        using var stdInReader = stdIn is null ? null : new StringReader(stdIn);

        Console.SetOut(stdOutWriter);
        Console.SetError(stdErrWriter);
        if (stdInReader is not null)
        {
            Console.SetIn(stdInReader);
        }

        try
        {
            var exitCode = configLoader is not null
                ? (promptExecutor, promptStreamingExecutor, modelAwarePromptStreamingExecutor, healthcheckExecutor, modelsExecutor, cancelSignalRegistration) switch
                {
                    (null, null, null, null, null, null) => Program.RunForTests(args, configLoader, configSaver!),
                    _ => throw new InvalidOperationException(
                        "Combinacao de executores de teste invalida para config.")
                }
                : modelsExecutor is not null
                ? (promptExecutor, promptStreamingExecutor, modelAwarePromptStreamingExecutor, healthcheckExecutor, cancelSignalRegistration) switch
                {
                    (null, null, null, null, null) => Program.RunForTests(args, modelsExecutor),
                    _ => throw new InvalidOperationException(
                        "Combinacao de executores de teste invalida para models.")
                }
                : mcpServersLoader is not null
                ? (promptExecutor, promptStreamingExecutor, modelAwarePromptStreamingExecutor, healthcheckExecutor, modelsExecutor, cancelSignalRegistration) switch
                {
                    (null, null, null, null, null, null) => Program.RunForTests(
                        args,
                        mcpServersLoader,
                        mcpServersSaver!,
                        mcpServerTester),
                    _ => throw new InvalidOperationException(
                        "Combinacao de executores de teste invalida para mcp.")
                }
                : cancelSignalRegistration is null
                ? (promptExecutor, promptStreamingExecutor, modelAwarePromptStreamingExecutor, healthcheckExecutor) switch
                {
                    (null, null, null, null) => Program.RunForTests(args),
                    (not null, null, null, null) => Program.RunForTests(args, promptExecutor),
                    (null, not null, null, null) => Program.RunForTests(args, promptStreamingExecutor),
                    (null, null, not null, null) => Program.RunForTests(args, modelAwarePromptStreamingExecutor),
                    (null, null, null, not null) => Program.RunForTests(
                        args,
                        static prompt => $"Prompt: {prompt}",
                        healthcheckExecutor),
                    (not null, null, null, not null) => Program.RunForTests(args, promptExecutor, healthcheckExecutor),
                    (null, not null, null, not null) => Program.RunForTests(args, promptStreamingExecutor, healthcheckExecutor),
                    (null, null, not null, not null) => Program.RunForTests(args, modelAwarePromptStreamingExecutor, healthcheckExecutor),
                    _ => throw new InvalidOperationException("Combinacao de executores de teste invalida.")
                }
                : (promptExecutor, promptStreamingExecutor, modelAwarePromptStreamingExecutor, healthcheckExecutor) switch
                {
                    (null, not null, null, null) => Program.RunForTests(args, promptStreamingExecutor, cancelSignalRegistration),
                    (null, null, not null, null) => Program.RunForTests(args, modelAwarePromptStreamingExecutor, cancelSignalRegistration),
                    _ => throw new InvalidOperationException(
                        "Combinacao de executores de teste invalida para cancelamento.")
                };

            return new ExecutionResult(
                ExitCode: exitCode,
                StdOut: stdOutWriter.ToString(),
                StdErr: stdErrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Console.SetIn(originalIn);
        }
    }

    private readonly record struct ExecutionResult(
        int ExitCode,
        string StdOut,
        string StdErr);

    private static async IAsyncEnumerable<string> StreamPromptInChunks(string prompt)
    {
        yield return "Prompt: ";
        yield return prompt;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamPromptWithCodeBlockInChunks(string prompt)
    {
        yield return "Resposta para: ";
        yield return prompt;
        yield return "\n```cs";
        yield return "harp\n";
        yield return "Console.WriteLine(\"ok\");\n";
        yield return "```";
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamPromptWithModel(string prompt, string? model)
    {
        yield return $"Modelo: {model ?? "<padrao>"} | Prompt: {prompt}";
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamSingleChunk(string content)
    {
        yield return content;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamSingleChunkAfterDelay(
        string content,
        int delayMilliseconds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(delayMilliseconds, cancellationToken);
        yield return content;
    }

    private static async IAsyncEnumerable<string> StreamPromptWithDiffInChunks()
    {
        yield return "```diff\n";
        yield return "+nova linha\n";
        yield return "-linha antiga\n";
        yield return "```\n";
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> StreamPromptRespectingCancellation(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return $"Prompt: {prompt}";
        await Task.CompletedTask;
    }

    private static Func<CancellationTokenSource, Action, IDisposable> CreateImmediateCancellationRegistration()
    {
        return (cancellationTokenSource, onCancellationRequested) =>
        {
            onCancellationRequested();
            cancellationTokenSource.Cancel();
            return NoopDisposable.Instance;
        };
    }

    private static Func<CancellationTokenSource, Action, IDisposable> CreateCancelFirstPromptRegistration()
    {
        var shouldCancel = true;

        return (cancellationTokenSource, onCancellationRequested) =>
        {
            if (shouldCancel)
            {
                shouldCancel = false;
                onCancellationRequested();
                cancellationTokenSource.Cancel();
            }

            return NoopDisposable.Instance;
        };
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class StubToolRuntime(
        Func<ToolExecutionRequest, CancellationToken, ToolExecutionResult> execute) : IToolRuntime
    {
        public IReadOnlyList<ToolDescriptor> ListTools()
        {
            return
            [
                new ToolDescriptor(
                    Name: "shell",
                    Description: "Stub shell para testes.",
                    Parameters:
                    [
                        new ToolParameter(
                            Name: "script",
                            Description: "Script capturado pelo teste.",
                            IsRequired: true)
                    ])
            ];
        }

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(execute(request, cancellationToken));
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"asxrun-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static string BuildValidSkillFileContent(
        string skillName,
        string description,
        string instruction)
    {
        return
            $"""
            ---
            name: {skillName}
            description: {description}
            instruction: |
              {instruction}
            ---
            """;
    }

    private static string BuildPatchRequestFileContent(
        params (string Kind, string Path, string? Content, string? ExpectedContent)[] changes)
    {
        var payload = new Dictionary<string, object?>
        {
            ["changes"] = changes
                .Select(static change => new Dictionary<string, object?>
                {
                    ["kind"] = change.Kind,
                    ["path"] = change.Path,
                    ["content"] = change.Content,
                    ["expectedContent"] = change.ExpectedContent
                })
                .ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }
}
