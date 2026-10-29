param(
    [ValidateSet('all', 'api', 'docs')]
    [string]$Target = 'all',
    [string]$HostName = 'pms-vps'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts\deploy'
$releaseId = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)

function Invoke-Remote([string]$Command) {
    & ssh $HostName $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Remote command failed with exit code $LASTEXITCODE."
    }
}

function Publish-Directory([string]$Source, [string]$RemoteRoot, [string]$HealthCheck) {
    $archive = Join-Path $artifactRoot "$releaseId.tgz"
    $remoteArchive = "/tmp/printsharp-$releaseId.tgz"
    $remoteRelease = "$RemoteRoot/releases/$releaseId"

    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    & tar.exe -czf $archive -C $Source .
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to archive $Source."
    }

    & scp $archive "${HostName}:$remoteArchive"
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to upload the release archive.'
    }

    $permissions = if ($RemoteRoot -eq '/opt/printsharp') {
        "find '$remoteRelease' -type d -exec chmod 755 {} +; find '$remoteRelease' -type f -exec chmod 644 {} +; chmod 755 '$remoteRelease/PrintSharp.Api';"
    }
    else {
        "find '$remoteRelease' -type d -exec chmod 755 {} +; find '$remoteRelease' -type f -exec chmod 644 {} +;"
    }
    $rollbackAction = if ($RemoteRoot -eq '/opt/printsharp') { 'systemctl restart printsharp-api || true;' } else { '' }
    $publish = "set -e; mkdir -p '$remoteRelease'; tar -xzf '$remoteArchive' -C '$remoteRelease'; rm -f '$remoteArchive'; $permissions previous=`$(readlink -f '$RemoteRoot/current' || true); ln -sfn '$remoteRelease' '$RemoteRoot/current.next'; mv -Tf '$RemoteRoot/current.next' '$RemoteRoot/current'; if ! ( $HealthCheck ); then if [ -n `"`$previous`" ]; then ln -sfn `"`$previous`" '$RemoteRoot/current.next'; mv -Tf '$RemoteRoot/current.next' '$RemoteRoot/current'; $rollbackAction fi; exit 1; fi"
    Invoke-Remote $publish
    Remove-Item -LiteralPath $archive -Force
}

if ($Target -in @('all', 'api')) {
    $apiOutput = Join-Path $artifactRoot 'api'
    if (Test-Path $apiOutput) {
        Remove-Item -LiteralPath $apiOutput -Recurse -Force
    }

    & dotnet publish (Join-Path $repoRoot 'src\PrintSharp.Api\PrintSharp.Api.csproj') `
        -c Release -r linux-x64 --self-contained true -o $apiOutput
    if ($LASTEXITCODE -ne 0) {
        throw 'PrintSharp.Api publish failed.'
    }

    Publish-Directory $apiOutput '/opt/printsharp' "systemctl restart printsharp-api; for i in {1..30}; do if curl -fsS http://127.0.0.1:5080/api/v1/health >/dev/null 2>&1; then healthy=1; break; fi; sleep 2; done; if [ `"`$healthy`" = 1 ]; then true; else journalctl -u printsharp-api -n 80 --no-pager; false; fi"
    Write-Host 'PrintSharp.Api published and passed its local health check.'
}

if ($Target -in @('all', 'docs')) {
    $docsRoot = Join-Path $repoRoot 'docs-site'
    Push-Location $docsRoot
    try {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'Docs dependency restore failed.' }
        & npm.cmd run docs:build
        if ($LASTEXITCODE -ne 0) { throw 'Docs build failed.' }
    }
    finally {
        Pop-Location
    }

    $docsCheck = "curl -fsS --resolve printsharp-docs.ginbow.eu.org:443:127.0.0.1 https://printsharp-docs.ginbow.eu.org/ -o /dev/null"
    Publish-Directory (Join-Path $docsRoot '.vitepress\dist') '/var/www/printsharp-docs' $docsCheck
    Write-Host 'PrintSharp docs published and passed their HTTPS check.'
}
