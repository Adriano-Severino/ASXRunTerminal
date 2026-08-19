using ASXRunTerminal.Core;
using ASXRunTerminal.Infra;
using Microsoft.Extensions.Logging;

namespace ASXRunTerminal.Commands;

/// <summary>
/// Command for displaying help information.
/// </summary>
internal sealed class HelpCommand : CommandBase
{
    private const string CliName = "asxrun";
    private readonly ILogger<HelpCommand> _logger;

    public HelpCommand(ILogger<HelpCommand>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HelpCommand>.Instance;
    }

    public override string Name => "help";
    public override string Description => "Exibe informacoes de ajuda.";

    public override CommandParseResult ParseArguments(string[] args)
    {
        // Help command doesn't require any arguments
        return Success(new Dictionary<string, object>());
    }

    public override Task<int> ExecuteAsync(CommandParseResult parseResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        if (parseResult.HasError)
        {
            WriteFriendlyError(parseResult.Error ?? CliFriendlyError.Runtime("Unknown error"));
            return Task.FromResult((int)(parseResult.Error?.ExitCode ?? CliExitCode.RuntimeError));
        }

        WriteHelp();
        return Task.FromResult((int)CliExitCode.Success);
    }

    private static void WriteHelp()
    {
        var helpHeader = TerminalVisualComponents.BuildHeader(
            "ASXRunTerminal CLI",
            "Terminal local para produtividade com IA.");
        Console.WriteLine((string)helpHeader);
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine($"  {CliName} [opcao]");
        Console.WriteLine($"  {CliName} [comando] [argumentos]");
        Console.WriteLine();
        Console.WriteLine("Opcoes:");
        Console.WriteLine("  --help, -h      Exibe esta mensagem de ajuda.");
        Console.WriteLine("  --version, -v   Exibe a versao do CLI.");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  ask              Executa um prompt unico com streaming de resposta.");
        Console.WriteLine("  agent            Inicia modo agente autonomo orientado por objetivo.");
        Console.WriteLine("  chat             Modo interativo no terminal.");
        Console.WriteLine("  code-review      Revisao de codigo com RAG para analise contextual.");
        Console.WriteLine("  doctor           Valida a disponibilidade do Ollama.");
        Console.WriteLine("  models           Lista os modelos locais do Ollama.");
        Console.WriteLine("  context          Inspeciona o resumo do workspace atual.");
        Console.WriteLine("  patch            Aplica mudancas de arquivo por JSON e exibe diff unificado.");
        Console.WriteLine("  history          Mostra e limpa historico local.");
        Console.WriteLine("  resume           Retoma a ultima sessao interrompida de ask/agent/skill.");
        Console.WriteLine("  mcp              Gerencia servidores MCP (list/add/remove/test).");
        Console.WriteLine("  config           Le e atualiza configuracao do usuario.");
        Console.WriteLine("  skills           Lista as skills disponiveis.");
        Console.WriteLine("  skill            Executa um prompt usando uma skill padrao.");
        Console.WriteLine();
        Console.WriteLine("Codigos de saida:");
        Console.WriteLine($"  {(int)CliExitCode.Success}  Sucesso.");
        Console.WriteLine($"  {(int)CliExitCode.RuntimeError}  Erro em tempo de execucao.");
        Console.WriteLine($"  {(int)CliExitCode.InvalidArguments}  Argumentos invalidos.");
        Console.WriteLine($"  {(int)CliExitCode.Cancelled}  Execucao cancelada pelo usuario.");
        Console.WriteLine((string)TerminalVisualComponents.BuildSeparator(width: 48));
    }

    private static void WriteFriendlyError(CliFriendlyError error)
    {
        Program.WriteFriendlyError(error);
    }
}
