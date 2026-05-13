# 03 — Localization

---

Prerequisite: [02-settings-panel.md](02-settings-panel.md)

Replace the hardcoded English strings in the settings panel with `TranslationService` lookups so the mod can be translated without recompiling.

## How it works

`TranslationService` (in `WildguardModFramework.Translation`) loads flat JSON files from the plugin's `Assets/Localization/` folder at startup. You get back a `T` delegate that resolves keys to strings — falling back to `"en"` and then to the key itself if no translation is found.

Resolution order: **current language → `"en"` → key literal**.

## Step 1: Create the English translation file

Create `Assets/Localization/PlayerEmotes.en.json`:

```json
{
  "mod.description": "Send floating text reactions visible to all players.",
  "menu.emote_key": "Emote Key"
}
```

File naming: `{ModName}.{lang}.json` — `PlayerEmotes.en.json` for English, `PlayerEmotes.fr.json` for French, etc. The locale code matches `AppManager.Instance.PlayerSettings.Gen_Language`.

## Step 2: Declare assets in the csproj

Add a `Content` item group so the files are copied to the build output and included in the release ZIP:

```xml
<ItemGroup>
  <Content Include="Assets\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

## Step 3: Initialise the translation delegate

In `PlayerEmotesMod.cs`, add a static `t` field and call `TranslationService.For()` early in `Awake()` — before the try/catch, so `t` is always usable.

```csharp
using WildguardModFramework.Translation;

internal static T t;

private void Awake() {
    PublicLogger = Logger;
    t = TranslationService.For(Name, Info.Location);
    CfgEmoteKey = Config.Bind("Controls", "EmoteKey", Key.T, "Key to send an emote.");
    try {
        // … Init, Enable, log …
    }
    catch (Exception ex) { PublicLogger.LogError(ex); }
}
```

> **`Info.Location`** is the full path to your plugin's DLL, provided by BepInEx. `TranslationService` derives `Assets/Localization/` from it.

> **`T`** is a delegate type: `delegate string T(string key, params (string Key, object Value)[] args)`. The args are optional named placeholders — `t("key", ("player", name))` replaces `{player}` in the value string.

## Step 4: Replace hardcoded strings

In `PlayerEmotesMod.cs`, replace the literal description:

```csharp
public string GetModDescription() => t("mod.description");
```

In `UI/PlayerEmotesMenu.cs`, replace the `CustomTransform` literal:

```csharp
internal void Build(VisualElement container) {
    var row = new OptionButton();
    var lbl = row.Q<LocLabel>("Label");
    if (lbl != null) {
        lbl.CustomTransform = _ => PlayerEmotesMod.t("menu.emote_key");
        lbl.Refresh();
    }
    // … rest unchanged …
}
```

## Result

The mod description and settings label are now driven by `PlayerEmotes.en.json`. To add French, create `Assets/Localization/PlayerEmotes.fr.json` with the same keys — no code change needed.

---

## Next

→ [04-local-labels.md](04-local-labels.md) — Show a floating label above the local player on keypress
