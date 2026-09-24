using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(GregModTrainer.TrainerMod), "gregMod.Trainer", "1.1.1", "TeamGreg Modding")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace GregModTrainer
{
    // Runtime gregCore detection (type-name lookup only, no hard dependency).
    internal static class TrainerGregHost
    {
        private const string ProbeType = "gregCore.UI.GregNotificationManager, gregCore";
        private static bool? _hasCore;

        public static bool HasCore
        {
            get
            {
                if (_hasCore == null)
                {
                    try { _hasCore = System.Type.GetType(ProbeType) != null; }
                    catch { _hasCore = false; }
                }
                return _hasCore.Value;
            }
        }
    }

    /// <summary>
    /// gregMod.Trainer — player trainer (Reputation / XP / XP per second /
    /// Money / Income per second / No Expenses). UI lives in TrainerOverlay
    /// (standalone UI Toolkit panel with the same theme as the gregCore-based
    /// mods, but without any gregCore dependency). All value changes go through
    /// the game's own economy methods so the HUD and the save stay consistent.
    /// </summary>
    public sealed class TrainerMod : MelonMod
    {
        internal static TrainerMod Instance { get; private set; }

        // ── Preferences ───────────────────────────────────────────────────────
        private static MelonPreferences_Entry<string> ToggleKeyEntry;
        internal static MelonPreferences_Entry<bool> NoExpensesEntry;
        internal static MelonPreferences_Entry<bool> XpPerSecEnabledEntry;
        internal static MelonPreferences_Entry<float> XpPerSecEntry;
        internal static MelonPreferences_Entry<bool> IncomePerSecEnabledEntry;
        internal static MelonPreferences_Entry<float> IncomePerSecEntry;

        internal static Key ToggleKey = Key.F7;
        internal static bool NoExpensesNow = false;

        private float _valuesRefreshAt;

        public override void OnInitializeMelon()
        {
            try
            {
                Instance = this;

                var cat = MelonPreferences.CreateCategory("gregMod.Trainer", "Trainer");
                ToggleKeyEntry = cat.CreateEntry("ToggleKey", "F7", "Trainer Toggle Key",
                    "Input System key to open/close the trainer panel (e.g. F7, F8, Backquote).");
                NoExpensesEntry = cat.CreateEntry("NoExpenses", false, "No Expenses",
                    "Blocks all money deductions (shop purchases, repairs, salaries).");
                XpPerSecEnabledEntry = cat.CreateEntry("XpPerSecEnabled", false, "XP/s",
                    "Continuously grants the configured XP each second.");
                XpPerSecEntry = cat.CreateEntry("XpPerSec", 1000f, "XP per second",
                    "Amount of XP granted per second while XP/s is enabled.");
                IncomePerSecEnabledEntry = cat.CreateEntry("IncomePerSecEnabled", false, "Income/s",
                    "Continuously grants the configured amount of money each second.");
                IncomePerSecEntry = cat.CreateEntry("IncomePerSec", 500f, "Income per second",
                    "Amount of money granted per second while Income/s is enabled.");
                cat.SaveToFile(false);

                if (Enum.TryParse<Key>(ToggleKeyEntry.Value, true, out var k) && k != Key.None)
                    ToggleKey = k;
                else
                    LoggerInstance.Warning($"[Trainer] Unknown key '{ToggleKeyEntry.Value}', defaulting to F7.");

                NoExpensesNow = NoExpensesEntry.Value;

                TrainerOverlay.EnsureRegistered();
                LoggerInstance.Msg($"[Trainer] Loaded. Press {ToggleKey} to open the trainer panel.");
                if (TrainerGregHost.HasCore)
                {
                    try { RegisterCoreExtras(); } catch { }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"[Trainer] OnInitializeMelon failed: {ex.GetBaseException().Message}");
            }
        }

        // Mod contract + key HUD + opener for F1 hub. Call only with gregCore
        // (own method for JIT split without gregCore DLL).
        private void RegisterCoreExtras()
        {
            try
            {
                gregCore.Core.Mods.GregModRegistry.Register(
                    "gregMod.Trainer", "Trainer", "1.1.1",
                    new string[] { "trainer" });
                gregCore.UI.GregHudRegistry.Register("trainer", ToggleKey.ToString(), "Trainer");
                gregCore.UI.GregMenuRegistry.RegisterOpener("trainer", () =>
                {
                    try { TrainerOverlay.Toggle(); } catch { }
                });
                gregCore.UI.GregMenuRegistry.RegisterCloser("trainer", () =>
                {
                    try { if (TrainerOverlay.IsVisible) TrainerOverlay.Toggle(); } catch { }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Trainer] Hub registration failed: " + ex.GetBaseException().Message);
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            PlayerRef.Clear();
        }

        public override void OnUpdate()
        {
            try { TrainerInputLock.Refresh(); } catch { /* best-effort */ }
            try { TrainerOverlay.RouteClicks(); } catch { /* best-effort */ }

            try
            {
                var kb = Keyboard.current;
                if (kb != null && kb[ToggleKey].wasPressedThisFrame && !IsPauseMenuActive())
                    TrainerOverlay.Toggle();
            }
            catch { /* input best-effort */ }

            if (TrainerOverlay.IsVisible)
            {
                TrainerPanel.Tick();
                ApplyPerSecond();
                try
                {
                    if (Time.unscaledTime >= _valuesRefreshAt)
                    {
                        _valuesRefreshAt = Time.unscaledTime + 0.25f;
                        TrainerOverlay.RefreshLabels();
                    }
                }
                catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// Applies the configured per-second grants scaled by the frame delta.
        /// Uses the game's own update methods so UI and the save stay consistent.
        /// </summary>
        internal static void ApplyPerSecond()
        {
            try
            {
                var player = PlayerRef.Current;
                if (player == null) return;

                if (XpPerSecEnabledEntry.Value && XpPerSecEntry.Value != 0f)
                    player.UpdateXP(XpPerSecEntry.Value * Time.deltaTime);

                if (IncomePerSecEnabledEntry.Value && IncomePerSecEntry.Value != 0f)
                    player.UpdateCoin(IncomePerSecEntry.Value * Time.deltaTime, true, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Trainer] Per-second grant failed: {ex.Message}");
            }
        }

        // ── Panel actions ──────────────────────────────────────────────────────

        internal static void AddCoin(float amount)
        {
            var player = PlayerRef.Current;
            if (player == null) return;
            try { player.UpdateCoin(amount, true, true); }
            catch (Exception ex) { MelonLogger.Warning($"[Trainer] Money action failed: {ex.Message}"); }
        }

        internal static void AddXp(float amount)
        {
            var player = PlayerRef.Current;
            if (player == null) return;
            try { player.UpdateXP(amount); }
            catch (Exception ex) { MelonLogger.Warning($"[Trainer] XP action failed: {ex.Message}"); }
        }

        internal static void AddReputation(float amount)
        {
            var player = PlayerRef.Current;
            if (player == null) return;
            try { player.UpdateReputation(amount); }
            catch (Exception ex) { MelonLogger.Warning($"[Trainer] Reputation action failed: {ex.Message}"); }
        }

        internal static void ToggleNoExpenses()
        {
            ToggleBool(NoExpensesEntry, value => NoExpensesNow = value);
        }

        internal static void ToggleXpPerSec()
        {
            ToggleBool(XpPerSecEnabledEntry, null);
        }

        internal static void ToggleIncomePerSec()
        {
            ToggleBool(IncomePerSecEnabledEntry, null);
        }

        internal static void StepXpPerSec(float delta)
        {
            if (XpPerSecEntry == null) return;
            try
            {
                XpPerSecEntry.Value = Math.Max(0f, XpPerSecEntry.Value + delta);
                MelonPreferences.Save();
            }
            catch { /* best-effort */ }
        }

        internal static void StepIncomePerSec(float delta)
        {
            if (IncomePerSecEntry == null) return;
            try
            {
                IncomePerSecEntry.Value = Math.Max(0f, IncomePerSecEntry.Value + delta);
                MelonPreferences.Save();
            }
            catch { /* best-effort */ }
        }

        private static void ToggleBool(MelonPreferences_Entry<bool> pref, Action<bool> onChanged)
        {
            if (pref == null) return;
            try
            {
                pref.Value = !pref.Value;
                MelonPreferences.Save();
                Instance?.LoggerInstance?.Msg($"[Trainer] {pref.DisplayName} = {pref.Value}");
                try { onChanged?.Invoke(pref.Value); } catch { /* best-effort */ }
            }
            catch (Exception ex)
            {
                Instance?.LoggerInstance?.Error($"[Trainer] Toggle failed: {ex.Message}");
            }
        }

        // Settings hub tab and mod-registry were intentionally left out:
        // this mod is dependency-free (no gregCore) like gregMod.NoClip, so the
        // trainer panel is the only UI surface.

        /// <summary>True while a game pause/settings canvas is on screen.</summary>
        internal static bool IsPauseMenuActive()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Canvas>();
                if (all == null) return false;
                foreach (var c in all)
                {
                    if (c == null || !c.isActiveAndEnabled) continue;
                    var go = c.gameObject;
                    if (go == null) continue;
                    if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                    if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                    var n = go.name ?? "";
                    if (n.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("EscapeMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("InGameMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("SystemMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("OptionsMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("SettingsMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { /* best-effort */ }
            return false;
        }
    }

    /// <summary>
    /// Blocks all money deductions while "No Expenses" is enabled.
    /// Grants (positive amounts) are never touched, only negative deltas are swallowed.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdateCoin")]
    internal static class PatchNoExpenses
    {
        [HarmonyPrefix]
        private static bool Prefix(Player __instance, ref float _coinChhangeAmount)
        {
            try
            {
                if (__instance == null) return true;
                if (TrainerMod.NoExpensesNow && _coinChhangeAmount < 0f)
                {
                    // Let the game see an overdraft-free no-op so downstream math stays valid.
                    _coinChhangeAmount = 0f;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Trainer] No-Expenses hook failed: {ex.Message}");
            }
            return true;
        }
    }

    /// <summary>
    /// Resolves the local player from the game's PlayerManager singleton.
    /// Cached but cleared when the scene changes so stale pointers are never used.
    /// </summary>
    internal static class PlayerRef
    {
        private static Player _cached;

        internal static Player Current
        {
            get
            {
                try
                {
                    var pm = PlayerManager.instance;
                    if (pm == null) return null;
                    _cached = pm.playerClass;
                    return _cached;
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static void Clear()
        {
            _cached = null;
        }
    }
}