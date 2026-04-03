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
- `history`: mostra e limpa historico local;
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

- Windows: `%USERPROFILE%\\.asxrun\\config` e `%USERPROFILE%\\.asxrun\\history`
- Linux/macOS: `~/.asxrun/config` e `~/.asxrun/history`

Chaves suportadas:

- `ollama_host`
- `default_model`
- `prompt_timeout_seconds`
- `healthcheck_timeout_seconds`
- `models_timeout_seconds`

Exemplos:

```bash
asxrun config get default_model
asxrun config set default_model qwen3.5:4b
asxrun config set ollama_host http://127.0.0.1:11434/
asxrun config set prompt_timeout_seconds 45
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

## Skills

Listar skills:

```bash
asxrun skills
```

Ver detalhes de uma skill:

```bash
asxrun skills show code-review
```

Executar prompt com skill:

```bash
asxrun skill docs-writer "Escrever guia de onboarding para contribuidores."
```

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
