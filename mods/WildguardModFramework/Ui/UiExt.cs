using System;
using UnityEngine.UIElements;

namespace WildguardModFramework.Ui {
    /// <summary>
    /// IL2CPP helpers for UI Toolkit.
    /// Interop delegate types (<c>EventCallback&lt;T&gt;</c>, <c>Il2CppSystem.Action&lt;T&gt;</c>) only convert
    /// implicitly from a typed <c>System.Action&lt;T&gt;</c>, never from a bare lambda — these helpers
    /// give the lambda its type so call sites stay readable.
    /// </summary>
    internal static class UiExt {
        /// <summary>RegisterCallback with a managed lambda.</summary>
        internal static void On<TEvent>(this CallbackEventHandler handler, Action<TEvent> callback)
            where TEvent : EventBase<TEvent>, new() {
            handler.RegisterCallback<TEvent>(callback);
        }
    }
}
