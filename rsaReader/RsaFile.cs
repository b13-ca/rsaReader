using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rsaReader;

internal static class RsaFile {
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static Package Read(string path) {
        string json;
        try {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new IOException(path + Environment.NewLine + Environment.NewLine + ex.Message, ex);
        }

        Package? package;
        try {
            package = JsonSerializer.Deserialize<Package>(json, JsonOptions);
        }
        catch (JsonException) {
            throw new InvalidDataException("This .rsa file is not valid JSON:" + Environment.NewLine + path);
        }

        if (package is null
            || !PhraseHash.IsSha256Hex(package.Key ?? "")
            || string.IsNullOrWhiteSpace(package.Nonce)
            || string.IsNullOrWhiteSpace(package.Content))
            throw new InvalidDataException("This .rsa file does not contain Key, Nonce, and Content:" + Environment.NewLine + path);

        return package;
    }

    public static string Decrypt(Package package, string aesKeyHex) {
        var key = PhraseHash.AesKeyBytes(aesKeyHex);
        try {
            var nonce = Convert.FromBase64String(package.Nonce ?? "");
            var blob = Convert.FromBase64String(package.Content ?? "");
            if (nonce.Length != NonceSize || blob.Length < TagSize)
                throw new InvalidDataException("This .rsa file has an invalid Nonce or Content.");

            var cipherLength = blob.Length - TagSize;
            var cipher = blob.AsSpan(0, cipherLength);
            var tag = blob.AsSpan(cipherLength, TagSize);
            var plain = new byte[cipherLength];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or AuthenticationTagMismatchException or CryptographicException) {
            throw new InvalidDataException("The file could not be decrypted.", ex);
        }
        finally {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static void Write(string path, string keyHex, string aesKeyHex, string markdown) {
        var key = PhraseHash.AesKeyBytes(aesKeyHex);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        try {
            var plain = Encoding.UTF8.GetBytes(markdown);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];
            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, plain, cipher, tag);

            var blob = new byte[cipher.Length + tag.Length];
            cipher.CopyTo(blob, 0);
            tag.CopyTo(blob, cipher.Length);

            var json = JsonSerializer.Serialize(new Package {
                Key = keyHex,
                Nonce = Convert.ToBase64String(nonce),
                Content = Convert.ToBase64String(blob)
            }, JsonOptions);

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new IOException("Could not save the file:" + Environment.NewLine + path + Environment.NewLine + Environment.NewLine + ex.Message, ex);
        }
        finally {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(nonce);
        }
    }

    internal sealed class Package {
        [JsonPropertyName("Key")]
        public string? Key { get; set; }

        [JsonPropertyName("Nonce")]
        public string? Nonce { get; set; }

        [JsonPropertyName("Content")]
        public string? Content { get; set; }
    }
}
