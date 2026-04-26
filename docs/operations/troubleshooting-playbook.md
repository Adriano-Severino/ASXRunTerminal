# Playbook de Troubleshooting (MCP, shell, Ollama, permissoes, encoding)

## Objetivo
Fornecer um guia operacional para diagnosticar e corrigir falhas recorrentes no ASXRunTerminal com foco em:

- conectividade e handshake MCP;
- execucao de shell local;
- disponibilidade do Ollama;
- bloqueios de permissoes por politicas;
- problemas de encoding em arquivos e terminal.

## Escopo
Este playbook cobre os fluxos e artefatos atuais em `v0.1.x`:

- comandos `doctor`, `models`, `ask`, `chat`, `patch`, `mcp list/add/remove/test`;
- politicas em `<workspace>/.asxrun/workspace-permissions.json` e `<workspace>/.asxrun/shell-command-policy.json`;
- catalogo MCP em `~/.asxrun/mcp-servers.json`;
- config de usuario em `~/.asxrun/config`.

## Triagem Rapida (5 minutos)
1. Validar versao e comandos disponiveis:

```bash
asxrun --version
asxrun --help
```

2. Validar conectividade Ollama:

```bash
asxrun doctor
asxrun models
```

3. Validar configuracao efetiva:

```bash
asxrun config get ollama_host
asxrun config get default_model
asxrun config get prompt_timeout_seconds
asxrun config get healthcheck_timeout_seconds
asxrun config get models_timeout_seconds
```

4. Validar servidores MCP cadastrados e handshake:

```bash
asxrun mcp list
asxrun mcp test <nome-servidor>
```

5. Validar raiz de workspace (impacta politicas e patch):

```bash
asxrun context
```

## 1) MCP

### Sintoma: `mcp list` ou `mcp test` falha ao carregar catalogo
Causas provaveis:
- JSON invalido em `~/.asxrun/mcp-servers.json`;
- campos obrigatorios ausentes ou com formato invalido;
- nomes de servidor duplicados.

Diagnostico:

```bash
asxrun mcp list
```

Se houver erro, revisar o arquivo:

- Windows: `%USERPROFILE%\.asxrun\mcp-servers.json`
- Linux/macOS: `~/.asxrun/mcp-servers.json`

Correcao:
1. Corrigir JSON invalido, campos nulos/vazios ou valores fora do formato.
2. Confirmar que cada servidor possui `name` unico.
3. Recriar entradas com `asxrun mcp add ...` quando necessario.

Validacao:

```bash
asxrun mcp list
```

### Sintoma: `mcp test <nome>` falha em servidor `stdio`
Causas provaveis:
- comando do servidor nao existe no ambiente (`node`, binario custom etc.);
- `--cwd` aponta para diretorio inexistente;
- variaveis `--env` ausentes/incorretas;
- servidor nao responde o protocolo MCP esperado.

Diagnostico:
1. Inspecionar configuracao salva:

```bash
asxrun mcp list
```

2. Verificar comando no host:

```powershell
Get-Command node
```

```bash
command -v node
```

3. Testar handshake novamente:

```bash
asxrun mcp test <nome-servidor>
```

Correcao:
1. Atualizar comando/args/cwd/env com `mcp remove` + `mcp add`.
2. Ajustar servidor para responder `initialize`, `notifications/initialized` e `tools/list`.

Validacao:

```bash
asxrun mcp test <nome-servidor>
```

### Sintoma: `mcp test <nome>` falha em servidor remoto (`http`/`sse`)
Causas provaveis:
- URL ou transporte incorretos;
- erro de autenticacao (token/header);
- timeout de rede;
- payload JSON-RPC invalido retornado pelo servidor.

Diagnostico:
1. Confirmar endpoint/transporte/auth:

```bash
asxrun mcp list
```

2. Validar endpoint externamente (curl/http client interno).
3. Repetir teste MCP:

```bash
asxrun mcp test <nome-servidor>
```

Correcao:
1. Ajustar `--url`, `--transport`, `--message-url`, `--bearer-token` ou `--header`.
2. Garantir que o servidor retorna JSON-RPC valido.
3. Em SSE, garantir emissao correta do endpoint de mensagens quando aplicavel.

Validacao:

```bash
asxrun mcp test <nome-servidor>
```

## 2) Shell

### Sintoma: comando de shell bloqueado
Causas provaveis:
- comando em blocklist default (ex.: `rm`, `remove-item`, `shutdown`, `diskpart`, `dd`);
- comando nao permitido na politica local;
- comando destrutivo liberado em `allow` sem aprovacao explicita.

Diagnostico:
1. Revisar politica do workspace:
- `<workspace>/.asxrun/shell-command-policy.json`
2. Confirmar se comando aparece em `deny` ou blocklist default.

Correcao:
1. Para desbloquear explicitamente, incluir o comando em `allow`.
2. Para comando destrutivo liberado, informar aprovacao explicita no argumento da tool call:
- `destructive_approval=sim`

Exemplo minimo:

```json
{
  "allow": ["rm"],
  "deny": ["curl"]
}
```

Observacoes:
- comando bloqueado por guardrail retorna `exit code 126`;
- arquivo de politica invalido interrompe avaliacao com erro de runtime.

### Sintoma: shell indisponivel no ambiente
Causas provaveis:
- ferramenta ausente no host (`powershell`, `bash`, `zsh`);
- plataforma nao suportada para o provider selecionado.

Diagnostico:
- no chat, use `/tools` para listar ferramentas registradas;
- no host, valide instalacao do shell:

```powershell
Get-Command powershell
```

```bash
command -v bash
command -v zsh
```

Correcao:
1. Instalar shell ausente ou ajustar imagem/ambiente.
2. Em Linux/macOS, prefira `bash`/`zsh`; em Windows, `powershell`.

Validacao:
- executar novamente o fluxo que usa tool de shell;
- confirmar ausencia de erro `exit code 127`.

## 3) Ollama

### Sintoma: `doctor` retorna indisponivel
Causas provaveis:
- servico Ollama parado;
- `ollama_host` invalido/inalcancavel;
- timeout configurado muito baixo.

Diagnostico:

```bash
asxrun doctor
asxrun config get ollama_host
asxrun config get healthcheck_timeout_seconds
```

Correcao:
1. Iniciar/reiniciar Ollama no host.
2. Corrigir host:

```bash
asxrun config set ollama_host http://127.0.0.1:11434/
```

3. Ajustar timeout quando necessario:

```bash
asxrun config set healthcheck_timeout_seconds 5
```

Validacao:

```bash
asxrun doctor
```

### Sintoma: `models` vazio ou falhando
Causas provaveis:
- nenhum modelo local baixado;
- erro de conectividade;
- timeout de listagem.

Diagnostico:

```bash
asxrun models
asxrun config get models_timeout_seconds
```

Correcao:
1. Baixar modelo no Ollama:

```bash
ollama pull qwen3.5:4b
```

2. Ajustar timeout:

```bash
asxrun config set models_timeout_seconds 10
```

Validacao:

```bash
asxrun models
```

### Sintoma: `ask/chat` com erro de modelo nao encontrado
Causas provaveis:
- modelo informado por `--model` nao existe localmente;
- `default_model` aponta para modelo ausente.

Diagnostico:

```bash
asxrun models
asxrun config get default_model
```

Correcao:
1. Ajustar `default_model` para um modelo existente:

```bash
asxrun config set default_model qwen3.5:4b
```

2. Ou baixar o modelo faltante no Ollama.

Observacao:
- o CLI tenta fallback automatico entre modelo solicitado, padrao e modelos locais disponiveis quando detecta indisponibilidade.

## 4) Permissoes

### Sintoma: `patch` falha com bloqueio de operacao
Causas provaveis:
- regra `deny` ativa para o caminho;
- `defaultMode=deny` sem `allow` para a operacao;
- caminho fora do escopo esperado do workspace.

Diagnostico:
1. Confirmar raiz de workspace:

```bash
asxrun context
```

2. Revisar politica:
- `<workspace>/.asxrun/workspace-permissions.json`
3. Simular antes de aplicar:

```bash
asxrun patch --dry-run patch.json
```

Correcao:
1. Ajustar `allow` da operacao (`read`, `create`, `edit`, `copy`, `move`, `delete`).
2. Manter `deny` apenas para bloqueios realmente intencionais.
3. Garantir que padroes usam paths relativos ao workspace.

Exemplo:

```json
{
  "defaultMode": "deny",
  "edit": {
    "allow": ["src/**", "docs/**"]
  },
  "delete": {
    "allow": ["src/**"],
    "deny": ["src/secrets/**"]
  }
}
```

### Sintoma: erro ao carregar politicas de permissao
Causas provaveis:
- JSON invalido;
- arquivo vazio;
- diretorio workspace nao resolvido;
- sem permissao de leitura no arquivo.

Diagnostico:
1. Revisar JSON e caminho dos arquivos:
- `<workspace>/.asxrun/workspace-permissions.json`
- `<workspace>/.asxrun/shell-command-policy.json`
2. Validar permissao de leitura no SO.

Correcao:
1. Corrigir formato JSON.
2. Remover arquivo invalido para voltar ao default (quando apropriado).
3. Recriar com template minimo valido.

Validacao:

```bash
asxrun context
asxrun patch --dry-run patch.json
```

## 5) Encoding

### Sintoma: caracteres quebrados (`�`) no terminal
Causas provaveis:
- terminal fora de UTF-8;
- arquivo de entrada salvo em code page local (ex.: Windows-1252) sem BOM.

Diagnostico:
1. Reproduzir com `asxrun --help` e `asxrun doctor`.
2. Conferir encoding do terminal.

Correcao (Windows PowerShell):

```powershell
chcp 65001
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
```

Correcao (arquivos JSON/MD):
1. Salvar arquivos de config/politica/patch em UTF-8.
2. Regravar arquivo em UTF-8 quando necessario:

```powershell
Get-Content .\arquivo.json -Raw | Set-Content .\arquivo.json -Encoding utf8
```

```bash
iconv -f WINDOWS-1252 -t UTF-8 arquivo.json > arquivo.utf8.json && mv arquivo.utf8.json arquivo.json
```

### Sintoma: erro JSON inesperado em arquivos aparentemente corretos
Causas provaveis:
- aspas tipograficas ou caracteres invisiveis copiados de editor externo;
- encoding inconsistente com o parser.

Diagnostico:
1. Reabrir arquivo em editor com visualizacao de encoding.
2. Substituir aspas tipograficas por aspas simples/duplas ASCII.
3. Validar JSON com formatter/linter local.

Correcao:
1. Reescrever o arquivo com UTF-8 e aspas ASCII (`"`).
2. Executar novamente o comando que consumia o arquivo.

## Checklist de Fechamento
- [ ] `asxrun doctor` retorna Ollama disponivel.
- [ ] `asxrun models` lista pelo menos 1 modelo.
- [ ] `asxrun mcp list` lista catalogo sem erro.
- [ ] `asxrun mcp test <nome>` completa handshake.
- [ ] `asxrun patch --dry-run patch.json` executa sem bloqueio inesperado.
- [ ] saida de terminal e arquivos JSON/MD sem sintomas de encoding.

## Referencias de Codigo
- `ASXRunTerminal/Program.cs`
- `ASXRunTerminal/infra/McpStdioClient.cs`
- `ASXRunTerminal/infra/McpRemoteClient.cs`
- `ASXRunTerminal/infra/McpToolDiscovery.cs`
- `ASXRunTerminal/infra/PowerShellToolProvider.cs`
- `ASXRunTerminal/infra/UnixShellToolProvider.cs`
- `ASXRunTerminal/infra/ShellCommandGuardrailEvaluator.cs`
- `ASXRunTerminal/core/ShellCommandPermissionPolicy.cs`
- `ASXRunTerminal/config/ShellCommandPermissionPolicyFile.cs`
- `ASXRunTerminal/core/WorkspaceFilePermissionPolicy.cs`
- `ASXRunTerminal/config/WorkspacePermissionPolicyFile.cs`
- `ASXRunTerminal/infra/OllamaHttpClient.cs`
- `ASXRunTerminal/config/UserConfigFile.cs`
- `ASXRunTerminal/config/McpServerCatalogFile.cs`
