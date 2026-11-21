namespace rsaReader;

internal static class AccessGate {
    public static string? Acquire() {
        if (!KeyStore.Exists)
            return Initialize();

        return Unlock(KeyStore.ReadKey());
    }

    private static string? Initialize() {
        var phrase = FormPhrase.Prompt(initialize: true);
        if (phrase is null)
            return null;

        KeyStore.Save(PhraseHash.Of(phrase));
        return PhraseHash.DecryptPassword(phrase);
    }

    private static string? Unlock(string storedKey) {
        return FormPhrase.Prompt(initialize: false, phrase => PhraseHash.Matches(phrase, storedKey)) is { } phrase
            ? PhraseHash.DecryptPassword(phrase)
            : null;
    }
}
