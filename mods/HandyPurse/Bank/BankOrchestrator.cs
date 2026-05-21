using RR.UI.Popups;
using RR.UI.UISystem;

namespace HandyPurse.Bank {
    internal static class BankOrchestrator {
        internal static string PendingPopupText { get; private set; }

        internal static void ShowPendingPopup() {
            if (string.IsNullOrEmpty(PendingPopupText)) { return; }
            var text = PendingPopupText;
            PendingPopupText = null;
            UIManager.Instance?.Popup?.ShowCustom(null, new DefaultOKPopup {
                Title = HandyPurseMod.t("popup.funds_banked.title"),
                Text = text,
            });
        }

        internal static void AppendPendingPopup(string text) {
            if (string.IsNullOrEmpty(PendingPopupText)) {
                PendingPopupText = text;
            } else {
                PendingPopupText += "\n\n" + text;
            }
        }
    }
}
