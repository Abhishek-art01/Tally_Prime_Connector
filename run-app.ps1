[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArguments
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'The .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again.'
    exit 1
}

$projectPath = Join-Path $PSScriptRoot 'apps\TallyPrimeConnector.App\TallyPrimeConnector.App.csproj'

& dotnet run --project $projectPath --configuration $Configuration -- $AppArguments
exit $LASTEXITCODE
