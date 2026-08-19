# Mk20Box
Open-source SimHub plugin driving Waveshare MK20 ScreenKey pads as live sim-racing dashboards and button box

## Per-game profiles

The SimHub settings page supports a global profile and per-game overrides.
Profiles are reusable named configurations created and deleted from the
settings page. Global mode uses one selected profile for every game. When
global mode is disabled, each configured game selects a profile from a
dropdown; unconfigured games fall back to the global profile.

The active profile follows the game selected in SimHub's main menu through
`PluginManager.GameName`. The configuration UI also reads SimHub's complete
`Configuration.Games` registry, so any supported game can be selected from a
searchable dropdown and game names never need to be entered manually. The
selection is exposed through `MK20Box.ActiveGame` and `MK20Box.ActiveProfile`.
`MK20Box.ActiveProfileIsGlobal` reports whether the active selection came
from the global profile.
