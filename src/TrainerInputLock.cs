// Input lock for trainer window: while panel is visible,
// cursor stays free and game inputs disabled (PlayerManager
// flags + PlayerInput components). So camera/movement do NOT
// keep going while panel is open, and mouse clicks reach only the panel
// (game would re-lock cursor each frame, breaking manual
// click routing). Same behavior as
// gregCore.UI.GregInputLock (Backplanes/MusicPlayer), but without gregCore
// dependency — value fields verified against Il2Cpp.PlayerManager.
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GregModTrainer
{
    internal static class TrainerInputLock
    {
        private static readonly List<PlayerInput> Suspended = new List<PlayerInput>();
        private static bool _applied;
        private static float _nextRescanRealtime;

        // Per frame from TrainerMod.OnUpdate: applies/releases lock automatically.
        public static void Refresh()
        {
            try
            {
                if (!TrainerOverlay.IsVisible)
                {
                    if (_applied) Restore();
                    return;
                }
                if (!_applied) Apply();
                else ForceCursor();
                SuspendNew();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Trainer] InputLock-Refresh failed: {ex.Message}");
            }
        }

        private static void Apply()
        {
            _applied = true;
            ForceCursor();
            SetPlayerManager(false, false, false);
            SuspendAll();
        }

        private static void Restore()
        {
            _applied = false;
            ResumeAll();
            SetPlayerManager(true, true, true);
            try
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            catch { /* best-effort */ }
        }

        private static void ForceCursor()
        {
            try
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch { /* best-effort */ }
        }

        private static void SetPlayerManager(bool mouse, bool movement, bool rayInteract)
        {
            try
            {
                var pm = Il2Cpp.PlayerManager.instance;
                if (pm == null) return;
                try { pm.enabledMouseMovement = mouse; } catch { /* tolerance */ }
                try { pm.enabledPlayerMovement = movement; } catch { /* tolerance */ }
                try { pm.enabledRayLookInteract = rayInteract; } catch { /* tolerance */ }
            }
            catch { /* best-effort */ }
        }

        private static void SuspendAll()
        {
            Suspended.Clear();
            SuspendNew();
        }

        // Also collect later-spawned PlayerInputs (call repeatedly).
        // Full object search is costly: max 1x/2s.
        private static void SuspendNew()
        {
            float now = 0f;
            try { now = Time.realtimeSinceStartup; } catch { return; }
            if (now < _nextRescanRealtime) return;
            _nextRescanRealtime = now + 2f;
            PlayerInput[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<PlayerInput>(); } catch { /* best-effort */ }
            if (all == null) return;
            foreach (var pi in all)
            {
                if (pi == null || Suspended.Contains(pi)) continue;
                try
                {
                    var go = pi.gameObject;
                    if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                }
                catch { continue; }
                try
                {
                    try
                    {
                        var asset = pi.actions;
                        if (asset != null && asset.enabled) asset.Disable();
                    }
                    catch { /* best-effort */ }
                    pi.DeactivateInput();
                    Suspended.Add(pi);
                }
                catch { /* best-effort */ }
            }
        }

        private static void ResumeAll()
        {
            foreach (var pi in Suspended)
            {
                if (pi == null) continue;
                try { pi.ActivateInput(); } catch { /* best-effort */ }
                try
                {
                    InputActionAsset asset = null;
                    try { asset = pi.actions; } catch { /* best-effort */ }
                    if (asset != null && !asset.enabled) asset.Enable();
                }
                catch { /* best-effort */ }
            }
            Suspended.Clear();
        }
    }
}