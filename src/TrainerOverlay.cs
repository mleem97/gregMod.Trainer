// Trainer panel content, built on the standalone TrainerPanel (UI Toolkit) which
// reproduces the gregCore panel look without depending on gregCore. Same
// architecture as gregMod.Backplanes/gregMod.MusicPlayer: theme styles, explicit
// Action<ClickEvent> callbacks (IL2CPP rule) plus manual click routing as
// fallback for the missing EventSystem, and the game font via TrainerPanel.
//
// Deliberately no TextField anywhere: this game strips TextEditor APIs used by
// text input controls (hard crash), so all amounts are steered by buttons.
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;

namespace GregModTrainer
{
    public static class TrainerOverlay
    {
        private sealed class Clickable
        {
            public VisualElement Element;
            public Action Action;
        }

        private static VisualElement _content;
        private static DateTime _lastRealClickUtc = DateTime.MinValue;
        private static bool _registered;
        private static readonly List<Clickable> _clickables = new List<Clickable>();
        private static readonly List<Label> _labels = new List<Label>();

        // Stored element refs so live values/rate amounts can be updated without
        // rebuilding the panel (rebuilding would reset buttons mid-click).
        private static Label _moneyVal;
        private static Label _xpVal;
        private static Label _repVal;
        private static Label _xpRateAmount;
        private static Label _incomeRateAmount;
        private static Button _xpRateBtn;
        private static Button _incomeRateBtn;
        private static Button _noExpBtn;

        public static bool IsVisible => TrainerPanel.IsVisible;

        public static void EnsureRegistered() { _registered = true; }

        public static void Toggle()
        {
            if (!_registered) return;
            try
            {
                if (!TrainerPanel.EnsureRoot())
                {
                    MelonLogger.Error("[Trainer] Panel not available.");
                    return;
                }
                bool willShow = !TrainerPanel.IsVisible;
                if (willShow) BuildContent();
                TrainerPanel.Toggle();
                MelonLogger.Msg($"[Trainer] Panel {(TrainerPanel.IsVisible ? "shown." : "hidden.")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Trainer] Panel toggle failed: {ex.GetBaseException().Message}");
            }
        }

        // Jeden Frame aus TrainerMod.OnUpdate: leitet Mausklicks an sichtbare
        // Buttons weiter (Ersatz fuers fehlende EventSystem).
        public static void RouteClicks()
        {
            if (!IsVisible || _clickables.Count == 0) return;
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null) return;
                if (!mouse.leftButton.wasPressedThisFrame) return;
                Vector2 pos = TrainerPanel.ToPanelSpace(mouse.position.ReadValue());
                try
                {
                    if ((DateTime.UtcNow - _lastRealClickUtc).TotalMilliseconds < 500.0) return;
                }
                catch { /* best-effort */ }
                for (int i = _clickables.Count - 1; i >= 0; i--)
                {
                    var c = _clickables[i];
                    if (c == null || c.Element == null || c.Action == null) continue;
                    try
                    {
                        if (!c.Element.visible) continue;
                        Rect b = c.Element.worldBound;
                        if (b.width <= 0f || b.height <= 0f) continue;
                        if (b.Contains(pos))
                        {
                            try { _lastRealClickUtc = DateTime.UtcNow; } catch { }
                            c.Action();
                            return;
                        }
                    }
                    catch { /* best-effort */ }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Trainer] Click routing failed: {ex.Message}");
            }
        }

        // Live-Werte + Toggle-Zustaende aktualisieren (kein Panel-Rebuild).
        public static void RefreshLabels()
        {
            if (!IsVisible) return;
            try
            {
                var player = PlayerRef.Current;
                if (_moneyVal != null)
                    _moneyVal.text = player == null ? "Money: —" : $"Money: {player.money:F0}";
                if (_xpVal != null)
                    _xpVal.text = player == null ? "XP: —" : $"XP: {player.xp:F0}";
                if (_repVal != null)
                    _repVal.text = player == null ? "Reputation: —" : $"Reputation: {player.reputation:F1}";
                if (_xpRateAmount != null)
                    _xpRateAmount.text = $"Amount: {TrainerMod.XpPerSecEntry.Value:F0}/s";
                if (_incomeRateAmount != null)
                    _incomeRateAmount.text = $"Amount: {TrainerMod.IncomePerSecEntry.Value:F0}/s";
                RefreshToggleBtn(_xpRateBtn, TrainerMod.XpPerSecEnabledEntry.Value);
                RefreshToggleBtn(_incomeRateBtn, TrainerMod.IncomePerSecEnabledEntry.Value);
                RefreshToggleBtn(_noExpBtn, TrainerMod.NoExpensesNow);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Trainer] Label refresh failed: {ex.Message}");
            }
        }

        private static void RefreshToggleBtn(Button btn, bool on)
        {
            if (btn == null) return;
            try
            {
                btn.text = on ? "ON" : "OFF";
                if (on) { TrainerPanel.ApplyPrimaryButtonStyle(btn); }
                else { TrainerPanel.ApplySecondaryButtonStyle(btn); }
            }
            catch { /* best-effort */ }
        }

        private static void BuildContent()
        {
            _content = TrainerPanel.Content;
            if (_content == null) return;

            _content.Clear();
            _clickables.Clear();
            _labels.Clear();
            _moneyVal = _xpVal = _repVal = null;
            _xpRateAmount = _incomeRateAmount = null;
            _xpRateBtn = _incomeRateBtn = _noExpBtn = null;

            AddHeadline("Player");
            _moneyVal = AddValueLabel("Money: —");
            _xpVal = AddValueLabel("XP: —");
            _repVal = AddValueLabel("Reputation: —");

            AddSeparator();

            AddHeadline("Economy");
            AddStepperRow("Money", 1000f, 10000f, 100000f, TrainerMod.AddCoin);
            AddStepperRow("XP", 1000f, 10000f, 100000f, TrainerMod.AddXp);
            AddStepperRow("Reputation", 10f, 100f, 500f, TrainerMod.AddReputation);

            AddSeparator();

            AddHeadline("Rates (per second)");
            _xpRateBtn = AddToggleBtn("XP/s", "Continuously grants the configured XP each second while the panel is open.",
                TrainerMod.XpPerSecEnabledEntry.Value, () => TrainerMod.ToggleXpPerSec());
            _xpRateAmount = AddSteppers("XP/s amount", TrainerMod.XpPerSecEntry.Value, 100f, 1000f, TrainerMod.StepXpPerSec);

            _incomeRateBtn = AddToggleBtn("Income/s", "Continuously grants the configured money each second while the panel is open.",
                TrainerMod.IncomePerSecEnabledEntry.Value, () => TrainerMod.ToggleIncomePerSec());
            _incomeRateAmount = AddSteppers("Income/s amount", TrainerMod.IncomePerSecEntry.Value, 100f, 1000f, TrainerMod.StepIncomePerSec);

            AddSeparator();

            AddHeadline("Protection");
            _noExpBtn = AddToggleBtn("No Expenses", "Blocks all money deductions (shop purchases, repairs, salaries).",
                TrainerMod.NoExpensesNow, () => TrainerMod.ToggleNoExpenses());

            AddSeparator();

            AddBtn(_content, $"Close ({TrainerMod.ToggleKey})", () => { try { Toggle(); } catch { /* best-effort */ } }, false);
            ApplyFont();
            RefreshLabels();
        }

        // ── Build helpers (styled like the other GregMods) ────────────────────

        private static void AddHeadline(string text)
        {
            var l = new Label(text.ToUpper());
            TrainerPanel.ApplyTextStyle(l, true);
            l.style.marginTop = 8f;
            l.style.marginBottom = 4f;
            _labels.Add(l);
            _content.Add(l);
        }

        private static Label AddValueLabel(string text)
        {
            var l = new Label(text);
            TrainerPanel.ApplyTextStyle(l, false);
            l.style.marginBottom = 3f;
            _labels.Add(l);
            _content.Add(l);
            return l;
        }

        // Resource row: -step / +small / +medium / +large grant buttons.
        private static void AddStepperRow(string title, float small, float medium, float large, Action<float> apply)
        {
            var row = Row();
            var label = new Label(title);
            TrainerPanel.ApplyTextStyle(label, false);
            label.style.flexGrow = 1f;
            label.style.alignSelf = Align.Center;
            _labels.Add(label);
            row.Add(label);

            AddBtn(row, $"-{FormatNum(small)}", () => ApplyStep(apply, -small), false, 72f);
            AddBtn(row, $"+{FormatNum(small)}", () => ApplyStep(apply, small), false, 72f);
            AddBtn(row, $"+{FormatNum(medium)}", () => ApplyStep(apply, medium), false, 72f);
            AddBtn(row, $"+{FormatNum(large)}", () => ApplyStep(apply, large), false, 72f);
            _content.Add(row);
        }

        // Amount label + small/medium step buttons, returns the label that shows the amount.
        private static Label AddSteppers(string title, float current, float smallStep, float largeStep, Action<float> step)
        {
            var row = Row();
            var label = new Label($"{title}: {current:F0}");
            TrainerPanel.ApplyTextStyle(label, false);
            label.style.flexGrow = 1f;
            label.style.alignSelf = Align.Center;
            _labels.Add(label);
            row.Add(label);

            AddBtn(row, $"-{FormatNum(largeStep)}", () => step(-largeStep), false, 72f);
            AddBtn(row, $"-{FormatNum(smallStep)}", () => step(-smallStep), false, 72f);
            AddBtn(row, $"+{FormatNum(smallStep)}", () => step(smallStep), false, 72f);
            AddBtn(row, $"+{FormatNum(largeStep)}", () => step(largeStep), false, 72f);
            _content.Add(row);
            return label;
        }

        private static void ApplyStep(Action<float> apply, float delta)
        {
            try { apply(delta); RefreshLabels(); }
            catch (Exception ex) { MelonLogger.Warning($"[Trainer] Action failed: {ex.Message}"); }
        }

        private static string FormatNum(float value)
        {
            if (value >= 100000f) return $"{value / 1000f:F0}k";
            if (value >= 10000f) return $"{value / 1000f:F0}k";
            return $"{value:F0}";
        }

        private static Button AddToggleBtn(string title, string hint, bool isOn, Action action)
        {
            var row = Row();
            var textCol = new VisualElement();
            textCol.style.flexGrow = 1f;
            var titleLabel = new Label(title);
            TrainerPanel.ApplyTextStyle(titleLabel, false);
            textCol.Add(titleLabel);
            var hintLabel = new Label(hint);
            TrainerPanel.ApplyTextStyle(hintLabel, false);
            textCol.Add(hintLabel);
            row.Add(textCol);

            var btn = new Button();
            btn.text = isOn ? "ON" : "OFF";
            btn.style.height = 34f;
            btn.style.width = 70f;
            RefreshToggleBtn(btn, isOn);
            RegisterBtn(btn, action);
            row.Add(btn);
            _content.Add(row);
            return btn;
        }

        private static void AddBtn(VisualElement parent, string label, Action action, bool primary, float width = 0f)
        {
            var btn = new Button();
            btn.text = label;
            btn.style.height = 34f;
            btn.style.marginBottom = 6f;
            if (width > 0f) btn.style.width = width;
            else btn.style.flexGrow = 1f;
            if (primary) TrainerPanel.ApplyPrimaryButtonStyle(btn);
            else TrainerPanel.ApplySecondaryButtonStyle(btn);
            RegisterBtn(btn, action);
            parent.Add(btn);
        }

        private static void RegisterBtn(Button btn, Action action)
        {
            try
            {
                btn.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
                {
                    try { _lastRealClickUtc = DateTime.UtcNow; action?.Invoke(); } catch { /* best-effort */ }
                }));
            }
            catch { /* fallback routing below */ }
            _clickables.Add(new Clickable { Element = btn, Action = action });
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 6f;
            return row;
        }

        private static void AddSeparator()
        {
            var sep = new VisualElement();
            sep.style.height = 2f;
            sep.style.backgroundColor = TrainerPanel.NeutralBorder;
            sep.style.marginTop = 6f;
            sep.style.marginBottom = 6f;
            _content.Add(sep);
        }

        private static void ApplyFont()
        {
            Font f = null;
            try { f = TrainerPanel.GameFont; } catch { /* best-effort */ }
            if (f == null) return;
            try
            {
                foreach (var l in _labels)
                {
                    try { if (l != null) l.style.unityFont = f; } catch { /* best-effort */ }
                }
                foreach (var c in _clickables)
                {
                    try
                    {
                        var b = c != null ? c.Element as Button : null;
                        if (b != null)
                        {
                            b.style.unityFont = f;
                            b.style.unityFontStyleAndWeight = FontStyle.Bold;
                        }
                    }
                    catch { /* best-effort */ }
                }
            }
            catch { /* best-effort */ }
        }
    }
}