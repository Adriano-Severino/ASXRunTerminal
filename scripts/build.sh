#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Uso: ./scripts/build.sh [opcoes]

Opcoes:
  -c, --configuration <Debug|Release>  Configuracao de build (padrao: Debug)
      --skip-tests                      Nao executa testes
      --publish                         Publica binario local
  -o, --output <diretorio>              Diretorio de saida do publish (padrao: dist)
  -h, --help                            Exibe esta ajuda
EOF
}

configuration="Debug"
skip_tests="false"
publish="false"
output_dir="dist"

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
    --skip-tests)
      skip_tests="true"
      shift
      ;;
    --publish)
      publish="true"
      shift
      ;;
    -o|--output)
      if [[ $# -lt 2 ]]; then
        echo "Erro: valor ausente para $1." >&2
        usage
        exit 1
      fi
      output_dir="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Erro: opcao invalida '$1'." >&2
      usage
      exit 1
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

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
solution_path="${repo_root}/ASXRunTerminal.slnx"
project_path="${repo_root}/ASXRunTerminal/ASXRunTerminal.csproj"

run_dotnet() {
  echo ">> dotnet $*"
  dotnet "$@"
}

run_dotnet restore "${solution_path}"
run_dotnet build "${solution_path}" -c "${configuration}" --no-restore

if [[ "${skip_tests}" != "true" ]]; then
  run_dotnet test "${solution_path}" -c "${configuration}" --no-build
fi

if [[ "${publish}" == "true" ]]; then
  publish_output_path="${repo_root}/${output_dir}"
  run_dotnet publish "${project_path}" -c "${configuration}" -o "${publish_output_path}" --no-build
  echo "Publicado em: ${publish_output_path}"
fi

echo "Build local finalizado com sucesso."