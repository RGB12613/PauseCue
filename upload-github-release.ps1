$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'PauseCue.csproj'
$remote = git -C $PSScriptRoot remote get-url origin 2>$null
if ($LASTEXITCODE -ne 0 -or -not $remote) { throw 'GitHub remote "origin" is not configured.' }

gh auth status -h github.com
if ($LASTEXITCODE -ne 0) { throw 'Authenticate with GitHub CLI first: gh auth login -h github.com' }

[xml]$projectXml = Get-Content -LiteralPath $project
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { throw 'Version is not set in PauseCue.csproj' }
$tag = "v$version"

& (Join-Path $PSScriptRoot 'publish-release.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Release package creation failed.' }

$packagePath = Join-Path $PSScriptRoot '.build\Release\packages\PauseCue-win-x64.zip'
gh release create $tag $packagePath --title "Pause Cue $version" --generate-notes
if ($LASTEXITCODE -ne 0) { throw "GitHub Release creation failed for $tag" }
Write-Host "Published GitHub Release $tag to $remote"
