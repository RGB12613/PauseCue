# Build instructions

- For every code change, build the application in Release mode before reporting completion.
- Use `publish-release.ps1` to create the distributable in `release\`.
- This project targets WPF on Windows. In this restricted build environment, MSBuild's default Windows SDK discovery reads machine registry keys and fails with `Requested registry access is not allowed`. Avoid that failure by setting all of these publish properties: `TargetPlatformVersion=10.0.19041.0`, `TargetPlatformSdkRootOverride` to `%ProgramFiles(x86)%\Windows Kits\10`, and `TargetPlatformDisplayName=Windows`.
- Set `DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1` before invoking `dotnet publish`; the SDK's workload notification can fail while querying Windows services in this environment.
- Publish to a dedicated intermediate `OutputPath` under `.build\Release\` so a running copy of the app under `bin\Release\` cannot lock the compiler output.
- If NuGet restore fails because `api.nuget.org` is unreachable, restore requires network access. Do not treat that network error as a source-code build failure; retry with network access enabled.
- Resolve every compiler error revealed by the Release build, then rebuild and confirm the publish succeeds before finishing.
