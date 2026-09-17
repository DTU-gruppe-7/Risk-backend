#!/usr/bin/env pwsh
#requires -Version 7.0

<#
.SYNOPSIS
    Sætter din personlige JWT-signeringsnøgle for Risk.Api.
    Kører på Windows, macOS og Linux.

.DESCRIPTION
    Køres én gang efter clone af repoet. Genererer en tilfældig nøgle og lægger
    den i din user-secrets-store, som ligger uden for repoet og aldrig bliver
    committet. Nøglen er unik pr. udvikler og skal ikke deles.

    Databaseopsætningen ligger i Risk.Api/appsettings.Development.json og er
    fælles for teamet - den hører ikke hjemme her.

    Kræver PowerShell 7. Har du den ikke, og har du .NET SDK'en:
        dotnet tool install --global PowerShell
    Derefter er kommandoen 'pwsh' tilgængelig på alle platforme.

.EXAMPLE
    ./setup-dev-secrets.ps1

.EXAMPLE
    ./setup-dev-secrets.ps1 -Force
#>

param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$SolutionName = 'Risk'

# ---------------------------------------------------------------------------
# Find projektet relativt til scriptet, ikke til den mappe man står i
# ---------------------------------------------------------------------------
$Project = Join-Path $PSScriptRoot "$SolutionName.Api" "$SolutionName.Api.csproj"

if (-not (Test-Path $Project)) {
    throw "Kunne ikke finde $Project. Ligger scriptet i roden af solutionen?"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Kommandoen 'dotnet' blev ikke fundet. Installer .NET SDK'en fra https://dotnet.microsoft.com/download"
}

# ---------------------------------------------------------------------------
# Sikrer at projektet har en UserSecretsId
# ---------------------------------------------------------------------------
if (-not (Select-String -Path $Project -Pattern 'UserSecretsId' -Quiet)) {
    Write-Host 'Initialiserer user-secrets...' -ForegroundColor Green
    dotnet user-secrets init --project $Project
    if ($LASTEXITCODE -ne 0) { throw 'dotnet user-secrets init fejlede.' }
}

# ---------------------------------------------------------------------------
# JWT-signeringsnøgle: 48 tilfældige bytes
# ---------------------------------------------------------------------------
$alreadySet = dotnet user-secrets list --project $Project 2>$null |
              Select-String -Pattern '^Jwt:Key = ' -Quiet

if ($alreadySet -and -not $Force) {
    Write-Host 'JWT-signeringsnøglen findes allerede - intet at gøre.' -ForegroundColor DarkGray
    Write-Host 'Brug -Force hvis du vil generere en ny.' -ForegroundColor DarkGray
}
else {
    $bytes = [byte[]]::new(48)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try     { $rng.GetBytes($bytes) }
    finally { $rng.Dispose() }

    dotnet user-secrets set 'Jwt:Key' ([Convert]::ToBase64String($bytes)) --project $Project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Kunne ikke sætte Jwt:Key.' }

    Write-Host 'JWT-signeringsnøgle sat.' -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Advar hvis en gammel connection string stadig ligger i user-secrets.
# Den ville overskygge appsettings.Development.json uden at nogen opdager det.
# ---------------------------------------------------------------------------
$staleConnection = dotnet user-secrets list --project $Project 2>$null |
                   Select-String -Pattern '^ConnectionStrings:' -Quiet

if ($staleConnection) {
    Write-Host ''
    Write-Host 'Bemærk: der ligger en connection string i dine user-secrets.' -ForegroundColor Yellow
    Write-Host 'Den overskygger appsettings.Development.json. Fjern den med:' -ForegroundColor Yellow
    Write-Host "  dotnet user-secrets remove `"ConnectionStrings:Default`" --project $Project"
}

# ---------------------------------------------------------------------------
# Vis hvor filen ligger - stien er forskellig pr. platform
# ---------------------------------------------------------------------------
$secretsId = (Select-String -Path $Project -Pattern '<UserSecretsId>(.+?)</UserSecretsId>').Matches.Groups[1].Value

$secretsPath = if ($IsWindows) {
    Join-Path $env:APPDATA 'Microsoft' 'UserSecrets' $secretsId 'secrets.json'
} else {
    Join-Path $HOME '.microsoft' 'usersecrets' $secretsId 'secrets.json'
}

Write-Host ''
Write-Host 'Færdig.' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Se dine secrets med:'
Write-Host "  dotnet user-secrets list --project $Project"
Write-Host ''
Write-Host "Filen ligger her: $secretsPath" -ForegroundColor DarkGray
