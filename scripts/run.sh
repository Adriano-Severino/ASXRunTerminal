#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Uso: ./scripts/run.sh [opcoes-do-script] [--] [argumentos-do-asxrun]

Opcoes do script:
  -c, --configuration <Debug|Release>  Configuracao de execucao (padrao: Debug)
  -h, --help-script                    Exibe esta ajuda do script

Exemplos:
  ./scripts/run.sh -- doctor
  ./scripts/run.sh -- ask "Explique testes de integracao"
  ./scripts/run.sh -c Release -- models
EOF
}

configuration="Debug"
cli_args=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      if [[ $# -lt 2 ]]; then
        echo "Erro: valor ausente para $1." >&2
        usage
        exit 1
      fi
      configuration="$2"
      shift 2
      ;;
    -h|--help-script)
      usage
      exit 0
      ;;
    --)
      shift
      while [[ $# -gt 0 ]]; do
        cli_args+=("$1")
        shift
      done
      ;;
    *)
      cli_args+=("$1")
      shift
      ;;
  esac
done

case "$configuration" in
  Debug|Release) ;;
  *)
    echo "Erro: configuracao invalida '$configuration'. Use Debug ou Release." >&2
    exit 1
    ;;
esac

if [[ ${#cli_args[@]} -eq 0 ]]; then
  cli_args=("--help")
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
project_path="${repo_root}/ASXRunTerminal/ASXRunTerminal.csproj"

echo ">> dotnet run --project ${project_path} -c ${configuration} -- ${cli_args[*]}"
dotnet run --project "${project_path}" -c "${configuration}" -- "${cli_args[@]}"