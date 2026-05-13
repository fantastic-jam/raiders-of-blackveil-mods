# 04 — Local Labels

---

Prerequisite: [03-localization.md](03-localization.md)

Detect the emote keypress and show a floating text label above the local player. No networking yet — only the pressing player sees the label.

## Step 1: Wire keypress detection into the HUD patch

A standalone `MonoBehaviour.Update()` on a `DontDestroyOnLoad` object does not reliably receive input in this game. The correct pattern (same as BeginnersWelcome) is to poll the keyboard inside a Harmony postfix on `BaseHUDPage.OnUpdate` — a game-managed update that runs every frame while a HUD page is active (i.e. during a run).

Create `EmoteInput.cs` as a **static class** (no `MonoBehaviour`):

```csharp
using UnityEngine.InputSystem;

namespace PlayerEmotes {
    internal static class EmoteInput {
        internal static void OnUpdate() {
            if (Keyboard.current[PlayerEmotesMod.CfgEmoteKey.Value].wasPressedThisFrame)
                EmoteController.TriggerLocal();
        }
    }
}
```

Then wire it into `PlayerEmotesPatch.cs`. Resolve `BaseHUDPage.OnUpdate` in `Init()`, patch it in `Patch()`, and call `EmoteInput.OnUpdate()` from the postfix:

```csharp
using System.Reflection;
using HarmonyLib;
using RR.UI.Pages;

namespace PlayerEmotes.Patch {
    internal static class PlayerEmotesPatch {
        private static Harmony _harmony;
        private static bool _patched;
        private static MethodInfo _hudOnUpdate;

        internal static bool Init() {
            _hudOnUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (_hudOnUpdate == null) {
                PlayerEmotesMod.PublicLogger.LogWarning("PlayerEmotes: BaseHUDPage.OnUpdate not found — emote key unavailable.");
            }
            return true;
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;
            if (_hudOnUpdate != null) {
                _harmony.Patch(_hudOnUpdate, postfix: new HarmonyMethod(typeof(PlayerEmotesPatch), nameof(OnHUDUpdatePostfix)));
            }
            PlayerEmotesMod.PublicLogger.LogInfo("PlayerEmotes patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }

        private static void OnHUDUpdatePostfix() {
            EmoteInput.OnUpdate();
        }
    }
}
```

> No `Disabled` guard is needed in the postfix — `Unpatch()` removes the hook entirely when the mod is disabled.

> Do **not** create a `new GameObject` or call `AddComponent<EmoteInput>()` in `Awake()`. The MonoBehaviour approach doesn't work here; the patch is sufficient.

## Step 2: Spawn the floating label

Create `EmoteController.cs`. The game already has a pooled floating-text system (`EffectText`) used for damage numbers. Use it directly — no custom Canvas, no coroutine needed.

- `EffectText.Allocate(text, size, color)` grabs a pooled label, sets text and style, and starts its own float-and-fade animation (~0.6 s).
- `UIManager.Instance.RegisterOverlayElement(element, gameObject, offset3D, offset2D)` pins the label to a world-space `GameObject` so it follows the character as they move.
- `LocalPlayer.PlayableChampion` is the champion interface; cast it to `MonoBehaviour` to get its `gameObject`.

```csharp
using RR;
using RR.UI.Controls.HUD.Overlay;
using RR.UI.UISystem;
using UnityEngine;

namespace PlayerEmotes {
    internal static class EmoteController {
        internal static void TriggerLocal() {
            var champion = PlayerManager.Instance?.LocalPlayer?.PlayableChampion as MonoBehaviour;
            if (champion == null) { return; }

            var label = EffectText.Allocate("Hi!", 48f, Color.white);
            UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
        }
    }
}
```

## Result

Press the emote key during a run. "Hi!" floats upward above the local player and fades out over roughly half a second. No other player sees it yet.

---

## Next

→ [05-networking.md](05-networking.md) — Broadcast the emote to all players in the session
