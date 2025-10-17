# ImprovedLoadouts

A BepInEx mod for MycoPunk that enhances the loadout system with more slots, upgrade icons, and custom naming.

## Description

This client-side mod significantly improves the loadout management experience in MycoPunk by expanding from 3 to 9 loadout slots and adding visual and organizational enhancements. Loadout buttons now display icons from equipped upgrades, and each loadout can be assigned custom names for easier identification and organization.

The mod enhances the existing save/load/equip functionality while maintaining full compatibility with the base game systems. Loadouts are automatically managed and expanded as needed, providing players with much more flexibility in managing their upgrade builds.

## Getting Started

### Dependencies

* MycoPunk (base game)
* [BepInEx](https://github.com/BepInEx/BepInEx) - Version 5.4.2403 or compatible
* .NET Framework 4.8

### Building/Compiling

1. Clone this repository
2. Open the solution file in Visual Studio, Rider, or your preferred C# IDE
3. Build the project in Release mode

Alternatively, use dotnet CLI:
```bash
dotnet build --configuration Release
```

### Installing

**Option 1: Via Thunderstore (Recommended)**
1. Download and install using the Thunderstore Mod Manager
2. Search for "ImprovedLoadouts" under MycoPunk community
3. Install and enable the mod

**Option 2: Manual Installation**
1. Ensure BepInEx is installed for MycoPunk
2. Copy `ImprovedLoadouts.dll` from the build folder
3. Place it in `<MycoPunk Game Directory>/BepInEx/plugins/`
4. Launch the game

### Executing program

Once installed, the mod automatically expands loadout functionality in gear detail windows:

**Enhanced Features:**
- **9 Loadout Slots:** Save and manage up to 9 different loadout configurations
- **Upgrade Icons:** Loadout buttons display icons from your equipped upgrades
- **Custom Names:** Press 'L' while hovering over a loadout to open rename dialog
- **Automatic Array Management:** Loadouts are dynamically expanded as needed

**Renaming Loadouts:**
1. Hover over any loadout button in the gear details UI
2. Press the 'L' key to open a rename dialog
3. Enter a custom name (or leave blank to use default)
4. Names persist for the current session

Loadouts work exactly as before but with more slots and better visual identification.

## Help

* **Why only 3 loadouts showing?** Make sure you have saved upgrades to additional slots first
* **Can't rename loadouts?** Make sure you're hovering over the exact loadout button
* **Icons not updating?** Loadout icons update when the loadout is saved or loaded
* **Custom names lost?** Names are currently session-only (persistence planned for future versions)
* **Performance impact?** Minimal - only affects loadout UI updates
* **Compatibility issues?** Mod designed to work with base game loadout systems

## Authors

* Sparroh
* funlennysub (original mod template)
* [@DomPizzie](https://twitter.com/dompizzie) (README template)

## License

* This project is licensed under the MIT License - see the LICENSE.md file for details
