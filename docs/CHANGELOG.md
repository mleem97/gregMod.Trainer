# Changelog

## 1.0.3

- Added a self-contained input lock while the trainer panel is open (mirrors
  `gregCore.UI.GregInputLock` without the gregCore dependency): `PlayerManager`
  movement/mouse/ray-interact flags are disabled and all `PlayerInput`
  components are suspended, cursor is forced unlocked each frame.
  Fixes: the camera/movement no longer keep running with the menu open, and
  the manual click routing works again (the game was re-locking the cursor,
  freezing `Mouse.current.position`).
- Click/drag coordinates now convert through `RuntimePanelUtils.ScreenToPanel`
  (panel scaling-safe) instead of a raw screen-pixel flip.

## 1.0.2

- Migrated keyboard input from the removed legacy `UnityEngine.Input` API to `UnityEngine.InputSystem.Keyboard`.
- Prevented per-frame input exceptions on the current Data Center input configuration.

## 1.0.0

- Rebuilt against Data Center 1.1.0 / Unity 6000.4.12f1.

## 1.0.1

- Rebranded the mod, assembly, and repository as `gregMod.NoClip`.
