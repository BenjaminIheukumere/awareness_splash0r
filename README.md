# Awareness Fullscreen Countdown

This small Windows tool shows a fullscreen awareness image on all connected monitors, overlays a countdown, and blocks certain key combinations while it is running.  
It is designed to be deployed centrally (e.g. via GPO) and triggered by a scheduled task for security awareness campaigns.

This is either a really good idea to spread awareness... or a really REALLY bad one.

PLEASE (for the love of god) get approval in writing from everybody and their parents before deploying.  
It WILL lock up the users' computers for the amount of time declared in the source code.

![pic1](https://github.com/user-attachments/assets/9696512e-5478-4072-a459-f5ce6c1c8860)

![pic2](https://github.com/user-attachments/assets/812714d8-e5ce-4af7-96aa-c7449e2ce183)

---

## Features

- Fullscreen display on **all** connected monitors
- Primary background image: `C:\awareness\pic1.jpg`
- Optional secondary background image: `C:\awareness\pic2.jpg`
  - Automatically shown **X seconds before the countdown ends** (configurable, default: 30 seconds)
- **Optional video overlay**: `C:\awareness\video1.wmv`
  - Shown centered (300×400) on each screen while the first image is active
  - Audio plays **only once** from the primary screen
  - Recommended format: **WMV** for best compatibility without extra codecs
- Countdown with a title (`"Countdown"`) rendered on top of the image
- Position and size of the countdown area are configurable via parameters in the source code
- Prevents:
  - `Alt+F4` inside the app
  - Left and right Windows keys (via low-level keyboard hook)
- Ignores mouse input within the app window
- Automatically exits when the countdown reaches zero

**Important:**  
The Secure Attention Sequence `Ctrl+Alt+Del` **cannot** be blocked from a normal user-mode application. This is a Windows security feature by design.

---

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.8 (or compatible)
- Read access to `C:\awareness\` for images, video and the executable
- Administrative rights for installation and deployment
- For video playback:
  - A WMV file at `C:\awareness\video1.wmv` (recommended format to avoid codec issues)
  - Standard .NET/WPF assemblies available (no extra installation needed on a typical Windows/.NET setup)

---

## Configuration in the source code

All central parameters are configured at the top of `Program.cs`:

```csharp
// Countdown duration in seconds (e.g. 180 = 3 minutes)
public static int CountdownSeconds = 180;

// Path to the primary background image
public static string ImagePath = @"C:\awareness\pic1.jpg";

// Path to the optional secondary background image
// This image will be shown shortly before the timer ends (see value below)
public static string SecondaryImagePath = @"C:\awareness\pic2.jpg";

// Optional awareness video (WMV recommended for best compatibility)
public static string VideoPath = @"C:\awareness\video1.wmv";

// Number of seconds before the end when the secondary image should be shown
public static int SecondaryImageSwitchBeforeEndSeconds = 30;

// Text displayed above the timer
public static string CountdownTitleText = "Countdown";

// Position and size of the countdown area in percent of the screen size
// 0–100, relative to fullscreen of each display
public static int CountdownAreaWidthPercent = 100; // width
public static int CountdownAreaHeightPercent = 10;  // height
public static int CountdownAreaLeftPercent = 0;     // distance from left
public static int CountdownAreaTopPercent = 85;     // distance from top
````
## Typical changes:
- Adjust CountdownSeconds to your preferred duration (e.g. 60, 120, 180)
- Adapt the image paths if you use a different folder
- Move the countdown area up/down or make it narrower/wider by changing the percentage values
- Change VideoPath if you store the WMV file in a different location

## Project structure

Minimal required files:

- Program.cs
    Contains:
        - Program class with configuration and entry point (Main)
        - CountdownForm (fullscreen window, image + countdown + optional video)
        - KeyboardBlocker (low-level keyboard hook for Windows keys)

## Optional in the repository:

Create-AwarenessTask.ps1
A PowerShell script to create a scheduled task that executes the EXE at a specific time.

## Build with Visual Studio
1. Create the project

    Install Visual Studio 2022 Community

        Include the “.NET desktop development” workload

    Create a new project:

        Template: “Windows Forms App (.NET Framework)” (C#)

        Target framework: .NET Framework 4.8

    Finish the wizard

2. Insert code

    Open Program.cs

    Delete its entire content

    Paste the full source code from this repository into Program.cs

    Remove the automatically generated Form1.cs from the project

    Add the following references to the project (they are standard .NET assemblies):

        PresentationCore

        PresentationFramework

        WindowsBase

        WindowsFormsIntegration

These references are required for the WPF-based video playback (the MediaElement hosted inside WinForms).

3. Build

    Set the configuration to Release

    Menu: Build → Build Solution

    The compiled EXE will be located in something like:

        bin\Release\AwarenessFullScreen.exe
        (the actual name depends on your project name)

You can then deploy this EXE to your clients, e.g. to
C:\awareness\AwarenessSplash.exe.

---

## Build with .NET SDK and CLI (optional)

If you prefer VS Code or the command line:

    Install the .NET SDK (e.g. .NET 8 SDK)

    Create a new WinForms project:

dotnet new winforms -n AwarenessFullScreen
cd AwarenessFullScreen

Replace the content of Program.cs with the code from this repository

Ensure WPF interop is enabled for video playback, e.g. in an SDK-style project file by adding:

<PropertyGroup>
  <UseWPF>true</UseWPF>
</PropertyGroup>

and make sure the required references (PresentationCore, PresentationFramework, WindowsBase, WindowsFormsIntegration) are present.

Build:

dotnet build -c Release

The EXE will be located under something like:

    bin\Release\net8.0-windows\AwarenessFullScreen.exe
---

## Deployment in an enterprise environment
1. Prepare files on clients

On each target system (manually, via GPO, or via software deployment):

Create the folder: C:\awareness\
    Copy:
        - AwarenessSplash.exe (compiled EXE)
        - pic1.jpg (primary background image)
        - optionally pic2.jpg (secondary image to be shown shortly before the timer ends)
        - optionally video1.wmv (optional awareness video; will be shown while pic1.jpg is active)

2. Create a scheduled task with PowerShell

Example script (Create-AwarenessTask.ps1) to run the EXE once at a given time:
````
# Create-AwarenessTask.ps1
# Creates a scheduled task for C:\awareness\AwarenessSplash.exe
# Example: run on 13.11.2025 at 08:17

$taskName = "AwarenessSplash"
$exePath  = "C:\awareness\AwarenessSplash.exe"

if (!(Test-Path $exePath)) {
    Write-Error "File '$exePath' not found. Please check path/file name."
    exit 1
}

# Date/time for the trigger (German format dd.MM.yyyy HH:mm)
$triggerTime = [datetime]::ParseExact(
    "13.11.2025 08:17",
    "dd.MM.yyyy HH:mm",
    $null
)

$action   = New-ScheduledTaskAction -Execute $exePath
$trigger  = New-ScheduledTaskTrigger -Once -At $triggerTime

Register-ScheduledTask `
    -TaskName    $taskName `
    -Action      $action `
    -Trigger     $trigger `
    -Description "AwarenessSplash at $($triggerTime.ToString('dd.MM.yyyy HH:mm'))" `
    -Force

Write-Host "Scheduled task '$taskName' has been created."
Write-Host "Run at: $($triggerTime.ToString('dd.MM.yyyy HH:mm'))"
Write-Host "Program: $exePath"
````

Run it as Administrator:

.\Create-AwarenessTask.ps1

Alternatively, configure an equivalent scheduled task centrally via Group Policy (GPP → Scheduled Tasks).
Runtime behavior

  The app starts in fullscreen mode on all monitors
    Background image: pic1.jpg
    If video1.wmv exists:
        A 300×400 video is shown centered on each screen while pic1.jpg is active
        Audio plays only once on the primary screen
    Countdown starts at CountdownSeconds and ticks down once per second

   If pic2.jpg exists:
        Exactly SecondaryImageSwitchBeforeEndSeconds seconds before the end, the background image is switched to pic2.jpg
        The video (if running) is stopped and removed
    Alt+F4 and the Windows keys are blocked while the app is running
    Mouse input inside the app has no effect
    When the countdown reaches zero, the application exits automatically

## Security & limitations

   This tool is not a kiosk-mode replacement and not a security product
   Ctrl+Alt+Del cannot be blocked; this is enforced by Windows as a security boundary
   Users can still terminate the process with tools like Task Manager, unless those are restricted by Group Policy
   The application is intended for awareness campaigns, not for locking users out of the system

## License

That MIT thingy. Share/modify how you want, but keep me in it as the OG.
