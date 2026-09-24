// Standalone UI Toolkit panel that reproduces the gregCore "GregPanel" look
// (IPAM design language: deep-navy surface, slate border, teal header, blue
// CTA buttons) WITHOUT depending on gregCore. Own canvas host (GameObject +
// UIDocument + PanelSettings), own drag handling, own cursor management and a
// best-effort game-font lookup. Click delivery = manual hit-routing by
// TrainerOverlay (the game ships without a working EventSystem).
using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;

namespace GregModTrainer
{
    internal static class TrainerPanel
    {
        // --- Theme (copied from gregCore.UI.GregUITheme / IPAM design language) ---
        internal static readonly Color PrimaryAccent = new Color(0.04f, 0.64f, 0.75f);
        internal static readonly Color PrimaryTextOnAccent = new Color(0.02f, 0.07f, 0.12f);
        internal static readonly Color SecondaryColor = new Color(0.53f, 0.81f, 0.92f);
        internal static readonly Color NeutralBorder = new Color(0.14f, 0.17f, 0.22f, 0.80f);
        internal static readonly Color SurfaceDark = new Color(0.08f, 0.10f, 0.13f, 0.98f);
        internal static readonly Color TextPrimary = new Color(0.92f, 0.94f, 0.96f);
        internal static readonly float CornerRadius = 8f;
        internal static readonly float Padding = 16f;

        internal static void ApplyTextStyle(Label label, bool isHeadline = false)
        {
            label.style.fontSize = isHeadline ? 20 : 14;
            label.style.color = isHeadline ? TextPrimary : new Color(0.88f, 0.88f, 0.88f);
            label.style.unityFontStyleAndWeight = isHeadline ? FontStyle.Bold : FontStyle.Normal;
        }

        internal static void ApplyPrimaryButtonStyle(Button button)
        {
            button.style.backgroundColor = PrimaryAccent;
            button.style.color = PrimaryTextOnAccent;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = CornerRadius;
            button.style.borderTopRightRadius = CornerRadius;
            button.style.borderBottomLeftRadius = CornerRadius;
            button.style.borderBottomRightRadius = CornerRadius;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        internal static void ApplySecondaryButtonStyle(Button button)
        {
            button.style.backgroundColor = new Color(0.11f, 0.13f, 0.17f);
            button.style.color = new Color(0.85f, 0.87f, 0.90f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = CornerRadius;
            button.style.borderTopRightRadius = CornerRadius;
            button.style.borderBottomLeftRadius = CornerRadius;
            button.style.borderBottomRightRadius = CornerRadius;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        // --- Runtime state ---
        private static GameObject _host;
        private static UIDocument _uiDoc;
        private static VisualElement _root;
        private static VisualElement _panel;
        private static Label _dragHandle;
        private static bool _visible;
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static Font _font;

        internal const float PanelWidth = 440f;

        internal static VisualElement Content { get; private set; }
        internal static bool IsVisible => _visible;

        // Converts a screen-space point (input system, bottom-left origin)
        // to panel space (worldBound/top-left origin, incl. panel scaling).
        // Fallback to naive Y-flip if RuntimePanel not ready yet.
        internal static Vector2 ToPanelSpace(Vector2 screenPoint)
        {
            try
            {
                IPanel panel = _root != null ? _root.panel : null;
                if (panel != null) return RuntimePanelUtils.ScreenToPanel(panel, screenPoint);
            }
            catch { /* best-effort */ }
            try { return new Vector2(screenPoint.x, Screen.height - screenPoint.y); } catch { return screenPoint; }
        }

        internal static bool EnsureRoot()
        {
            try
            {
                if (_panel != null) return true;

                _host = new GameObject("Trainer_UIHost");
                UnityEngine.Object.DontDestroyOnLoad(_host);

                var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "Trainer_PanelSettings";
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                panelSettings.sortingOrder = 1000;

                _uiDoc = _host.AddComponent<UIDocument>();
                _uiDoc.panelSettings = panelSettings;
                _root = _uiDoc.rootVisualElement;
                _root.pickingMode = PickingMode.Position;
                _root.style.display = DisplayStyle.None;

                _panel = new VisualElement();
                _panel.name = "TrainerPanel";
                _panel.style.position = Position.Absolute;
                _panel.style.left = 16f;
                _panel.style.top = 60f;
                _panel.style.width = PanelWidth;
                _panel.style.backgroundColor = SurfaceDark;
                _panel.style.borderTopLeftRadius = CornerRadius;
                _panel.style.borderTopRightRadius = CornerRadius;
                _panel.style.borderBottomLeftRadius = CornerRadius;
                _panel.style.borderBottomRightRadius = CornerRadius;
                _panel.style.borderLeftWidth = 2f;
                _panel.style.borderRightWidth = 2f;
                _panel.style.borderTopWidth = 2f;
                _panel.style.borderBottomWidth = 2f;
                _panel.style.borderLeftColor = NeutralBorder;
                _panel.style.borderRightColor = NeutralBorder;
                _panel.style.borderTopColor = NeutralBorder;
                _panel.style.borderBottomColor = NeutralBorder;
                _panel.style.paddingLeft = Padding;
                _panel.style.paddingRight = Padding;
                _panel.style.paddingTop = 12f;
                _panel.style.paddingBottom = 12f;

                _dragHandle = new Label(TrainerLang.T("panel.title", "TRAINER"));
                ApplyTextStyle(_dragHandle, true);
                _panel.Add(_dragHandle);

                Content = new VisualElement();
                Content.name = "Content";
                Content.style.flexGrow = 1f;
                _panel.Add(Content);

                _root.Add(_panel);
                _root.style.display = DisplayStyle.Flex;
                _visible = false;
                Hide();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Trainer] Panel root failed: {ex.GetBaseException().Message}");
                return false;
            }
        }

        internal static void Show()
        {
            if (_panel == null) return;
            _visible = true;
            try { if (_root != null) _root.style.display = DisplayStyle.Flex; } catch { }
            RefreshCursor();
        }

        internal static void Hide()
        {
            _visible = false;
            _dragging = false;
            try { if (_root != null) _root.style.display = DisplayStyle.None; } catch { }
            RefreshCursor();
        }

        internal static void Toggle()
        {
            if (_visible) Hide();
            else Show();
        }

        // Called each frame from TrainerMod.OnUpdate while visible (cursor unlock
        // + drag head). Drag coordinates use UI Toolkit's top-left origin.
        internal static void Tick()
        {
            if (!_visible || _panel == null) return;
            try
            {
                if (UnityEngine.Cursor.lockState != CursorLockMode.None || !UnityEngine.Cursor.visible)
                    RefreshCursor();

                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null) return;
                Vector2 pos = ToPanelSpace(mouse.position.ReadValue());

                if (mouse.leftButton.wasPressedThisFrame && !_dragging)
                {
                    try
                    {
                        Rect b = _dragHandle.worldBound;
                        if (b.width > 0f && b.height > 0f && b.Contains(pos))
                        {
                            _dragging = true;
                            Rect r = _panel.worldBound;
                            _dragOffset = new Vector2(pos.x - r.x, pos.y - r.y);
                        }
                    }
                    catch { /* best-effort */ }
                    return;
                }
                if (_dragging)
                {
                    if (mouse.leftButton.isPressed)
                    {
                        float nx = pos.x - _dragOffset.x;
                        float ny = pos.y - _dragOffset.y;
                        if (nx < 0f) nx = 0f;
                        if (ny < 0f) ny = 0f;
                        _panel.style.left = nx;
                        _panel.style.top = ny;
                    }
                    else
                    {
                        _dragging = false;
                    }
                }
            }
            catch { /* best-effort */ }
        }

        private static void RefreshCursor()
        {
            try
            {
                if (_visible)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
            catch { /* best-effort */ }
        }

        // Toolkit default font is unusable in IL2CPP builds (invisible text),
        // so assign first loaded game font (even if several
        // modules find one it stays one, cached).
        internal static Font GameFont
        {
            get
            {
                if (_font != null) return _font;
                try
                {
                    var fonts = Resources.FindObjectsOfTypeAll<Font>();
                    if (fonts != null)
                        foreach (var f in fonts)
                            if (f != null) { _font = f; return _font; }
                }
                catch { /* best-effort */ }
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { /* best-effort */ }
                return _font;
            }
        }
    }
}