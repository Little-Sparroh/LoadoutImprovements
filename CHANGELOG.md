# Changelog

## 1.2.2

- Refactor
- Rename loadout dialog now uses SparrohUILib (themed modal with input field) instead of the legacy hand-rolled box

## 1.2.1

- Equipped loadout highlight now inverts icon/fill: fill uses your UI color (`Global.UIColor`), icon turns black
- Replaces the previous gold icon tint

## 1.2.0

- Highlight loadout slots that match currently equipped upgrades (content match)
- Active slot's loadout icon is tinted gold; updates on equip, save, upgrade changes, and page scroll

## 1.1.0

- Initial standalone release of Loadout Improvements
- Split loadout features out of Enhanced Upgrade Menu
- Expanded loadouts (9 slots) with page scrolling
- Loadout renaming with configurable keybind
- Optional text loadout preview on hover
- Custom loadout icons from equipped upgrades

## 1.0.0

- Expanded loadout system with 9 customizable loadout slots
- Loadout icons use equipped upgrade icons instead of generic defaults
- In-game loadout renaming with dialog popup (press rename key while hovering)
- Enhanced loadout button tooltips with custom names and rename instructions
- Automatic loadout array expansion and UI button management
- Save/load/equip functionality patched for expanded slot support
- Add thunderstore.toml for tcli
- Add LICENSE and CHANGELOG.md
