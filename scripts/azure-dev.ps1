<#
.SYNOPSIS
    Parks or wakes the AzureSuite dev compute between work sessions.

.DESCRIPTION
    stop   Stops both App Services and the Function App. Stopped apps answer 403 to every
           request, so nothing on the public internet can make them run (or log) anything.
    start  Starts them again and waits until both APIs answer /health.
    status Shows the state of each app and of the serverless SQL database.

    What is not stopped, and why:
    - Static Web App (Free): has no stop, and serves static files only, at no cost.
    - SQL (serverless free offer): pauses by itself after 60 idle minutes once the API is down.
    - Service Bus, Cosmos DB, Storage, Key Vault: no compute; managed identity only, so they
      can't be driven from outside. Messages sent while the Function App is stopped wait in
      the queue (14-day TTL) and are processed after start.

    CI still deploys to stopped apps; it skips the health checks and leaves them stopped.

.EXAMPLE
    ./scripts/azure-dev.ps1 stop
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('stop', 'start', 'status')]
    [string] $Action
)

$ErrorActionPreference = 'Stop'

$resourceGroup = 'rg-messaginghub-dev'
$webApps = @(
    @{ Name = 'app-messaginghub-catalog-dev'; Health = 'https://app-messaginghub-catalog-dev.azurewebsites.net/health' }
    @{ Name = 'app-messaginghub-ingestion-dev'; Health = 'https://app-messaginghub-ingestion-dev.azurewebsites.net/health' }
)
$functionApp = 'func-messaginghub-processing-dev'

function Invoke-Az {
    & az @args
    if ($LASTEXITCODE -ne 0) { throw "az $($args -join ' ') failed (exit $LASTEXITCODE)" }
}

function Show-Status {
    foreach ($app in $webApps) {
        $state = Invoke-Az webapp show -g $resourceGroup -n $app.Name --query state -o tsv
        '{0,-34} {1}' -f $app.Name, $state
    }
    # `az functionapp show` leaves .state empty for Flex Consumption; the raw resource has it.
    $state = Invoke-Az resource show -g $resourceGroup -n $functionApp --resource-type Microsoft.Web/sites --query properties.state -o tsv
    '{0,-34} {1}' -f $functionApp, $state
    $sqlState = Invoke-Az sql db show -g $resourceGroup -s sql-messaginghub-catalog-dev -n catalog --query status -o tsv
    '{0,-34} {1}' -f 'sql catalog database', $sqlState
}

if (-not (az account show --query id -o tsv 2>$null)) {
    throw 'Not signed in: az login --tenant 46b33134-1b34-42b4-9848-6b40016d2e22 --use-device-code'
}

switch ($Action) {
    'stop' {
        foreach ($app in $webApps) {
            Write-Host "Stopping $($app.Name)"
            Invoke-Az webapp stop -g $resourceGroup -n $app.Name
        }
        Write-Host "Stopping $functionApp"
        Invoke-Az functionapp stop -g $resourceGroup -n $functionApp
    }
    'start' {
        Write-Host "Starting $functionApp"
        Invoke-Az functionapp start -g $resourceGroup -n $functionApp
        foreach ($app in $webApps) {
            Write-Host "Starting $($app.Name)"
            Invoke-Az webapp start -g $resourceGroup -n $app.Name
        }
        # F1 cold starts take a while; /health runs no checks, so 200 means the app is up.
        foreach ($app in $webApps) {
            $healthy = $false
            for ($i = 1; $i -le 12 -and -not $healthy; $i++) {
                try {
                    $healthy = (Invoke-WebRequest $app.Health -TimeoutSec 30 -SkipHttpErrorCheck).StatusCode -eq 200
                } catch {
                    $healthy = $false
                }
                if (-not $healthy) { Start-Sleep -Seconds 10 }
            }
            Write-Host ('{0}: {1}' -f $app.Name, ($healthy ? 'healthy' : 'NOT answering /health yet'))
        }
    }
}

Show-Status
