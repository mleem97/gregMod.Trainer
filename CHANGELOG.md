# Changelog — gregMod.Trainer

Format: [Keep a Changelog](https://keepachangelog.com/de/1.0.0/).

## [Unreleased]

### Added

- Uebersetzungen via gregCore.Lang (Pilot-Mod): alle UI-Strings laufen ueber
  `TrainerLang` (Probe + Reflection, keine harte gregCore-Referenz).
  Tabellen in `data/gregMod.Trainer/en.json` (Pflicht, Fallback) und
  `de.json`; Deploy via `./build.sh Trainer --deploy` nach
  `Mods/Data/gregMod.Trainer/`. Ohne gregCore weiterhin englische Defaults.
- Toggle runs via the central gregCore keybind registry when present
  (auto-resolve on collision; HUD shows the effective key). Without
  gregCore, unchanged own polling.

## [1.1.1] — 2026-09-24

### Changed

- English strings throughout.

## [1.1.0] — 2026-09-24

### Changed

- Default-Toggle-Key von F9 auf F7 geaendert (F9 ist der MusicPlayer-Hotkey; bestehende cfgs behalten ihren Wert).

### Added

- Mod-Vertrag, Tasten-HUD-Eintrag und Oeffner fuers F1-Hub (nur mit gregCore).
