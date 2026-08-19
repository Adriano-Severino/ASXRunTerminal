# Backlog MVP - ASXRunTerminal CLI

## Objetivo Funcional Minimo

Entregar um CLI que rode no terminal, aceite prompts, execute modelos locais via Ollama e retorne respostas de forma confiavel, com configuracao basica de modelo e comandos essenciais.

## Estrutura: Epic -> Feature -> Task

## EPIC 1 - Fundacao do CLI

### FEATURE 1.1 - Inicializacao e comando base

- [x] TASK 1.1.1: Criar projeto CLI com ponto de entrada (`asxrun`).
- [x] TASK 1.1.2: Implementar parser de argumentos (`--help`, `--version`).
- [x] TASK 1.1.3: Definir estrutura de pastas (commands, core, infra, config).
- [x] TASK 1.1.4: Adicionar logs basicos e codigos de saida padrao.

### FEATURE 1.2 - UX minima de terminal

- [x] TASK 1.2.1: Implementar modo comando unico (`asxrun ask "prompt"`).
- [x] TASK 1.2.2: Implementar modo interativo (`asxrun chat`).
- [x] TASK 1.2.3: Exibir estados de execucao (conectando, processando, concluido, erro).
- [x] TASK 1.2.4: Padronizar mensagens de erro amigaveis.

## EPIC 2 - Integracao com Ollama

### FEATURE 2.1 - Conexao e healthcheck

- [ ] TASK 2.1.1: Implementar cliente HTTP para API local do Ollama.
- [ ] TASK 2.1.2: Criar comando `asxrun doctor` para validar disponibilidade do Ollama.
- [ ] TASK 2.1.3: Validar timeout, retry curto e tratamento de indisponibilidade.
- [ ] TASK 2.1.4: Refatorar integracao com Ollama para usar `OllamaSharp` como cliente principal e `Microsoft.Extensions.AI` como camada de abstracao multi-provider.

### FEATURE 2.2 - Execucao de prompts

- [x] TASK 2.2.1: Enviar prompt simples para endpoint de geracao.
- [x] TASK 2.2.2: Suportar streaming de resposta no terminal.
- [ ] TASK 2.2.3: Implementar cancelamento por `Ctrl+C` sem travar o processo.
- [x] TASK 2.2.4: Tratar respostas parciais e erros de parsing.

### FEATURE 2.3 - Modelos

- [x] TASK 2.3.1: Criar comando `asxrun models` para listar modelos locais.
- [x] TASK 2.3.2: Permitir selecao de modelo por flag (`--model`).
- [x] TASK 2.3.3: Definir modelo padrao `qwen3.5:4b` configuravel.

## EPIC 3 - Configuracao e persistencia minima

### FEATURE 3.1 - Arquivo de configuracao local

- [x] TASK 3.1.1: Criar arquivo de config do usuario (`~/.asxrun/config`).
- [x] TASK 3.1.2: Persistir host do Ollama, modelo padrao e parametros basicos.
- [x] TASK 3.1.3: Implementar comando `asxrun config set/get`.

### FEATURE 3.2 - Historico basico de sessoes

- [x] TASK 3.2.1: Salvar ultimos prompts e respostas em arquivo local.
- [x] TASK 3.2.2: Implementar comando `asxrun history`.
- [x] TASK 3.2.3: Adicionar opcao para limpar historico (`asxrun history --clear`).

## EPIC 4 - Qualidade minima para uso real

### FEATURE 4.1 - Testes essenciais

- [x] TASK 4.1.1: Criar testes unitarios para parser de comandos e config.
- [x] TASK 4.1.2: Criar testes de integracao do cliente Ollama com mocks.
- [x] TASK 4.1.3: Criar smoke test de fluxo completo (`ask` e `chat`).

### FEATURE 4.2 - Entrega e documentacao

- [x] TASK 4.2.1: Escrever README com instalacao, uso e exemplos.
- [x] TASK 4.2.2: Definir script de build e execucao local.
- [x] TASK 4.2.3: Adicionar changelog inicial e versao `0.1.0`.

## EPIC 5 - UX avancada de terminal (estilo Copilot CLI)

### FEATURE 5.1 - Interface visual rica no terminal

- [x] TASK 5.1.1: Definir design system de terminal (cores, destaque, status e tipografia monospace).
- [x] TASK 5.1.2: Implementar renderizador ANSI com fallback para terminais sem suporte a cor.
- [x] TASK 5.1.3: Criar componentes visuais reutilizaveis (header, badges de estado, spinner e separadores).
- [x] TASK 5.1.4: Melhorar renderizacao de resposta com blocos de codigo destacados por linguagem.
- [x] TASK 5.1.5: Adicionar tema configuravel (`auto`, `light`, `dark`, `high-contrast`) via config.

### FEATURE 5.2 - Experiencia interativa

- [x] TASK 5.2.1: Adicionar comandos interativos (`/help`, `/clear`, `/models`, `/tools`, `/exit`).
- [x] TASK 5.2.2: Implementar historico navegavel com setas e busca incremental no modo `chat`.
- [x] TASK 5.2.3: Adicionar autocomplete de comandos, opcoes e nomes de modelos.
- [x] TASK 5.2.4: Exibir progresso de execucao de forma mais legivel (conectando, tool call, diff, concluido).

## EPIC 6 - Ferramentas locais e integracao MCP

### FEATURE 6.1 - Runtime unificado de ferramentas

- [x] TASK 6.1.1: Criar camada `ToolRuntime` com contrato unico para executar ferramentas internas e externas.
- [x] TASK 6.1.2: Implementar adaptador de shell para PowerShell (Windows).
- [x] TASK 6.1.3: Implementar adaptador de shell para Bash/Zsh (Linux/macOS).
- [x] TASK 6.1.4: Implementar deteccao de ambiente para selecionar shell padrao por plataforma.
- [x] TASK 6.1.5: Padronizar captura de `stdout`, `stderr`, `exit code`, timeout e cancelamento em tool calls.

### FEATURE 6.2 - Suporte MCP

- [x] TASK 6.2.1: Implementar cliente MCP com transporte `stdio`.
- [x] TASK 6.2.2: Implementar suporte a servidores MCP remotos (`sse`/`http`) com autenticacao.
- [x] TASK 6.2.3: Criar comandos `asxrun mcp list`, `asxrun mcp add`, `asxrun mcp remove`, `asxrun mcp test`.
- [x] TASK 6.2.4: Implementar descoberta de ferramentas MCP e validacao de schema de parametros.
- [ ] TASK 6.2.5: Habilitar invocacao de ferramentas MCP durante `ask/chat/agents` com logs de auditoria.

### FEATURE 6.3 - Skills reutilizaveis

- [x] TASK 6.3.1: Implementar comando `asxrun skills` para listar skills disponiveis.
- [x] TASK 6.3.2: Implementar comando `asxrun skills show <nome>` para exibir detalhes da skill.
- [x] TASK 6.3.3: Implementar comando `asxrun skill <nome> [--model <modelo>] "prompt"` para executar prompt com contexto da skill.
- [x] TASK 6.3.4: Criar skills padrao iniciais (`code-review`, `bugfix`, `refactor`, `test-writer`, `docs-writer`).

### FEATURE 6.4 - Skills carregadas por arquivos de diretorio

- [x] TASK 6.4.1: Definir diretorios de descoberta de skills (`./.asxrun/skills` e `~/.asxrun/skills`).
- [x] TASK 6.4.2: Criar formato padrao de arquivo de skill (`SKILL.md` com metadados obrigatorios: `name`, `description`, `instruction`).
- [x] TASK 6.4.3: Implementar leitor de skills em diretorio com busca recursiva e filtro por extensao suportada.
- [x] TASK 6.4.4: Implementar validacao de schema e mensagens de erro amigaveis para arquivo de skill invalido.
- [x] TASK 6.4.5: Implementar precedencia de resolucao (skill local do projeto > skill do usuario > skill built-in).
- [x] TASK 6.4.6: Adicionar comando `asxrun skills init` para criar template de arquivo de skill no diretorio atual.
- [x] TASK 6.4.7: Adicionar comando `asxrun skills reload` para recarregar cache de skills sem reiniciar o CLI.
- [x] TASK 6.4.8: Criar testes unitarios e de integracao para leitura de skills por arquivo e regras de precedencia.

## EPIC 7 - Contexto inteligente de workspace e operacoes de arquivos

### FEATURE 7.1 - Compreensao de contexto de diretorios

- [x] TASK 7.1.1: Detectar automaticamente raiz de projeto (Git root, solution/workspace, monorepo).
- [x] TASK 7.1.2: Mapear estrutura de arquivos respeitando `.gitignore` e limites de performance.
- [x] TASK 7.1.3: Implementar indexacao incremental de arquivos para consultas rapidas de contexto.
- [x] TASK 7.1.4: Expor comando `asxrun context` para inspecionar resumo do workspace atual.

### FEATURE 7.2 - Edicao segura de arquivos com diff

- [x] TASK 7.2.1: Implementar operacoes de arquivo (`read`, `create`, `edit`, `copy`, `move`, `delete`) com validacoes.
- [x] TASK 7.2.2: Implementar engine de patch/diff unificado para aplicar mudancas com previsao.
- [x] TASK 7.2.3: Adicionar modo `--dry-run` para mostrar diff sem alterar arquivos.
- [x] TASK 7.2.4: Exigir confirmacao para operacoes destrutivas (delete/move recursivo).
- [x] TASK 7.2.5: Criar trilha de auditoria local com historico de mudancas por sessao.

## EPIC 8 - Seguranca, confiabilidade e governanca de execucao

### FEATURE 8.1 - Guardrails de execucao

- [x] TASK 8.1.1: Implementar politicas de permissao por workspace para operacoes de arquivo.
- [x] TASK 8.1.2: Implementar allowlist/blocklist de comandos de shell de alto risco.
- [x] TASK 8.1.3: Bloquear por padrao comandos destrutivos sem aprovacao explicita.
- [x] TASK 8.1.4: Implementar mascaramento de segredos em logs e saida de ferramentas.

### FEATURE 8.2 - Resiliencia operacional

- [x] TASK 8.2.1: Implementar retries e circuit breaker para chamadas MCP e Ollama.
- [x] TASK 8.2.2: Implementar checkpoints por etapa para retomar sessoes interrompidas.
- [x] TASK 8.2.3: Adicionar fallback de modelo e fallback de ferramenta quando houver indisponibilidade.

## EPIC 9 - Qualidade para escala e paridade de produto

### FEATURE 9.1 - Testes avancados

- [x] TASK 9.1.1: Criar testes de contrato para servidores MCP e ferramentas externas.
- [x] TASK 9.1.2: Criar testes de integracao cross-platform (PowerShell e Bash) em pipeline CI.
- [x] TASK 9.1.3: Criar testes de regressao para engine de diff e operacoes destrutivas.
- [x] TASK 9.1.4: Criar snapshot tests para interface ANSI e temas de terminal.

### FEATURE 9.2 - Documentacao e operacao

- [x] TASK 9.2.1: Documentar arquitetura de plugins/ferramentas e fluxo de contexto.
- [x] TASK 9.2.2: Criar guia de seguranca operacional para uso local e corporativo.
- [x] TASK 9.2.3: Publicar playbook de troubleshooting (MCP, shell, Ollama, permissoes, encoding).

## EPIC 10 - Modo agente autonomo (estilo desenvolvedor senior)

### FEATURE 10.1 - Planejamento e execucao autonoma

- [x] TASK 10.1.1: Implementar comando `asxrun agent` para iniciar modo autonomo por objetivo.
- [x] TASK 10.1.2: Implementar decomposicao automatica de objetivo em plano de execucao por etapas.
- [x] TASK 10.1.3: Implementar loop autonomo `plan -> execute -> verify -> refine` ate concluir.
- [x] TASK 10.1.4: Implementar controle de orcamento (`max_steps`, `max_time`, `max_cost`) por sessao.
- [x] TASK 10.1.5: Permitir retomada de execucao autonoma a partir de checkpoint.

### FEATURE 10.2 - Capacidade de engenharia end-to-end

- [x] TASK 10.2.1: Habilitar leitura do contexto do projeto (codigo, testes, docs, historico git) antes de agir.
- [x] TASK 10.2.2: Implementar alteracao de codigo com geracao de diff e justificativa tecnica por mudanca.
- [x] TASK 10.2.3: Executar comandos de validacao automaticamente (`build`, `test`, `lint`) apos cada bloco de mudancas.
- [x] TASK 10.2.4: Implementar auto-correcao quando testes falharem, com limite de tentativas.
- [x] TASK 10.2.5: Gerar resumo final de entrega (arquivos alterados, riscos, validacoes e proximos passos).

### FEATURE 10.3 - Governanca e seguranca do modo autonomo

- [x] TASK 10.3.1: Definir niveis de autonomia (`assistido`, `semi-autonomo`, `autonomo`) configuraveis por projeto.
- [x] TASK 10.3.2: Exigir aprovacao explicita para operacoes destrutivas e comandos sensiveis.
- [x] TASK 10.3.3: Implementar trilha de auditoria detalhada de decisoes, comandos e alteracoes aplicadas.
- [x] TASK 10.3.4: Implementar rollback automatico para ultimo estado estavel quando uma execucao degradar o projeto.

### FEATURE 10.4 - Qualidade e criterios de "dev senior"

- [x] TASK 10.4.1: Criar rubric de qualidade tecnica (corretude, legibilidade, testes, seguranca, performance).
- [x] TASK 10.4.2: Implementar auto-review da propria mudanca antes de finalizar a tarefa.
- [x] TASK 10.4.3: Bloquear conclusao quando cobertura minima de testes nao for atendida.
- [x] TASK 10.4.4: Criar benchmark de sucesso para medir taxa de tarefas concluidas sem intervencao humana.

## Definicao de pronto do MVP

- [ ] O comando `asxrun ask "texto"` retorna resposta via Ollama.
- [ ] O comando `asxrun chat` funciona em modo interativo com streaming.
- [ ] O usuario consegue listar modelos e selecionar modelo padrao.
- [ ] Existe configuracao local funcional e comando `doctor`.
- [ ] Existem testes minimos cobrindo fluxo principal e casos de erro comuns.
