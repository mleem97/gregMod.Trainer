// Translation bridge to gregCore.Lang. Probe + reflection only — this file
// must never reference gregCore types directly, so the mod keeps zero hard
// references and runs standalone (English defaults) without gregCore.dll.
// Same pattern as TrainerGregHost.
using System;

namespace GregModTrainer
{
    /// <summary>
    /// Mod-side bridge into the central translation service
    /// (<c>gregCore.Lang.GregLang, gregCore</c>). Tables live in
    /// <c>Mods/Data/gregMod.Trainer/&lt;lang&gt;.json</c>; without gregCore
    /// every call returns the embedded English default.
    /// </summary>
    internal static class TrainerLang
    {
        private const string ModId = "gregMod.Trainer";
        private const string LangTypeName = "gregCore.Lang.GregLang, gregCore";

        private static bool _probed;
        private static System.Reflection.MethodInfo _t3;
        private static System.Reflection.MethodInfo _t4;
        private static bool _subscribed;
        private static DateTime _nextRetryUtc = DateTime.MinValue;

        internal static string T(string key, string englishDefault)
        {
            try
            {
                EnsureProbed();
                if (_t3 != null)
                {
                    var result = _t3.Invoke(null, new object[] { ModId, key, englishDefault });
                    if (result is string s && !string.IsNullOrEmpty(s))
                        return s;
                }
            }
            catch { /* fall through to default */ }
            return englishDefault;
        }

        internal static string T(string key, string englishDefault, params object[] args)
        {
            try
            {
                EnsureProbed();
                if (_t4 != null)
                {
                    var result = _t4.Invoke(null, new object[] { ModId, key, englishDefault, args ?? new object[0] });
                    if (result is string s && !string.IsNullOrEmpty(s))
                        return s;
                }
            }
            catch { /* fall through to default */ }

            if (args != null && args.Length > 0)
            {
                try { return string.Format(englishDefault, args); }
                catch { /* keep raw default */ }
            }
            return englishDefault;
        }

        /// <summary>
        /// Runs <paramref name="refresh"/> after gregCore reports a language
        /// switch. No-op without gregCore. Best-effort, never throws.
        /// </summary>
        internal static void RefreshOnLanguageChanged(Action refresh)
        {
            if (refresh == null) return;
            try
            {
                if (_subscribed) return;
                EnsureProbed();
                var type = System.Type.GetType(LangTypeName);
                var evt = type != null ? type.GetEvent("LanguageChanged") : null;
                if (evt == null) return;
                System.Delegate handler;
                try
                {
                    handler = refresh.Target == null
                        ? System.Delegate.CreateDelegate(evt.EventHandlerType, refresh.Method)
                        : System.Delegate.CreateDelegate(evt.EventHandlerType, refresh.Target, refresh.Method);
                }
                catch { return; }
                if (handler == null) return;
                evt.AddEventHandler(null, handler);
                _subscribed = true;
            }
            catch { /* best-effort */ }
        }

        // One-shot once gregCore is found; throttled retries before that so
        // translations kick in even when gregCore loads after this mod.
        private static void EnsureProbed()
        {
            if (_probed) return;
            try
            {
                var type = System.Type.GetType(LangTypeName);
                if (type == null)
                {
                    try
                    {
                        if (DateTime.UtcNow < _nextRetryUtc) return;
                        _nextRetryUtc = DateTime.UtcNow.AddSeconds(5);
                    }
                    catch { return; }
                    return;
                }
                try
                {
                    _t3 = type.GetMethod("T", new[] { typeof(string), typeof(string), typeof(string) });
                }
                catch { /* best-effort */ }
                try
                {
                    _t4 = type.GetMethod("T", new[] { typeof(string), typeof(string), typeof(string), typeof(object[]) });
                }
                catch { /* best-effort */ }
                _probed = true;
            }
            catch { /* standalone mode */ }
        }
    }
}
