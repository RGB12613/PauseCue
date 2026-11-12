param(
    [Parameter(Mandatory = $true)][int]$ParentPid,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$RestartExecutable,
    [Parameter(Mandatory = $true)][string]$ReleaseTag
)

$ErrorActionPreference = 'Stop'
$resolvedInstall = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$resolvedRestart = [System.IO.Path]::GetFullPath($RestartExecutable)
$resolvedArchive = [System.IO.Path]::GetFullPath($ArchivePath)
$stage = "$resolvedInstall.update-staging"
$backup = "$resolvedInstall.previous"

try {
    for ($attempt = 0; $attempt -lt 300; $attempt++) {
        if (-not (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 200
    }
    if (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue) {
        throw 'Pause Cue did not close in time.'
    }

    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($resolvedArchive, $stage)

    $newExecutable = Join-Path $stage ([System.IO.Path]::GetFileName($resolvedRestart))
    if (-not (Test-Path -LiteralPath $newExecutable -PathType Leaf)) {
        throw 'The downloaded package does not contain PauseCue.exe.'
    }

    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
    if (Test-Path -LiteralPath $resolvedInstall) { Move-Item -LiteralPath $resolvedInstall -Destination $backup }
    try {
        Move-Item -LiteralPath $stage -Destination $resolvedInstall
    }
    catch {
        if ((Test-Path -LiteralPath $backup) -and -not (Test-Path -LiteralPath $resolvedInstall)) {
            Move-Item -LiteralPath $backup -Destination $resolvedInstall
        }
        throw
    }

    Start-Process -FilePath $resolvedRestart
    Remove-Item -LiteralPath $resolvedArchive -Force -ErrorAction SilentlyContinue
}
catch {
    if ((Test-Path -LiteralPath $stage) -and -not (Test-Path -LiteralPath $resolvedInstall)) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show("Pause Cueの更新を完了できませんでした。`n`n$($_.Exception.Message)`n`n旧バージョンを起動します。", "Pause Cue 更新", 'OK', 'Warning') | Out-Null
    if (Test-Path -LiteralPath $resolvedRestart) { Start-Process -FilePath $resolvedRestart }
    exit 1
}
