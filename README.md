# Pause Cue

Pause Cue is a small Windows tray app that reminds you to take breaks during long work sessions. It tracks work time locally and shows a full-screen break prompt with a hold-to-confirm progress gauge.

## Install

Download `PauseCue-win-x64.zip` from the [latest GitHub Release](https://github.com/RGB12613/PauseCue/releases/latest), extract it to a folder you can write to, and run `PauseCue.exe`. The app requires the .NET 10 Desktop Runtime.

After the first install, select **更新を確認** in the app to download and install newer GitHub Releases. The updater verifies the release asset digest, closes Pause Cue, replaces its install folder, and starts the new version. Settings and work logs are stored under `%LocalAppData%\PauseCue` and are preserved during updates.

## Build and publish

Open `PauseCue.csproj` in Visual Studio and press F5 to use XAML Hot Reload while tuning the UI. See [DEVELOPMENT.md](DEVELOPMENT.md) for details.

For a GitHub Release, increment the project version and run `./upload-github-release.ps1` after pushing the source commit. See [RELEASE.md](RELEASE.md).
