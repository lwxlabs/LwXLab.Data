param(
    [ValidateSet('All','Sqlite','MySql','MariaDb')]
    [string] $Provider = 'All'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts/local-providers'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$priorProvider = [Environment]::GetEnvironmentVariable('LWX_PROVIDER', 'Process')
$priorConnection = [Environment]::GetEnvironmentVariable('LWX_PROVIDER_CONNECTION', 'Process')
$runId = [guid]::NewGuid().ToString('N').Substring(0, 12)

function Invoke-Check([string] $framework) {
    & dotnet run --project (Join-Path $root 'tests/LwXLab.Data.ProviderLiveTests') -c Release -f $framework
    if ($LASTEXITCODE -ne 0) { throw "$env:LWX_PROVIDER $framework live test failed." }
}
function Invoke-Docker([string[]] $DockerArgs) {
    $result = & docker @DockerArgs
    if ($LASTEXITCODE -ne 0) { throw "Docker command failed: $($DockerArgs[0])" }
    return $result
}
try {
    if ($Provider -in @('All','Sqlite')) {
        $env:LWX_PROVIDER = 'Sqlite'
        foreach ($framework in @('net8.0','net9.0','net10.0')) {
            $database = Join-Path $output "sqlite-$runId-$framework.db"
            try {
                $env:LWX_PROVIDER_CONNECTION = "Data Source=$database"
                Invoke-Check $framework
            }
            finally { Remove-Item -LiteralPath $database -ErrorAction SilentlyContinue }
        }
    }
    foreach ($engine in @('MySql','MariaDb')) {
        if ($Provider -notin @('All',$engine)) { continue }
        $name = "lwx-data-$($engine.ToLowerInvariant())-$runId"
        $password = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
        $image = if ($engine -eq 'MySql') { 'mysql:8.4' } else { 'mariadb:10.11' }
        $env:LWX_PROVIDER = $engine
        try {
            $container = Invoke-Docker @('run','--rm','-d','--name',$name,'-p','127.0.0.1::3306',
                '-e',"MYSQL_ROOT_PASSWORD=$password",'-e','MYSQL_ROOT_HOST=%',$image)
            $portLine = (Invoke-Docker @('port',$name,'3306/tcp') | Select-Object -First 1)
            if ($portLine -notmatch '127\.0\.0\.1:(\d+)$') { throw "Unexpected Docker port: $portLine" }
            $port = $Matches[1]
            $ready = $false
            for ($attempt = 0; $attempt -lt 60; $attempt++) {
                & docker exec -e "MYSQL_PWD=$password" $name mysql -uroot -N -e 'SELECT 1' *> $null
                if ($LASTEXITCODE -eq 0) { $ready = $true; break }
                Start-Sleep -Seconds 2
            }
            if (-not $ready) { throw "$engine did not become ready." }
            foreach ($framework in @('net8.0','net9.0','net10.0')) {
                $database = "lwx_verify_$($framework.Replace('.','_'))"
                Invoke-Docker @('exec','-e',"MYSQL_PWD=$password",$name,'mysql','-uroot','-e',"CREATE DATABASE $database") | Out-Null
                $ssl = if ($engine -eq 'MySql') { 'Required' } else { 'None' }
                $env:LWX_PROVIDER_CONNECTION = "Server=127.0.0.1;Port=$port;Database=$database;User ID=root;Password=$password;SslMode=$ssl;Pooling=false"
                Invoke-Check $framework
            }
        }
        finally {
            & docker rm -f $name *> $null
            if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove disposable container $name" }
            $password = $null
        }
    }
    Write-Host 'Selected live provider tests passed; disposable files and containers were removed.'
}
finally {
    [Environment]::SetEnvironmentVariable('LWX_PROVIDER', $priorProvider, 'Process')
    [Environment]::SetEnvironmentVariable('LWX_PROVIDER_CONNECTION', $priorConnection, 'Process')
}
