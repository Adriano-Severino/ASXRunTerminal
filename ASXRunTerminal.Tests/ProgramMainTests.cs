using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
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
        Assert.Contains("asxrun chat", result.StdOut);
        Assert.Contains("asxrun doctor", result.StdOut);
        Assert.Contains("asxrun models", result.StdOut);
        Assert.Contains("asxrun history", result.StdOut);
        Assert.Contains("asxrun history [--clear]", result.StdOut);
        Assert.Contains("asxrun config get <chave>", result.StdOut);
        Assert.Contains("asxrun config set <chave> <valor>", result.StdOut);
        Assert.Contains("asxrun skills", result.StdOut);
        Assert.Contains("asxrun skill <nome> [--model <modelo>] \"prompt\"", result.StdOut);
        Assert.Contains("--model <nome>", result.StdOut);
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
        Assert.Contains("[INFO] Ferramentas e recursos locais disponiveis:", result.StdOut);
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
        Assert.Contains("[INFO] Listando skills padrao disponiveis.", result.StdOut);
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
}
