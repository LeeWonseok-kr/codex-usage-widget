# Codex Usage Widget

A compact Windows desktop widget for monitoring Codex account usage. It follows the Windows theme and displays a two-line taskbar overlay with left, center, and right positioning. The details window has a fixed size.

## Screenshot

![Codex usage overlay on the Windows taskbar](docs/images/taskbar.png)

An actual screenshot with the overlay positioned on the right. Usage values and dates reflect the time of capture. The current application interface is in Korean; English descriptions below explain the controls.

## Getting started

1. Install the Codex app for Windows and sign in to your account.
2. Download [CodexUsage-Windows.zip](CodexUsage-Windows.zip) and extract it to a folder of your choice.
3. Run `CodexUsage.exe`.
4. To launch automatically at Windows sign-in, click the startup toggle at the top of the details window. Click it again to disable startup.

The startup toggle creates a shortcut in the current Windows user's Startup folder. Administrator privileges are not required. At sign-in, the widget shows the small overlay and keeps the details window hidden. Before moving the executable, disable startup, then enable it again from the new location. Disable startup before uninstalling the widget.

Each PC uses the Codex account signed in on that PC. You do not need to copy authentication files, another PC's `settings.json`, or task history.

## Displayed information

- **Summary** (first tab): account-wide lifetime tokens, token aggregation date, remaining quota, and next reset time.
- **Daily** (second tab): usage for the latest seven dates reported by the server, plus the total for those displayed dates.
- **This task** (third tab): total, input, output, and cached tokens for the selected local task, along with record status and the last recorded timestamp.

On first launch, the widget selects the most recently updated task record on this PC. Use the task selection submenu under **···** to choose another recent task. Your selection stays fixed; the widget does not automatically detect the conversation currently open in Codex.

Cached tokens are included in input tokens and are not added to the total again. Account-wide lifetime and daily usage may be delayed by server aggregation. Dates not yet reported are not treated as zero usage. Exact account-wide token usage since a quota reset is not currently displayed. When multiple standard Codex quota windows are available, the widget displays the remaining percentage for the longest window.

## Controls and refresh intervals

- **Click the overlay:** open the details window.
- **Drag the overlay:** move it manually.
- **Position buttons at the bottom:** choose left, center, or right, in that order. The selection is saved across restarts. Right positioning places the overlay before the clock and notification area.
- **Reset overlay position** in the **···** menu: return to the selected automatic position.
- **Toggle overlay** in the **···** menu: show or hide the small display.
- **Close the details window:** hide the window while the widget keeps running.
- **Ctrl+Shift+Q**, or the exit item in **···**: quit completely.
- **Quota refresh:** every 10 seconds.
- **Account token refresh:** every 60 seconds.
- **Selected task:** check for file changes every 2 seconds.

The overlay is a separate window placed over the taskbar. It does not modify the Windows clock or Explorer. If your Start button is on the left, use the right position button. Center and left positioning may overlap icons at those locations; you can also drag the overlay to adjust it. K, M, and B mean thousand, million, and billion tokens.

## Build from source

Uses .NET Framework 4.x and PowerShell on Windows 10/11. No additional packages are required.

```powershell
.\build.ps1
.\test.ps1
.\dist\CodexUsage.exe
```

If `CODEX_HOME` is set, the widget uses that folder. Otherwise, it uses `.codex` under the current user's profile. It looks for the Codex executable under `OpenAI/Codex/bin` in local application data, then falls back to `codex.exe` on PATH. If necessary, set `codexExe` in the generated `settings.json` to your installation path.

## Data handling

The widget reads account information through the signed-in Codex `app-server` and reads the selected task's local records. It does not create model tasks. Settings, the selected task path, and logs remain on your PC and are excluded from the repository and distribution package. Future Codex protocol changes may require compatibility updates.
