# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('setup', 'run')]
    [string]$Action,
    [string]$PythonPath,
    [string]$EnvironmentRoot
)

$ErrorActionPreference = 'Stop'
$names = @{
    'agent-targeted-messages' = 'targeted'
    'bot-ai-messages' = 'ai'
    'bot-attachments' = 'files'
    'bot-cards' = 'cards'
    'bot-meetings' = 'meetings'
    'bot-message-extensions' = 'msgext'
    'bot-task-modules' = 'tasks'
}
$sample = Split-Path (Get-Location).Path -Leaf
if (-not $names.ContainsKey($sample) -or
    (Resolve-Path '..').Path -ne (Resolve-Path $PSScriptRoot).Path) {
    throw 'Run this command from a Python Teams sample directory.'
}
$environment = if ($EnvironmentRoot) {
    Join-Path $EnvironmentRoot ('teams-' + $names[$sample])
} else {
    Join-Path (Get-Location).Path '.venv'
}
$interpreter = Join-Path $environment 'Scripts\python.exe'

function Test-PythonVersion([string]$Executable) {
    try {
        $version = & $Executable -c 'import sys; print(".".join(map(str, sys.version_info[:3])))' 2>$null
        return ($LASTEXITCODE -eq 0 -and $version -eq '3.11.14')
    } catch { return $false }
}

if ($Action -eq 'setup') {
    $longPaths = Get-ItemPropertyValue 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' 'LongPathsEnabled'
    if ($longPaths -ne 1) {
        throw 'Enable Windows long-path support and restart Windows before setup. See this sample README.'
    }
    if (-not (Test-Path $interpreter)) {
        if (Test-Path $environment) {
            throw "Incomplete environment at $environment. Choose a new -EnvironmentRoot."
        }
        if ($PythonPath) {
            $candidates = @($PythonPath)
        } else {
            $repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
            $candidates = @(
                (Join-Path $repo '.sync\python-tools311\Scripts\python.exe'),
                'python3.11', 'python'
            )
            if (Get-Command py -ErrorAction SilentlyContinue) {
                try { $candidates += & py -3.11 -c 'import sys; print(sys.executable)' 2>$null } catch { }
            }
        }
        $selected = $candidates | Where-Object { $_ -and (Test-PythonVersion $_) } | Select-Object -First 1
        if (-not $selected) {
            throw 'Python 3.11.14 was not found. Run setup -PythonPath C:\path\to\python.exe.'
        }
        & $selected -m venv $environment
        if ($LASTEXITCODE -ne 0) { throw 'Environment creation failed.' }
    }
}

if (-not (Test-PythonVersion $interpreter)) {
    throw "Expected Python 3.11.14 at $interpreter. Run setup with a fresh -EnvironmentRoot; do not recreate an existing environment with another Python version."
}
if ($Action -eq 'setup') {
    & $interpreter -m pip install -r requirements.txt
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed; resolve the error and rerun setup.' }
    & $interpreter -m pip check
    if ($LASTEXITCODE -ne 0) { throw 'Dependency compatibility check failed.' }
    & $interpreter -c 'from pydantic_core import ArgsKwargs; from src.agent import create_agent'
    if ($LASTEXITCODE -ne 0) { throw 'Sample import check failed.' }
    if (-not (Test-Path '.env')) {
        $template = if (Test-Path 'env.TEMPLATE') { 'env.TEMPLATE' } else { '.env.TEMPLATE' }
        Copy-Item $template '.env'
    }
    Write-Host "Setup complete: $environment. Configure .env, then run: ..\sample.ps1 run"
} else {
    & $interpreter -m src.main
    exit $LASTEXITCODE
}
