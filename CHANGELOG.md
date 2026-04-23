# Changelog

Todas as mudancas relevantes deste projeto serao documentadas neste arquivo.

O formato segue o Keep a Changelog e o versionamento segue Semantic Versioning.

## [Unreleased]

### Adicionado
- Suporte a tema configuravel via `config` com chave `theme` e valores `auto`, `light`, `dark` e `high-contrast`.
- Comandos interativos no modo `chat`: `/help`, `/clear`, `/models`, `/tools` e `/exit`.
- Guardrails por workspace para operacoes de arquivo via `.asxrun/workspace-permissions.json`, aplicados no comando `patch` (incluindo `--dry-run`).
- Camada de resiliencia operacional com retries e circuit breaker para fluxos de Ollama (`ask/chat`, `doctor`, `models`) e `mcp test`.

## [0.1.0] - 2026-04-03

### Adicionado
- CLI base com parser de argumentos (`--help`, `--version`) e codigos de saida padrao.
- Comandos principais: `ask`, `chat`, `doctor`, `models`, `history`, `config`, `skills` e `skill`.
- Integracao local com Ollama para prompts e listagem de modelos.
- Persistencia local de configuracao e historico do usuario.
- Suite de testes unitarios, integracao com mocks e smoke tests de fluxo.
- Scripts de build e execucao local para Windows e Linux/macOS em `scripts/`.

### Alterado
- Versao inicial do produto definida para `0.1.0`.
