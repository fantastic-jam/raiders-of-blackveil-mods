using HandyPurse.Bank;
using HarmonyLib;
using RR.UI.Pages;

namespace HandyPurse.Patch {
    public static class HandyPursePatch {
        public static void ApplyMenuHook(Harmony harmony) {
            var menuActivateMethod = AccessTools.Method(typeof(MenuStartPage), "OnActivate");
            if (menuActivateMethod != null) {
                harmony.Patch(menuActivateMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(MenuStartPageOnActivatePostfix))));
            }
        }

        private static void MenuStartPageOnActivatePostfix() =>
            BankOrchestrator.ShowPendingPopup();
    }
}
