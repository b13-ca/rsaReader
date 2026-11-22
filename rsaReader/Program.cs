namespace rsaReader;

internal static class Program {
    [STAThread]
    static int Main(string[] args) {
        ApplicationConfiguration.Initialize();

        try {
            string? decryptPassword;
            try {
                decryptPassword = AccessGate.Acquire();
            } catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                MessageBox.Show(ex.Message, "rsaReader", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }

            if (decryptPassword is null) {
                return 0;
            }

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
                    var opened = FormMain.ReadDocument(null, documentPath);
                    if (opened is null)
                        documentPath = null;
                    else {
                        data = opened.Value.Data;
                        keyHex = opened.Value.KeyHex;
                        aesKeyHex = opened.Value.AesKeyHex;
                    }
                }
            }

            var main = new FormMain(documentPath, data, keyHex, aesKeyHex);
            main.ShowPrepared();
            Application.Run(main);
        } catch {
            //
        }

        return 0;
    }
}

