param(
    [Parameter(Mandatory = $true)][int]$ParentPid,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$RestartExecutable,
    [Parameter(Mandatory = $true)][string]$ReleaseTag
)

# NOTE: Windows PowerShell 5.1 reads BOM-less scripts as the ANSI code page.
# Keep this file saved as UTF-8 *with BOM* (the app also re-writes it with a BOM before running it).

$ErrorActionPreference = 'Stop'
# Never keep the install folder as the current directory, or it cannot be modified.
Set-Location -LiteralPath ([System.IO.Path]::GetTempPath())

$resolvedInstall = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$resolvedRestart = [System.IO.Path]::GetFullPath($RestartExecutable)
$resolvedArchive = [System.IO.Path]::GetFullPath($ArchivePath)
$stage = "$resolvedInstall.update-staging"
$backup = "$resolvedInstall.previous"
$movedToBackup = New-Object System.Collections.Generic.List[string]
$installedFromStage = New-Object System.Collections.Generic.List[string]

function Restore-Previous {
    foreach ($name in $installedFromStage) {
        Remove-Item -LiteralPath (Join-Path $resolvedInstall $name) -Recurse -Force -ErrorAction SilentlyContinue
    }
    foreach ($name in $movedToBackup) {
        $source = Join-Path $backup $name
        if (Test-Path -LiteralPath $source) {
            Move-Item -LiteralPath $source -Destination (Join-Path $resolvedInstall $name) -Force -ErrorAction SilentlyContinue
        }
    }
}

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
    New-Item -ItemType Directory -Path $backup | Out-Null
    if (-not (Test-Path -LiteralPath $resolvedInstall)) { New-Item -ItemType Directory -Path $resolvedInstall | Out-Null }

    # Swap the folder contents instead of renaming the folder itself, so an Explorer
    # window or another process looking at the folder does not block the update.
    try {
        $stagedNames = @(Get-ChildItem -LiteralPath $stage -Force | ForEach-Object { $_.Name })
        foreach ($name in $stagedNames) {
            $target = Join-Path $resolvedInstall $name
            if (Test-Path -LiteralPath $target) {
                Move-Item -LiteralPath $target -Destination (Join-Path $backup $name)
                $movedToBackup.Add($name)
            }
        }
        foreach ($name in $stagedNames) {
            Move-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $resolvedInstall $name)
            $installedFromStage.Add($name)
        }
    }
    catch {
        Restore-Previous
        throw
    }

    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath $resolvedRestart -WorkingDirectory $resolvedInstall
    Remove-Item -LiteralPath $resolvedArchive -Force -ErrorAction SilentlyContinue
}
catch {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show("Pause Cueの更新を完了できませんでした。`n`n$($_.Exception.Message)`n`n旧バージョンを起動します。", "Pause Cue 更新", 'OK', 'Warning') | Out-Null
    if (-not (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $resolvedRestart)) {
        Start-Process -FilePath $resolvedRestart -WorkingDirectory $resolvedInstall
    }
    exit 1
}