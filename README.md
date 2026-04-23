# ASXRunTerminal CLI

CLI de produtividade para desenvolvedores com foco em execucao local de IA via Ollama.

## Objetivo

O ASXRunTerminal CLI foi desenhado para entregar uma experiencia parecida com um Copilot CLI, com:

- orquestracao de modelos locais do Ollama;
- comandos rapidos para fluxo de desenvolvimento;
- configuracao e historico locais;
- foco em confiabilidade, privacidade e desempenho.

## Estado Atual

Funcionalidades ja disponiveis:

- `ask`: executa prompt unico com streaming de resposta;
- `chat`: modo interativo no terminal;
- `doctor`: valida disponibilidade do Ollama;
- `models`: lista modelos locais do Ollama;
- `context`: inspeciona resumo do workspace atual;
- `patch`: aplica mudancas de arquivo por JSON e exibe diff unificado (com suporte a `--dry-run`);
- `history`: mostra e limpa historico local;
- `resume`: retoma a ultima sessao interrompida de `ask/skill` com checkpoints por etapa;
- `mcp list/add/remove/test`: gerencia servidores MCP locais/remotos;
- `config get/set`: le e atualiza configuracao do usuario;
- `skills`, `skills show`, `skill`: skills padrao para tarefas tecnicas.

Versao atual: `0.1.0`.
Historico de mudancas: `CHANGELOG.md`.

## Pre-requisitos

1. .NET SDK 10.0 (para compilar e executar o projeto `net10.0`).
2. Ollama instalado e em execucao local.
3. Pelo menos um modelo baixado no Ollama.

Exemplo para preparar o ambiente Ollama:

```bash
ollama pull qwen3.5:4b
```

## Instalacao e Execucao

### 1. Clonar o repositorio

```bash
git clone <url-do-repositorio>
cd ASXRunTerminal
```

### 2. Build local com script

Windows (PowerShell):

```powershell
.\scripts\build.ps1
```

Linux/macOS (Bash):

```bash
chmod +x ./scripts/*.sh
./scripts/build.sh
```

Exemplos uteis:

```powershell
.\scripts\build.ps1 -Configuration Release -Publish
.\scripts\build.ps1 -SkipTests
```

```bash
./scripts/build.sh --configuration Release --publish
./scripts/build.sh --skip-tests
```

### 3. Rodar localmente com script

Windows (PowerShell):

```powershell
.\scripts\run.ps1 doctor
.\scripts\run.ps1 ask "Explique o padrao repository em C#."
```

Linux/macOS (Bash):

```bash
./scripts/run.sh -- doctor
./scripts/run.sh -- ask "Explique o padrao repository em C#."
```

### 4. Comandos manuais equivalentes

```bash
dotnet restore ASXRunTerminal.slnx
dotnet build ASXRunTerminal.slnx -c Debug
```

Rodar sem script (modo desenvolvimento):

```bash
dotnet run --project ASXRunTerminal -- --help
```

Publicar binario local:

```bash
dotnet publish ASXRunTerminal/ASXRunTerminal.csproj -c Release -o ./dist
```

No Windows, execute:

```powershell
.\dist\asxrun.exe --help
```

No Linux/macOS, execute:

```bash
./dist/asxrun --help
```

## Uso Rapido

Se voce ainda nao publicou o binario local, substitua `asxrun` por:

```bash
dotnet run --project ASXRunTerminal --
```

### Diagnostico do Ollama

```bash
asxrun doctor
```

### Listar modelos locais

```bash
asxrun models
```

### Inspecionar contexto do workspace

```bash
asxrun context
```

### Aplicar patch de arquivos com diff

```bash
asxrun patch patch.json
```

Para mudancas destrutivas (`delete`), o CLI pede confirmacao explicita antes de aplicar.
Cada execucao do comando `patch` gera um registro de auditoria local com `sessionId`, sequencia na sessao e diff aplicado/simulado.

### Simular patch sem alterar arquivos (`--dry-run`)

```bash
asxrun patch --dry-run patch.json
```

Exemplo de `patch.json`:

```json
{
  "changes": [
    {
      "kind": "edit",
      "path": "src/Program.cs",
      "content": "linha 1\nlinha 2 atualizada"
    }
  ]
}
```

### Politicas de permissao por workspace (guardrails de arquivo)

Opcionalmente, o comando `patch` pode ser restrito por workspace com o arquivo:

- `<raiz-do-workspace>/.asxrun/workspace-permissions.json`

Quando o arquivo nao existe, o comportamento padrao continua `allow` para todas as operacoes.

Exemplo de politica com `defaultMode=deny`, liberando edicao apenas em `src/**`
e bloqueando delete em `src/secrets/**`:

```json
{
  "defaultMode": "deny",
  "edit": {
    "allow": ["src/**"]
  },
  "delete": {
    "allow": ["src/**"],
    "deny": ["src/secrets/**"]
  }
}
```

Operacoes suportadas na politica: `read`, `create`, `edit`, `copy`, `move`, `delete`.

### Politicas de comandos de shell de alto risco

Os providers de shell (`powershell`, `bash`, `zsh`) aplicam guardrails antes da execucao
do script para bloquear comandos de alto risco.

Arquivo opcional por workspace:

- `<raiz-do-workspace>/.asxrun/shell-command-policy.json`

Quando esse arquivo nao existe, o CLI usa uma `blocklist` default para comandos sensiveis
(ex.: `rm`, `remove-item`, `diskpart`, `shutdown`, `dd`, `mkfs`).

Mesmo quando um comando sensivel estiver em `allow`, a execucao continua bloqueada por padrao
ate receber aprovacao explicita no argumento da tool call:

- `destructive_approval=sim`

Exemplo de politica liberando explicitamente `rm` e adicionando bloqueio para `echo`:

```json
{
  "allow": ["rm"],
  "deny": ["echo"]
}
```

Regras:

- `deny`: adiciona comandos bloqueados.
- `allow`: libera comandos bloqueados (override explicito).
- comandos desbloqueados via `allow` exigem aprovacao explicita por execucao (`destructive_approval=sim`).
- comando bloqueado retorna `exit code 126`.

### Prompt unico

```bash
asxrun ask "Explique o padrao repository em C#."
```

### Prompt unico com modelo especifico

```bash
asxrun ask --model qwen2.5-coder:7b "Gerar teste unitario para parser de argumentos."
```

### Modo interativo

```bash
asxrun chat
```

Comandos interativos no chat:

- `/help`
- `/clear`
- `/models`
- `/tools`
- `/exit`

Atalhos uteis no chat:

- `Tab`: autocomplete de comandos, opcoes e nomes de modelos.
- `Ctrl+R`: busca incremental no historico local de prompts.
- `Esc`: cancela a busca incremental ativa.

Para sair do modo interativo: `exit`, `quit` ou `sair`.

### Modelo padrao via variavel de ambiente

PowerShell:

```powershell
$env:ASXRUN_DEFAULT_MODEL = "qwen3.5:4b"
asxrun ask "Resumo do arquivo Program.cs"
```

Bash/Zsh:

```bash
export ASXRUN_DEFAULT_MODEL="qwen3.5:4b"
asxrun ask "Resumo do arquivo Program.cs"
```

## Configuracao Local

Arquivos locais criados automaticamente:

- Windows: `%USERPROFILE%\\.asxrun\\config`, `%USERPROFILE%\\.asxrun\\history`, `%USERPROFILE%\\.asxrun\\mcp-servers.json`, `%USERPROFILE%\\.asxrun\\patch-audit` e `%USERPROFILE%\\.asxrun\\execution-checkpoints`
- Linux/macOS: `~/.asxrun/config`, `~/.asxrun/history`, `~/.asxrun/mcp-servers.json`, `~/.asxrun/patch-audit` e `~/.asxrun/execution-checkpoints`

Chaves suportadas:

- `ollama_host`
- `default_model`
- `prompt_timeout_seconds`
- `healthcheck_timeout_seconds`
- `models_timeout_seconds`
- `theme` (`auto`, `light`, `dark`, `high-contrast`)

Exemplos:

```bash
asxrun config get default_model
asxrun config set default_model qwen3.5:4b
asxrun config set ollama_host http://127.0.0.1:11434/
asxrun config set prompt_timeout_seconds 45
asxrun config set theme high-contrast
```

## Historico

Listar historico:

```bash
asxrun history
```

Limpar historico:

```bash
asxrun history --clear
```

## Retomada de Sessao

Retomar a sessao interrompida mais recente de `ask/skill`:

```bash
asxrun resume
```

Retomar uma sessao especifica:

```bash
asxrun resume <session-id>
```

## MCP

Listar servidores MCP configurados:

```bash
asxrun mcp list
```

Adicionar servidor MCP via `stdio`:

```bash
asxrun mcp add filesystem --command node --arg server.js --cwd . --env NODE_ENV=production
```

Adicionar servidor MCP remoto:

```bash
asxrun mcp add github --url https://mcp.example.com/rpc --transport http --bearer-token <token>
```

Remover servidor MCP:

```bash
asxrun mcp remove filesystem
```

Testar conectividade MCP:

```bash
asxrun mcp test github
```

## Skills

Listar skills:

```bash
asxrun skills
```

Ver detalhes de uma skill:

```bash
asxrun skills show code-review
```

Criar template de arquivo de skill no diretorio atual:

```bash
asxrun skills init
```

Observacao: a descoberta de skills locais usa `<raiz-do-projeto>/.asxrun/skills`.
A raiz do projeto e detectada automaticamente por precedencia:
`monorepo` (`pnpm-workspace.yaml`, `nx.json`, `package.json` com `workspaces`) >
`solution/workspace` (`.sln`, `.slnx`, `.code-workspace`) > `git root` (`.git`).

Executar prompt com skill:

```bash
asxrun skill docs-writer "Escrever guia de onboarding para contribuidores."
```

Formato padrao de arquivo de skill (`SKILL.md`):

```md
---
name: code-review-api
description: Revisa contratos, riscos e casos de erro de APIs.
instruction: |
  Atue como revisor tecnico de APIs.
  Priorize corretude, regressao e testes faltantes.
---
```

Metadados obrigatorios: `name`, `description`, `instruction`.

## Build e Testes

Executar build:

```bash
dotnet build ASXRunTerminal.slnx -c Debug
```

Executar testes:

```bash
dotnet test ASXRunTerminal.slnx -c Debug
```

## Codigos de Saida

- `0`: sucesso
- `1`: erro em tempo de execucao
- `2`: argumentos invalidos
- `130`: execucao cancelada pelo usuario (Ctrl+C)

## Boas Praticas do Projeto

- priorizar mudancas pequenas e seguras;
- sempre adicionar/atualizar testes para comportamentos alterados;
- manter tratamento de erro com mensagens acionaveis;
- para mapeamentos de modelos internos, preferir `implicit operator` em vez de AutoMapper.

## Solucao de Problemas

1. `doctor` falhou com indisponibilidade:
   - verifique se o servico Ollama esta ativo;
   - confirme `ollama_host` no config.
2. comando retornou argumentos invalidos:
   - execute `asxrun --help` e revise a sintaxe.
3. nenhum modelo listado em `models`:
   - rode `ollama pull <modelo>` e tente novamente.
