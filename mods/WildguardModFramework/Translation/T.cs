namespace WildguardModFramework.Translation {
    /// <summary>
    /// Translate delegate. Call as t("key") or t("key", ("var", value)).
    /// Returned by <see cref="TranslationService.For"/>.
    /// </summary>
    public delegate string T(string key, params (string Key, object Value)[] args);
}
