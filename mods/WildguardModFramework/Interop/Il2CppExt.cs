using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;

namespace WildguardModFramework.Interop {
    /// <summary>
    /// IL2CPP helpers for comparing game objects.
    ///
    /// A managed wrapper is not guaranteed to be unique per native object: the same game object can
    /// reach managed code through two different wrapper instances (e.g. typed as a base class in a
    /// Harmony patch, as the concrete class elsewhere). C# reference equality (<c>==</c> on non-Unity
    /// types, <c>Dictionary</c> default comparer) then gives false negatives. Compare native pointers.
    /// </summary>
    internal static class Il2CppExt {
        /// <summary>True when both wrappers point to the same native object (or both are null).</summary>
        internal static bool SameAs(this Il2CppObjectBase a, Il2CppObjectBase b) {
            if (a is null || b is null) { return a is null && b is null; }
            return a.Pointer == b.Pointer;
        }

        /// <summary>
        /// Copies an IL2CPP sequence into a managed list. Interop IEnumerator&lt;T&gt; does not expose
        /// MoveNext (interface inheritance is flattened), so neither foreach nor LINQ work on it directly.
        /// </summary>
        internal static List<T> ToManagedList<T>(this Il2CppSystem.Collections.Generic.IEnumerable<T> source) {
            var list = new List<T>();
            if (source == null) { return list; }
            var typed = source.GetEnumerator();
            var untyped = typed.Cast<Il2CppSystem.Collections.IEnumerator>();
            while (untyped.MoveNext()) { list.Add(typed.Current); }
            return list;
        }

        internal static System.Guid ToManaged(this Il2CppSystem.Guid guid) => System.Guid.Parse(guid.ToString());

        internal static Il2CppSystem.Guid ToIl2Cpp(this System.Guid guid) => Il2CppSystem.Guid.Parse(guid.ToString());
    }

    /// <summary>
    /// Remembers the last native instance seen. IL2CPP constructors can't be patched (Il2CppInterop has
    /// no native detour for them), so one-time page setup runs from a lifecycle hook (OnActivate) instead,
    /// gated by <see cref="IsNew"/>.
    /// </summary>
    internal sealed class InstanceTracker {
        private System.IntPtr _last;

        /// <summary>True the first time this native object is seen (since the previous one).</summary>
        internal bool IsNew(Il2CppObjectBase obj) {
            if (obj is null || obj.Pointer == _last) { return false; }
            _last = obj.Pointer;
            return true;
        }
    }

    /// <summary>Dictionary/HashSet comparer keyed by native object identity.</summary>
    internal sealed class NativeIdentityComparer<T> : IEqualityComparer<T> where T : Il2CppObjectBase {
        internal static readonly NativeIdentityComparer<T> Instance = new();

        public bool Equals(T x, T y) => x.SameAs(y);

        public int GetHashCode(T obj) => obj?.Pointer.GetHashCode() ?? 0;
    }
}
