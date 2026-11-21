namespace rsaReader;

internal static class AppSession {
    /// <summary>
    /// SHA-256 hex of the phrase with suffix appended. Memory only; never written to disk.
    /// </summary>
    public static string DecryptPassword { get; private set; } = "";

    public static void SetDecryptPassword(string decryptPassword) => DecryptPassword = decryptPassword;

    public static void Clear() => DecryptPassword = "";
}
