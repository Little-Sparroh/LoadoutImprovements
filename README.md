# Loadout Improvements

A BepInEx mod for Mycopunk that expands and improves gear loadout management.

## Features

- **Expanded Loadouts**: Up to 9 loadout slots per gear piece, navigated in pages of 3
- **Loadout Scrolling**: Configurable left/right keys to page through loadout slots
- **Rename Loadouts**: Hover a loadout and press the rename key to give it a custom name
- **Loadout Preview**: Optional text preview listing upgrades when hovering a loadout
- **Custom Loadout Icons**: Loadout icons can use equipped upgrade icons

## Getting Started

### Dependencies

* Mycopunk (base game)
* [BepInEx](https://github.com/BepInEx/BepInEx) - Version 5.4.2403 or compatible (BepInExPack_Mycopunk)
* HarmonyLib (included via BepInEx)

### Installing

**Via Thunderstore (Recommended)**:
1. Download and install via Thunderstore Mod Manager / r2modman
2. The mod will be installed to the correct plugins directory automatically

**Manual Installation**:
1. Place `LoadoutImprovements.dll` in your `<Mycopunk Directory>/BepInEx/plugins/` folder

### Building

```bash
dotnet build --configuration Release
```

## Configuration

Settings are stored at:

`<Mycopunk Directory>/BepInEx/config/sparroh.loadoutimprovements.cfg`

| Section | Option | Default | Description |
|---------|--------|---------|-------------|
| Keybinds | Scroll Loadout Left | `,` | Previous loadout page |
| Keybinds | Scroll Loadout Right | `.` | Next loadout page |
| Keybinds | Rename Loadout | `L` | Rename the hovered loadout |
| General | Loadout Preview | `false` | Show upgrade list when hovering a loadout |

## Help

* **Mod not loading?** Verify BepInEx is installed and check the BepInEx log for errors
* **Keybinds not working?** Check for conflicts with other mods or game binds
* **Can't see extra loadouts?** Use the scroll keys to move between pages (slots 1–3, 4–6, 7–9)
* **Rename not working?** Hover a loadout button first, then press the rename key

## Authors

- Sparroh
- Coloron (Loadout Expander)

## License

This project is licensed under the MIT License - see the LICENSE file for details
