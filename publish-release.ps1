$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'PauseCue.csproj'
$buildRoot = Join-Path $PSScriptRoot '.build\Release'
$publishDir = Join-Path $buildRoot 'publish'
$packageDir = Join-Path $buildRoot 'packages'
$packagePath = Join-Path $packageDir 'PauseCue-win-x64.zip'
$resolvedRoot = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)

foreach ($target in @($buildRoot, $publishDir, $packageDir)) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($target)
    if (-not $resolvedTarget.StartsWith("$resolvedRoot$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected build output path: $resolvedTarget"
    }
}

[xml]$projectXml = Get-Content -LiteralPath $project
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { throw 'Version is not set in PauseCue.csproj' }

if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

$windowsSdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
if (-not (Test-Path -LiteralPath $windowsSdkRoot)) { throw "Windows SDK was not found at $windowsSdkRoot" }

$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
dotnet publish $project -c Release -o $publishDir `
    "-p:OutputPath=$buildRoot\bin\" `
    -p:TargetPlatformVersion=10.0.19041.0 `
    "-p:TargetPlatformSdkRootOverride=$windowsSdkRoot" `
    -p:TargetPlatformDisplayName=Windows `
    -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $packagePath -CompressionLevel Optimal
Write-Host "Pause Cue $version packaged at $packagePath"
