Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Show-ScriptHelp {
    Write-Host "Uso: ./scripts/run.ps1 [-Configuration <Debug|Release>] [--] [argumentos do asxrun]"
    Write-Host ""
    Write-Host "Exemplos:"
    Write-Host "  ./scripts/run.ps1 --help"
    Write-Host "  ./scripts/run.ps1 ask \"Explique testes\""
    Write-Host "  ./scripts/run.ps1 -Configuration Release -- doctor"
}

$configuration = "Debug"
$cliArgs = [System.Collections.Generic.List[string]]::new()

for ($index = 0; $index -lt $args.Count; $index++) {
    $token = $args[$index]

    switch ($token) {
        "-Configuration" {
            if ($index + 1 -ge $args.Count) {
                throw "O parametro -Configuration exige um valor (Debug ou Release)."
            }

            $candidate = $args[$index + 1]
            if ($candidate -cne "Debug" -and $candidate -cne "Release") {
                throw "Configuracao invalida '$candidate'. Use Debug ou Release."
            }

            $configuration = $candidate
            $index++
            continue
        }
        "-c" {
            if ($index + 1 -ge $args.Count) {
                throw "O parametro -c exige um valor (Debug ou Release)."
            }

            $candidate = $args[$index + 1]
            if ($candidate -cne "Debug" -and $candidate -cne "Release") {
                throw "Configuracao invalida '$candidate'. Use Debug ou Release."
            }

            $configuration = $candidate
            $index++
            continue
        }
        "--help-script" {
            Show-ScriptHelp
            exit 0
        }
        "-HelpScript" {
            Show-ScriptHelp
            exit 0
        }
        "--" {
            for ($forwardIndex = $index + 1; $forwardIndex -lt $args.Count; $forwardIndex++) {
                $cliArgs.Add($args[$forwardIndex])
            }

            break
        }
        default {
            $cliArgs.Add($token)
        }
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
$projectPath = Join-Path (Join-Path $repoRoot "ASXRunTerminal") "ASXRunTerminal.csproj"

if ($cliArgs.Count -eq 0) {
    $cliArgs.Add("--help")
}

$dotnetArgs = @("run", "--project", $projectPath, "-c", $Configuration, "--")
$dotnetArgs += $cliArgs

Write-Host ">> dotnet $($dotnetArgs -join ' ')" -ForegroundColor Cyan
& dotnet @dotnetArgs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}