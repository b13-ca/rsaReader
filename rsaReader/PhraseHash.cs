using System.Security.Cryptography;
using System.Text;

namespace rsaReader;

internal static class PhraseHash {
    private const string DecryptSuffix = "1972-09-04";

    public static string Of(string phrase) => ToHex(Sha256(phrase));

    public static string DecryptPassword(string phrase) => ToHex(Sha256(phrase + DecryptSuffix));

    public static bool Matches(string phrase, string storedKey) {
        var actual = Sha256(phrase);
        byte[] expected;
        try {
            expected = Convert.FromHexString(storedKey);
        }
        catch (FormatException) {
            return false;
        }

        if (expected.Length != actual.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool HexEquals(string left, string right) {
        byte[] actual;
        byte[] expected;
        try {
            actual = Convert.FromHexString(left);
            expected = Convert.FromHexString(right);
        }
        catch (FormatException) {
            return false;
        }

        if (actual.Length != expected.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static byte[] AesKeyBytes(string hex) {
        var key = Convert.FromHexString(hex);
        if (key.Length != 32)
            throw new InvalidDataException("The encryption key is not 32 bytes.");
        return key;
    }

    public static bool IsSha256Hex(string value) {
        if (value.Length != 64)
            return false;

        try {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException) {
            return false;
        }
    }

    private static byte[] Sha256(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

    private static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
