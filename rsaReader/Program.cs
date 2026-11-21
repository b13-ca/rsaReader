namespace rsaReader;

internal static class Program {
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static int Main(string[] args) {
        var subprocess = CefSharp.BrowserSubprocess.SelfHost.Main(args);
        if (subprocess >= 0)
            return subprocess;

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        try {
            string? decryptPassword;
            try {
                decryptPassword = AccessGate.Acquire();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                MessageBox.Show(ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }

            if (decryptPassword is null)
                return 0;

            AppSession.SetDecryptPassword(decryptPassword);
            var documentPath = args.Length > 0 ? args[0] : null;
            //string strDoc = @"D:\Work\Cursor\cSharp\rsaReader\basedOn\test.rsa";
            //documentPath = strDoc;
            string? data = null;
            string? keyHex = null;
            string? aesKeyHex = null;
            if (!string.IsNullOrWhiteSpace(documentPath)) {
                var emptyFile = File.Exists(documentPath) && new FileInfo(documentPath).Length == 0;
                if (!emptyFile) {
                    var opened = ReadDocument(documentPath);
                    if (opened is null)
                        documentPath = null;
                    else {
                        data = opened.Value.Data;
                        keyHex = opened.Value.KeyHex;
                        aesKeyHex = opened.Value.AesKeyHex;
                    }
                }
            }

            CefHost.Start();
            var main = new FormMain(documentPath, data, keyHex, aesKeyHex);
            main.ShowPrepared();
            Application.Run(main);
            return 0;
        }
        finally {
            CefHost.Stop();
        }
    }

    private static (string Data, string KeyHex, string AesKeyHex)? ReadDocument(string path) {
        RsaFile.Package package;
        try {
            package = RsaFile.Read(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) {
            MessageBox.Show(ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        var keyHex = package.Key ?? "";
        string aesKeyHex;
        try {
            if (PhraseHash.HexEquals(keyHex, KeyStore.ReadKey()))
                aesKeyHex = AppSession.DecryptPassword;
            else {
                var phrase = FormFileKey.Prompt(null!, keyHex, Path.GetFileName(path));
                if (phrase is null)
                    return null;
                aesKeyHex = PhraseHash.DecryptPassword(phrase);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
            MessageBox.Show(ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        try {
            return (RsaFile.Decrypt(package, aesKeyHex), keyHex, aesKeyHex);
        }
        catch (InvalidDataException ex) {
            MessageBox.Show(ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }
}