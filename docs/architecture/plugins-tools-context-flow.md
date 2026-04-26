# Arquitetura de Plugins/Ferramentas e Fluxo de Contexto

## Objetivo
Documentar como o ASXRunTerminal organiza extensoes (tools, MCP e skills) e como o contexto e construido/executado no fluxo `ask/chat/skill/context`.

## Escopo Atual (v0.1.x)
- Runtime unificado de tools locais com `IToolProvider` + `ToolRuntime`.
- Suporte MCP para catalogo, conexao, handshake e descoberta de schema de tools.
- Skills built-in e skills por arquivo (`SKILL.md`) com precedencia por diretorio.
- Contexto de workspace com deteccao de raiz, mapeamento de arquivos e indice em memoria.
- Persistencia operacional local (config, checkpoints, auditoria de patch, historico e catalogo MCP).
- `ask/chat` ainda nao executam tool calls MCP durante a geracao; isso permanece como evolucao da `TASK 6.2.5`.

## Mapa de Componentes
| Camada | Responsabilidade | Principais arquivos |
| --- | --- | --- |
| `Program` | Orquestra parse, dispatch, resiliencia, checkpoint e execucao de comandos | `ASXRunTerminal/Program.cs` |
| `core` | Contratos e regras de negocio (tool runtime, skills, contexto de workspace, patch) | `ASXRunTerminal/core/*.cs` |
| `infra` | Adaptadores de execucao (shell, MCP stdio/http/sse, descoberta/validacao MCP, Ollama HTTP) | `ASXRunTerminal/infra/*.cs` |
| `config` | Persistencia e validacao de arquivos locais (`~/.asxrun` e `.asxrun` no workspace) | `ASXRunTerminal/config/*.cs` |

## Fluxo 1: Ferramentas Locais (Plugin Runtime)
1. `Program.CreateDefaultToolRuntime()` registra providers locais (`echo`, `powershell`, `bash/zsh`).
2. `ToolRuntime.ListTools()` agrega todos os `ToolDescriptor` e adiciona alias `shell` conforme plataforma (`ShellEnvironmentDetector`).
3. `ToolRuntime.ExecuteAsync()` resolve o nome da tool, tenta fallback de shell (`powershell`/`bash`/`zsh`) e seleciona provider via `CanHandle`.
4. Provider valida argumentos obrigatorios e aplica guardrails (`ShellCommandGuardrailEvaluator` + `ShellCommandPermissionPolicy`).
5. `ShellProcessExecutor` executa processo com captura de `stdout/stderr`, timeout, cancelamento e `exit code`.
6. Resultado retorna como `ToolExecutionResult` com sanitizacao de segredos (`SecretMasker`).

## Fluxo 2: Plugins MCP (Catalogo + Descoberta)
1. `mcp add/remove/list` persiste definicoes em `~/.asxrun/mcp-servers.json` (`McpServerCatalogFile`).
2. `mcp test` instancia cliente por transporte: `stdio` -> `McpStdioClient`; `http/sse` -> `McpRemoteClient`.
3. Teste executa handshake (`initialize` + `notifications/initialized`).
4. Descoberta chama `tools/list` (`McpToolDiscovery`), parseia schema e gera `ToolDescriptor`.
5. Validacao de argumentos segue `McpToolSchemaValidator` (required e `additionalProperties=false`).

## Fluxo 3: Contexto de Prompt (`ask`, `skill`, `resume`)
1. `ask` e `skill` chamam `ExecutePrompt(...)` com estados de execucao (`connecting`, `tool-call`, `processing`, `diff`, `completed/error`).
2. `skill` injeta contexto no prompt final por `BuildSkillPrompt(...)`, com bloco `[SKILL: <nome>]` + `instruction` e bloco `[TAREFA]` com prompt do usuario.
3. Execucao usa `fallbackPromptExecutor` com retries/circuit breaker e fallback de modelo (selecionado -> default -> modelos locais disponiveis).
4. Cada etapa gera `ExecutionSessionCheckpoint` persistido em `~/.asxrun/execution-checkpoints`.
5. `resume` encontra checkpoint interrompido mais recente (ou `session-id` explicito) e reexecuta `ask` ou `skill`.

## Fluxo 4: Contexto de Workspace (`context` e operacoes de arquivo)
1. `WorkspaceRootDetector` resolve a raiz por precedencia: monorepo (`pnpm-workspace.yaml`, `nx.json`, `workspaces` no `package.json`) -> solution/workspace (`.sln`, `.slnx`, `.code-workspace`) -> git root (`.git`) -> diretorio atual.
2. `WorkspaceFileStructureMapper` percorre arquivos respeitando `.gitignore`, limites de profundidade e limite de entradas.
3. `WorkspaceContextFileIndex` constroi indices por caminho, nome de arquivo e extensao para consultas rapidas.
4. `context` exibe resumo operacional (`entry count`, truncamento, limites aplicados, timestamp de indexacao).
5. `patch` usa `WorkspacePatchEngine` + `WorkspaceFileOperations` com politica opcional de permissoes (`.asxrun/workspace-permissions.json`) e auditoria local (`~/.asxrun/patch-audit`).

## Arquivos de Operacao
- Usuario (`~/.asxrun`): `config`, `history`, `mcp-servers.json`, `execution-checkpoints`, `patch-audit`.
- Workspace (`<raiz>/.asxrun`): `workspace-permissions.json`, `shell-command-policy.json`, `skills/**/*.md`.

## Guia de Extensao
1. Nova tool local: implementar `IToolProvider`, registrar no `CreateDefaultToolRuntime()` e adicionar testes de contrato/runtime.
2. Novo servidor MCP: cadastrar com `asxrun mcp add ...`, validar via `asxrun mcp test <nome>` e revisar schema de tool exposto.
3. Nova skill: criar `SKILL.md` com `name`, `description`, `instruction`; publicar em `<workspace>/.asxrun/skills` (precedencia alta) ou `~/.asxrun/skills`; validar com `asxrun skills` e `asxrun skills show <nome>`.

## Referencias de Codigo
- `ASXRunTerminal/Program.cs`
- `ASXRunTerminal/core/IToolProvider.cs`
- `ASXRunTerminal/infra/ToolRuntime.cs`
- `ASXRunTerminal/infra/PowerShellToolProvider.cs`
- `ASXRunTerminal/infra/UnixShellToolProvider.cs`
- `ASXRunTerminal/core/SkillCatalog.cs`
- `ASXRunTerminal/core/SkillFileFormat.cs`
- `ASXRunTerminal/core/WorkspaceRootDetector.cs`
- `ASXRunTerminal/core/WorkspaceContextFileIndex.cs`
- `ASXRunTerminal/infra/McpToolDiscovery.cs`
- `ASXRunTerminal/config/McpServerCatalogFile.cs`
