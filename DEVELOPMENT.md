# UI development

Use Visual Studio XAML Hot Reload to adjust the UI without stopping Pause Cue or publishing a Release build for each change.

1. Open `PauseCue.csproj` in Visual Studio.
2. Start the app with **F5** in the **Debug** configuration.
3. Edit a supported `.xaml` property and save. Visual Studio applies the change to the running app.

Hot Reload applies visual and layout edits such as text, colors, spacing, and control properties. Adding a new control with a new event handler, adding files or windows, changing project settings, and changing application logic may require stopping and starting the debugger again. Release publishing remains a separate step for distributing an updated app.
