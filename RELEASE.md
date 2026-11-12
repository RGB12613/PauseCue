# GitHub Releases and in-app updates

Pause Cue checks the public `RGB12613/PauseCue` GitHub repository for its latest release. Each release must include an asset named `PauseCue-win-x64.zip`; the updater verifies GitHub's SHA-256 digest when available, downloads the package, closes Pause Cue, replaces its install folder through a separate PowerShell helper, and restarts the app. User settings and work logs stay under `%LocalAppData%`.

## Publish a release

1. Increase `<Version>` in `PauseCue.csproj` using `major.minor.patch`.
2. Commit and push the changes to GitHub.
3. Run `./upload-github-release.ps1` from PowerShell. The script builds Release, packages it, and creates a GitHub Release tagged `v<Version>`.
4. In Pause Cue, choose the update check button and accept the update.

The Release package is built under `.build\Release\packages\`. Publishing never clears or overwrites the currently running app folder.

The first release containing the updater must be installed once using its GitHub Release package. After that, the app can update itself from later releases.
