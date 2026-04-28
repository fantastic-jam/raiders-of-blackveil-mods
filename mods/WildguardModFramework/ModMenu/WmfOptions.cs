using System;
using UnityEngine.UIElements;
using WildguardModFramework.Chat;
using WildguardModFramework.PlayerManagement;
using WildguardModFramework.Registry;

namespace WildguardModFramework.ModMenu {
    internal static class WmfOptions {
        internal static RegisteredMod CreateRegisteredMod() =>
            new RegisteredMod(WmfMod.Id, WmfMod.Name, "WMF",
                new (string Title, Action<VisualElement, bool> Build)[] {
                    (WmfMod.t("options.chat.tab"),    (c, g) => ServerChat.BuildSettingsPanel(c, g)),
                    (WmfMod.t("options.players.tab"), (c, g) => PlayerManagementController.BuildBanListPanel(c, g)),
                });
    }
}
