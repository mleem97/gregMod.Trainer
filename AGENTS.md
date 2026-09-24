# AGENTS.md — Notes for AI agents (gregMod.Trainer)

Repo: gregMod.Trainer · License: Apache-2.0 · Version: see `VERSION` (1.1.1).

MelonMod for Data Center (`TrainerMod : MelonMod`, namespace
`GregModTrainer`). Player trainer: Reputation / XP / XP per second / Money /
Income per second / No Expenses. Panel key: **F7**. Standalone UI Toolkit
panel (same theme as the gregCore-based mods, but with **no hard** gregCore
references — type-name probes + reflection only). i18n pilot mod for
gregCore.Lang (see `src/TrainerLang.cs`, `data/gregMod.Trainer/`).

## Duties

1. **Read first:** `README.md`, `docs/SOURCE_LAYOUT.md`, `src/` — only then make changes.
2. **Do not commit secrets** (keys, tokens, `.env`). Use keys only via environment variables.
3. **Preserve history:** no `push --force`, no history rewrite without instruction.
4. **Verify changes:** before reporting done, build the mod (`dotnet build gregMod.Trainer.csproj -c Release` or `./build.sh Trainer` from `ModRepositories/`).
5. **Keep docs in sync:** for new features update `README.md` + `docs/` + `CHANGELOG.md` (Unreleased).
6. **Conventions:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), one logical change per commit.
7. **When unsure:** stop and ask instead of guessing — especially for deletes, migrations, CI.

## Build and references

- Target: `net6.0`, x64, `AllowUnsafeBlocks`. Game: Data Center (`MelonGame("Waseku", "Data Center")`).
- `references/` holds absolute symlinks into the Steam Data Center install.
  Never commit `references/*.dll`, `bin/`, or `obj/`.
- After a fresh clone, run `../tools/sync-melon-assemblies.sh`.
- Deploy only with `./build.sh Trainer --deploy`.

## Hard rules

- All value changes go through the game's own economy methods so HUD and
  save stay consistent — never write reputation/XP/money fields directly.
- **Zero hard gregCore references** (not even the Lang bridge): `TrainerGregHost`
  and `TrainerLang` use type-name probes + reflection only, with embedded
  English defaults. The mod must load and run fully without `gregCore.dll`.
- All user-facing strings go through `TrainerLang.T(key, englishDefault)` with
  namespaced keys (`panel.*`, `section.*`, `value.*`, `stepper.*`, `rate.*`,
  `toggle.*`, `pref.*`, `log.*`). `data/gregMod.Trainer/en.json` stays complete;
- **Panel key is F7** (F4 = FiberTrunk, F6 = Backplanes, F8 = MultiCable,
  F9 = MusicPlayer, F10 = NotesHUD). Do not collide.
- Input lock (`TrainerInputLock`) must always release on panel close,
  mod disable, and unload — never trap keyboard/mouse.
- Defensive `try/catch` in every per-frame path; no per-frame reflection.

## Layout

- `src/TrainerMod.cs` — MelonMod entry, prefs, economy hooks, `TrainerGregHost` probe.
- `src/TrainerLang.cs` — translation bridge (probe + reflection + embedded defaults).
- `data/gregMod.Trainer/en.json` (complete, fallback) + `de.json` — deployed to `Mods/Data/`.
- `src/TrainerPanel.cs` — value editors (routes through game methods).
- `src/TrainerOverlay.cs` — F7 overlay host.
- `src/TrainerInputLock.cs` — input capture/release.
