[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$SkipTests,
    [switch]$Publish,
    [string]$OutputDir = "dist"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
$solutionPath = Join-Path $repoRoot "ASXRunTerminal.slnx"
$projectPath = Join-Path (Join-Path $repoRoot "ASXRunTerminal") "ASXRunTerminal.csproj"

function Invoke-DotNet {
    param([string[]]$Arguments)

    Write-Host ">> dotnet $($Arguments -join ' ')" -ForegroundColor Cyan
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet @("restore", $solutionPath)
Invoke-DotNet @("build", $solutionPath, "-c", $Configuration, "--no-restore")

if (-not $SkipTests) {
    Invoke-DotNet @("test", $solutionPath, "-c", $Configuration, "--no-build")
}

if ($Publish) {
    $publishOutputPath = Join-Path $repoRoot $OutputDir
    Invoke-DotNet @("publish", $projectPath, "-c", $Configuration, "-o", $publishOutputPath, "--no-build")
    Write-Host "Publicado em: $publishOutputPath" -ForegroundColor Green
}

Write-Host "Build local finalizado com sucesso." -ForegroundColor Green