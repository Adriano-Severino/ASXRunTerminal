# Guia de Seguranca Operacional (Uso Local e Corporativo)

## Objetivo
Definir um baseline de seguranca para operar o ASXRunTerminal em ambiente local (desenvolvedor individual) e corporativo (times com governanca), reduzindo risco de:

- exposicao de segredos;
- execucao de comandos destrutivos;
- alteracoes indevidas no workspace;
- uso inseguro de integracoes MCP remotas.

## Escopo
Este guia cobre os fluxos com maior impacto de seguranca no estado atual do produto (`0.1.x`):

- `ask`, `chat`, `skill`, `resume`;
- `patch` e politicas de permissao por workspace;
- tools de shell (`powershell`, `bash`, `zsh`);
- catalogo e teste de servidores MCP (`mcp add/list/remove/test`);
- persistencia local em `~/.asxrun`.

## Superficie de Risco
1. Prompt e resposta podem conter segredos, caminhos internos e dados sensiveis.
2. Tools de shell podem executar comandos com impacto destrutivo no host.
3. `patch` pode alterar ou remover arquivos criticos sem governanca adequada.
4. MCP remoto pode introduzir exfiltracao de dados se endpoint/autenticacao nao forem controlados.
5. Arquivos locais de operacao podem conter trilhas com dados confidenciais.

## Controles de Seguranca Ja Implementados
1. Guardrails de arquivo por workspace com `.asxrun/workspace-permissions.json`:
   - suporte a `defaultMode` (`allow` ou `deny`);
   - regras por operacao (`read`, `create`, `edit`, `copy`, `move`, `delete`);
   - `deny` tem precedencia sobre `allow`.
2. Guardrails de shell por workspace com `.asxrun/shell-command-policy.json`:
   - blocklist default para comandos de alto risco (ex.: `rm`, `remove-item`, `shutdown`, `diskpart`, `dd`);
   - comandos desbloqueados via `allow` exigem aprovacao explicita por execucao com `destructive_approval=sim`;
   - bloqueio retorna `exit code 126`.
3. Mascaramento de segredos em logs e saidas (`SecretMasker`):
   - cobre tokens, chaves de API, cabecalhos `Authorization`, JWT, userinfo em URL e blocos de chave privada.
4. Trilha de auditoria local para `patch` em `~/.asxrun/patch-audit`:
   - registra sessao, contadores de mudanca e diff unificado.
5. Checkpoints de execucao em `~/.asxrun/execution-checkpoints`:
   - rastreia comando, etapa e status (`in-progress`, `completed`, `failed`, `cancelled`).

## Baseline Recomendado - Uso Local
1. Executar o CLI em conta de usuario sem privilegios administrativos.
2. Manter host atualizado (SO, shell e runtime .NET) e com protecao de endpoint ativa.
3. Evitar passar segredo direto em linha de comando sempre que possivel:
   - comandos podem ficar no historico do shell;
   - prefira variaveis de ambiente temporarias e tokens de curta duracao.
4. Revisar sempre o diff com `asxrun patch --dry-run <arquivo>` antes de aplicar mudancas reais.
5. Limitar escopo de escrita com politica de workspace quando trabalhar em repositorios sensiveis.
6. Tratar `~/.asxrun` como dado sensivel local (nao sincronizar para locais publicos).

## Baseline Recomendado - Uso Corporativo
1. Padronizar bootstrap de projeto com politicas versionadas em `<workspace>/.asxrun/`:
   - `workspace-permissions.json` com `defaultMode=deny`;
   - `shell-command-policy.json` com overrides minimos e justificados.
2. Definir allowlist de endpoints MCP remotos e exigir `https`.
3. Usar credenciais de curta duracao para MCP remoto e processo formal de rotacao/revogacao.
4. Separar ambientes (dev/staging/prod) com tokens e catalogos MCP distintos.
5. Coletar e auditar periodicamente `patch-audit` e `execution-checkpoints` para rastreabilidade.
6. Integrar revisao de politica de seguranca em onboarding tecnico e revisoes de mudanca.

## Templates de Politica
### 1) Workspace permissions (modo restritivo)
Arquivo: `<workspace>/.asxrun/workspace-permissions.json`

```json
{
  "defaultMode": "deny",
  "read": {
    "allow": ["src/**", "tests/**", "docs/**", "*.sln", "*.slnx", "*.md"]
  },
  "create": {
    "allow": ["src/**", "tests/**", "docs/**"]
  },
  "edit": {
    "allow": ["src/**", "tests/**", "docs/**"]
  },
  "copy": {
    "allow": ["src/**", "tests/**", "docs/**"]
  },
  "move": {
    "allow": ["src/**", "tests/**", "docs/**"]
  },
  "delete": {
    "allow": ["src/**", "tests/**", "docs/**"],
    "deny": ["**/secrets/**", "**/*.pem", "**/*.pfx", "**/.env*"]
  }
}
```

### 2) Shell command policy (minimo necessario)
Arquivo: `<workspace>/.asxrun/shell-command-policy.json`

```json
{
  "allow": [],
  "deny": ["curl", "wget"]
}
```

Observacao: `allow` deve ser usado com parcimonia para comandos destrutivos bloqueados por default.

## Rotina Operacional Recomendada
1. Inicio de sessao:
   - `asxrun doctor`
   - `asxrun mcp test <servidor>`
2. Durante execucao:
   - usar `patch --dry-run` para previsao de mudancas;
   - validar diff e escopo antes de aplicar;
   - evitar prompts com dados pessoais ou segredos desnecessarios.
3. Fechamento:
   - revisar falhas/bloqueios recorrentes;
   - ajustar politicas de workspace quando necessario;
   - registrar excecoes aprovadas no processo interno.

## Resposta a Incidentes (Resumo)
1. Vazamento de credencial:
   - revogar token/segredo imediatamente;
   - atualizar catalogo MCP e credenciais associadas;
   - revisar historico/auditoria para medir escopo.
2. Tentativa de comando destrutivo:
   - identificar origem e contexto da execucao;
   - reforcar `shell-command-policy.json` e processos de aprovacao;
   - validar integridade dos arquivos afetados via diff e controle de versao.
3. Mudanca indevida em arquivo sensivel:
   - interromper execucao automatizada;
   - restaurar estado via VCS e investigar causa raiz;
   - endurecer `workspace-permissions.json` para o caminho afetado.

## Checklist de Adoacao
### Local
- [ ] Politicas de workspace habilitadas para repositorios criticos.
- [ ] Revisao de `patch --dry-run` antes de aplicacao.
- [ ] Credenciais fora do historico de shell.
- [ ] Diretorio `~/.asxrun` tratado como sensivel.

### Corporativo
- [ ] Baseline de politicas versionado e revisado por seguranca.
- [ ] Endpoints MCP remotos com allowlist e `https`.
- [ ] Rotacao de token definida e automatizada.
- [ ] Auditoria periodica de `patch-audit` e checkpoints.
- [ ] Processo de incidente documentado e testado.

## Referencias de Codigo
- `ASXRunTerminal/core/SecretMasker.cs`
- `ASXRunTerminal/core/ShellCommandPermissionPolicy.cs`
- `ASXRunTerminal/core/WorkspaceFilePermissionPolicy.cs`
- `ASXRunTerminal/config/ShellCommandPermissionPolicyFile.cs`
- `ASXRunTerminal/config/WorkspacePermissionPolicyFile.cs`
- `ASXRunTerminal/config/WorkspacePatchAuditFile.cs`
- `ASXRunTerminal/config/ExecutionCheckpointFile.cs`
