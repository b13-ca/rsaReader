using System.Text.Json;
using System.Text.Json.Serialization;

namespace rsaReader;

internal static class KeyStore {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "rsaReader");

    public static string FilePath { get; } = Path.Combine(DirectoryPath, "rsaReader.json");

    public static bool Exists => File.Exists(FilePath);

    public static string ReadKey() {
        string json;
        try {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new IOException(FileError("Could not read the key file", ex), ex);
        }

        Document? document;
        try {
            document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
        }
        catch (JsonException) {
            throw new InvalidDataException($"The key file is not valid JSON:{Environment.NewLine}{FilePath}");
        }

        var key = document?.Key;
        if (string.IsNullOrWhiteSpace(key) || !PhraseHash.IsSha256Hex(key))
            throw new InvalidDataException($"The key file does not contain a SHA-256 Key:{Environment.NewLine}{FilePath}");

        return key;
    }

    public static void Save(string keyHex) {
        var json = JsonSerializer.Serialize(new Document { Key = keyHex }, JsonOptions);
        var temporary = FilePath + ".tmp";

        try {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(temporary, json);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new IOException(FileError("Could not save the key file", ex), ex);
        }
    }

    private static string FileError(string action, Exception ex) =>
        $"{action}:{Environment.NewLine}{FilePath}{Environment.NewLine}{Environment.NewLine}{ex.Message}";

    private sealed class Document {
        [JsonPropertyName("Key")]
        public string? Key { get; set; }
    }
}
