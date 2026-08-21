# How MK20Box is built

A SimHub plugin (.NET Framework 4.8, WPF) that drives the MK20 over serial
through [MK20Control](../external/MK20Control), a reverse-engineered protocol
library included as a submodule.

## Layout

```
src/Mk20Box/
  Mk20BoxPlugin.cs          entry point; SimHub lifecycle and properties
  Mk20DeviceConnection.cs   serial connection, upload, status
  Mk20BoxSettingsControl    the settings UI (XAML + code-behind)

  Layout/                   what gets stored
    Mk20LayoutSettings.cs   pages, keys, encoders
    Mk20WidgetSettings.cs   the four widget types
    Mk20MacroSettings.cs    keystrokes and macro steps
    ThemeComposer.cs        model -> device theme

  Runtime/
    SimHubBridge.cs         all host interaction
    TelemetryPump.cs        streams widget values
    PropertyResolver.cs     resolves property names

  Ui/                       view models and custom controls
  Mk20Assets/               532 bundled icons
```

## Two seams

**`ThemeComposer`** is the only code that knows the device format. Everything
the user edits lives in plain settings classes; the composer turns that model
into an MK20Control `ThemeFile` at upload time.

**`SimHubBridge`** is the only code that touches SimHub's API — actions,
inputs, keystrokes, typed text. Keystrokes use SimHub's own
`InputManagerCS.Keyboard.ShortcutKeys` rather than a hand-rolled `SendInput`.

## Where work happens

Actions split between the device and the plugin:

- **Device** — keystrokes and page navigation are encoded into the theme, so
  they run with SimHub closed.
- **Plugin** — macros and SimHub actions are routed by command id. The device
  reports a press as *row, column, pressed* only, never which page it came
  from, so each host-routed key carries a stable id. That is why a binding
  survives moving the key to another cell, page or folder.

## Telemetry

Widgets are host-driven. `TelemetryPump` reads SimHub properties once a second
and pushes values under channel names the composer bound. Each widget owns a
stable channel id, so renaming a label never breaks the binding.

The clock is pushed the same way — the device has no real-time clock.

## Profiles

Profiles are stored in SimHub's own plugin settings. A profile is not owned by a
game: `GameProfiles` maps a game name to a profile id, so any profile can be
used by any game. The plugin follows `PluginManager.GameName` and reads SimHub's
`Configuration.Games` registry, so game names are never typed by hand.

`ProfileTransfer` handles sharing. A `.mk20profile` file is a zip holding
`profile.json` plus a `media/` folder, because profiles store artwork as
absolute paths that mean nothing on another machine. On export each path is
rewritten to either `lib:` (a bundled icon, referenced so it is not copied) or
`pkg:` (embedded, named by content hash so duplicates are stored once). Import
reverses that, unpacking to `%LOCALAPPDATA%\Mk20Box\SharedMedia` since the
SimHub folder is usually not writable, and assigns a new profile id so an
import can never overwrite existing work.

## Editor

MVVM. `DeviceLayoutViewModel` owns navigation and decides which inspector the
right-hand panel shows — key, encoder or secondary screen — driven by what was
clicked on the device schematic.

The schematic is scaled with a `Viewbox`, so drag maths stays in unscaled
coordinates. Its grid column is a fixed width; an `Auto` column was resized by
the panel's content and made the device appear to jump.

## Device constraints worth knowing

Verified against hardware or the vendor's own themes:

- **Coordinates must be whole numbers.** `x=224` renders; `x=224.0395833`
  silently does not, and the upload still reports success. The composer rounds.
- **Widget colours use `r=..,g=..,b=..,a=..`.** Plain hex is correct only for
  key titles.
- **The clock has no colon and no letter-spacing.** Each digit pair is centred
  in its own box, so the box and the gap between boxes are the only spacing.
- **Text is item type 113, outline text 117.** The latter adds a border and a
  shadow; the shadow is switched off explicitly, since the builder defaults it
  on.
- **Key icons always pad**, so fill mode pre-crops to 128×128.
- Encoders do not block widgets, and an unassigned encoder emits nothing.
- In *Report to plugin* mode the device cannot distinguish rotation direction.

Disproven, so they are not re-investigated: encoder keys hiding widgets, and a
dead zone at x=320. Both were fractional coordinates.

## Building

```powershell
setx SIMHUB_INSTALL_PATH "C:\Program Files (x86)\SimHub\"
git submodule update --init --recursive

.\build.ps1        # Release, staged in dist\Mk20Box
.\build.ps1 -Zip   # also packs a release archive
.\deploy.ps1       # build and install into SimHub (development)
```

SimHub's assemblies are referenced, never copied or modified. Building never
writes to the installation — deployment is opt-in via the `DeployToSimHub`
MSBuild property, which `deploy.ps1` sets. `deploy.ps1` refuses to run while
SimHub holds the DLL open, and verifies the deployed file matches the build.

The MK20Control
[API guide](../external/MK20Control/Mk20Control.Protocol.API.md) and
[examples](../external/MK20Control/examples/) are authoritative for protocol
questions.
