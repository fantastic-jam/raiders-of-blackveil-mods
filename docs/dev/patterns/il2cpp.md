# IL2CPP modding

Since game 0.6.5 (Unity 6000.5, Nov 2026) Raiders of Blackveil ships as **IL2CPP**: game code is native
(`GameAssembly.dll`), there is no `RoB_Data/Managed/` folder, and mods run on **BepInEx 6 (Unity.IL2CPP)**
under CoreCLR (.NET 6). Mods compile against **Il2CppInterop wrapper assemblies** — managed proxies that
forward every call, field and property access to native code.

Read this before touching any mod code. It supersedes Mono-era advice elsewhere in `docs/` where they conflict.

## Toolchain

| Piece | Where | Notes |
|---|---|---|
| Loader | BepInEx 6 `be.793` (`bepinex/` — compile refs) | `BasePlugin`, `Load()`, `Log` |
| Dummy assemblies | `game-lib/il2cpp/dummy/` | Cpp2IL output: original signatures + attributes, empty bodies |
| Interop assemblies | `game-lib/il2cpp/interop/` | What mods reference (`$(InteropDir)` in `Common.props`) |
| `game-src/` | decompiled **dummy** `Assembly-CSharp` | Signatures, `[Rpc(Key=…)]`, field layout. **No method bodies.** |
| `game-lib/il2cpp/interop-src/` | decompiled **interop** `Assembly-CSharp` | The exact API mods see (property names, `Il2Cpp*` types) |
| `game-src-mono/` | last Mono decompile (game 0.6.4) | Only source of **method bodies**. Logic may have drifted since. |

`pnpm run setup` regenerates dummy + interop + decompiles whenever `GameAssembly.dll` or
`global-metadata.dat` changes (hash in `game-lib/il2cpp/.source.sha256`).

The game's metadata is **v107**. No Cpp2IL release reads it yet, so the tooling pins a Cpp2IL CI
build (`tools/lib/il2cpp.mts`), mirrored as a release of this repo (`cpp2il-b5ad444`) because CI artifacts expire. Stock BepInEx 6 `be.793` bundles an older Cpp2IL
and **fails at runtime** on this game ([BepInEx#1395](https://github.com/BepInEx/BepInEx/issues/1395)), and
newer Cpp2IL can't be loaded next to Il2CppInterop 1.5.3 (AsmResolver 6 clash,
[Il2CppInterop#295](https://github.com/BepInEx/Il2CppInterop/issues/295)).

### Our BepInEx build — `pnpm run build:bepinex`

Builds `dist/BepInEx-IL2CPP-RoB/` (+ `.zip`): stock be.793 with two patched DLLs (same assembly versions)
and a .NET Framework build of the pinned Cpp2IL in `BepInEx/cpp2il/`. **Verified in game (2026-11-26):
chainloader starts on 0.6.5.**

- `BepInEx.Unity.IL2CPP.dll` — [`external-cpp2il.patch`](../../../tools/bepinex/external-cpp2il.patch):
  runs that Cpp2IL **in a separate process** and loads its dummy DLLs with BepInEx's own AsmResolver (same
  split as `pnpm run setup`). Config: `[IL2CPP] ExternalCpp2ILPath` (empty = bundled Cpp2IL).
- `Il2CppInterop.Runtime.dll` — [`il2cppinterop-unity6000_5-class.patch`](../../../tools/bepinex/il2cppinterop-unity6000_5-class.patch):
  Unity 6000.5 reordered the native `Il2CppClass` struct (`events`/`properties`/`nestedTypes` now after
  `typeHierarchy`, `methods` right after `fields`). 1.5.3's newest handler is for 2023.1, so every class
  injection / delegate conversion crashed (`NullReferenceException` in `ClassInjector.RegisterTypeInIl2Cpp`).
  Adds `NativeClassStructHandler_29_3` for 6000.5+. Layout source: Il2CppDumper PR roytu/Il2CppDumper#5.

Remaining harmless warnings: `Class::Init signatures have been exhausted, using a substitute!` and two
preload failures (`UnityEngine.PhysicsCore2DModule`, `UnityEngine.MathematicsModule`). Drop the patches once
upstream BepInEx / Il2CppInterop support Unity 6000.5.

## Plugin entry point

```csharp
[BepInPlugin(Id, Name, Version)]
public class MyMod : BasePlugin {               // not BaseUnityPlugin
    public static ManualLogSource PublicLogger;
    public override void Load() {                // not Awake()
        PublicLogger = Log;                      // not Logger
        var dllPath = typeof(MyMod).Assembly.Location; // no Info.Location
    }
}
```

`Chainloader.PluginInfos` → `IL2CPPChainloader.Instance.Plugins`.

## Reading private game state — no reflection needed

Il2CppInterop exposes **every** field and method, private or not, as a **public** member of the wrapper
(fields become properties with the same name):

```csharp
var cursor = page._cursor;                       // was AccessTools.Field(typeof(MenuStartHostPage), "_cursor")
manager.CollectRaidInfoForBroadcast(out var a, ...); // was AccessTools.Method(...).Invoke
```

`AccessTools.Field(typeof(GameType), "_x")` **returns null** on interop types — it compiles but silently
breaks. Use the property. `AccessTools.Method` still works and is how patch targets are resolved.

Custom attributes (`[Rpc]`, `[Networked]`…) are **not** kept in interop assemblies. Read them from the
dummy assembly (`game-src/`) and hard-code with a comment.

## Type checks — `TryCast`, never `is` / `as`

A wrapper's C# type is whatever the call site declared, not the native runtime type. A Harmony patch on
`BaseHUDPage.OnUpdate` receives `__instance` as a `BaseHUDPage` wrapper even when the object is a
`GameHUDPage`, so `__instance is GameHUDPage` is **always false**.

```csharp
var gameHud = __instance.TryCast<GameHUDPage>();   // null if not that native type
var stop = toggle.Cast<ICursorLinearStopElement>(); // throws if not
```

`GetType()` returns the wrapper type; use `GetIl2CppType()` for the native one.

## Identity — compare native pointers

Two wrappers can point to the same native object. `==` on non-`UnityEngine.Object` types and the default
`Dictionary`/`HashSet` comparer use **managed reference identity** → false negatives.

```csharp
using WildguardModFramework.Interop;      // or copy Il2CppExt into your mod
if (a.SameAs(b)) { ... }                  // a.Pointer == b.Pointer
new Dictionary<BaseHUDPage, T>(NativeIdentityComparer<BaseHUDPage>.Instance);
```

**`ConditionalWeakTable<TGameType, …>` is broken** for the same reason (rule 3 in `CLAUDE.md` / `proxify.md`):
it keys on the wrapper, which can be collected and recreated while the native object lives on. Key per-instance
state by native pointer and clear it from a lifecycle hook (destroy / deactivate / session end).

`UnityEngine.Object` subclasses (`MonoBehaviour`, `GameObject`…) are fine with `==` — the interop keeps
Unity's overloaded equality.

## Interfaces become classes

Game interfaces (`ICursorLinearStopElement`, `IAllowClick`, `ITextSelection`…) are generated as **classes**.

- Passing a game object where an interface is expected: `obj.Cast<IAllowClick>()`.
- A managed class **cannot implement** a game interface. Don't subclass a game `VisualElement` to implement
  `ICursorLinearStopElement` — compose plain `VisualElement`s and drive them with managed code
  (see `ModsMenuOverlay.NavCursor` in WMF).
- Interface members that the game implements explicitly may be missing from the interface class
  (`ITextSelection.cursorColor`) — cast to the implementing class instead (`TextElement`).

## Delegates

Interop delegate types (`Il2CppSystem.Action<T>`, `EventCallback<T>`, `Func<,>`…) convert implicitly from a
**typed** `System.Action<T>` / `System.Func<…>`, never from a bare lambda or method group:

```csharp
toggle.OnValueChangedCallback = (Action<bool>)(v => ...);
label.SetLiteral("", customTransform: (Func<string, string>)(_ => t("key")));
el.schedule.Execute((Action)Refresh);
el.RegisterCallback<ClickEvent>((Action<ClickEvent>)(_ => ...));  // WMF: el.On<ClickEvent>(_ => ...)
new Button((Action)(() => ...));
```

## Collections, arrays, strings

| Game type | Interop type | Managed access |
|---|---|---|
| `List<T>` | `Il2CppSystem.Collections.Generic.List<T>` | `Count`, indexer, `Add`; no LINQ |
| `IEnumerable<T>` | `Il2CppSystem.Collections.Generic.IEnumerable<T>` | `.ToManagedList()` (WMF `Il2CppExt`) — interop `IEnumerator<T>` has no `MoveNext` |
| `T[]` (struct) | `Il2CppStructArray<T>` | implicit `T[]` ↔ conversions, `foreach` works |
| `T[]` (ref) | `Il2CppReferenceArray<T>` | same |
| `string[]` | `Il2CppStringArray` | implicit from `string[]` |
| `ReadOnlySpan<T>` | `Il2CppSystem.ReadOnlySpan<T>` | `.ToArray()` → `T[]` |
| `Guid` | `Il2CppSystem.Guid` | `.ToManaged()` / `.ToIl2Cpp()` (WMF `Il2CppExt`) |
| `string` | `string` | marshalled automatically |

Harmony patch parameters must use the **interop** types (`Il2CppStructArray<PlayerRef> raidPlayerRefs`, not
`PlayerRef[]`) — a mismatch compiles but the patch binds wrongly at runtime.

Blittable structs (`PlayerRef`, `NetworkBool`, `Vector3`) stay real C# structs. Structs with reference fields
(`StartGameArgs`, `RpcBuilder`) become **boxed classes** deriving `Il2CppSystem.ValueType`.

## MonoBehaviours and coroutines

Managed `MonoBehaviour` subclasses must be injected before use, and need an `IntPtr` constructor:

```csharp
internal sealed class Runner : MonoBehaviour {
    public Runner(IntPtr ptr) : base(ptr) { }
}
ClassInjector.RegisterTypeInIl2Cpp<Runner>();   // once, in Load()
go.AddComponent<Runner>();
```

Injected classes only expose a **parameterless** constructor to native code: the game can't instantiate them
itself (e.g. a `PageBase` subclass created by `UIDynamicPageLayer` via `Activator.CreateInstance(type, args)`).
Build such UI as a managed overlay instead (WMF `SoloModePicker`).

Coroutines: `runner.StartCoroutine(MyRoutine().WrapToIl2Cpp())`
(`using BepInEx.Unity.IL2CPP.Utils.Collections;`).

## Harmony

Unchanged API (`AccessTools.Method`, `harmony.Patch`, `__instance`, `ref __result`, prefix `return false`),
backed by native detours. Caveats:

- **Constructors can't be patched.** `harmony.Patch(AccessTools.Constructor(...))` logs
  `Failed to init IL2CPP patch backend for void X::.ctor(...), using normal patch handlers` and the patch
  never fires for objects the game creates. Pages (`MenuStartPage`, `GameHUDPage`…) build their UI in the
  `PageArgs` constructor: hook `OnActivate` instead and run one-time setup once per native instance
  (WMF `InstanceTracker.IsNew`, or a `NativeIdentityComparer` dictionary `ContainsKey` check).
  Any `Failed to init IL2CPP patch backend` line in the log = a dead patch.
- **Methods with `Span` / `ReadOnlySpan` parameters: no Harmony.** The prefix runs, but whenever the
  original must run Il2CppInterop calls it through `il2cpp_runtime_invoke`, whose invoker for byref-like
  parameters throws `NotImplementedException` (`During invoking native->managed trampoline`). The game
  then loses that call entirely. Use a native detour (`INativeDetour.CreateAndApply`, raw-pointer
  delegate, call the trampoline for the original) — see WMF `Network/ReliableDataDetour.cs`
  (needs a `MonoMod.RuntimeDetour` reference). Win x64: structs not 1/2/4/8 bytes are passed by pointer.
- **Inlining**: IL2CPP may inline small methods into their callers; a patch on an inlined method never fires.
- **Transpilers don't work** (there is no IL to transpile).
- Patching generic methods and methods with by-value non-blittable struct parameters is fragile — validate in game.

## Hot reload

UnityHotReload is Mono-only and was removed. There is no IL2CPP equivalent; restart the game to test.
