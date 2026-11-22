using System.Runtime.InteropServices;

namespace rsaReader;

sealed class EditorTextBox : TextBox {
    private const int WM_PASTE = 0x0302;

    protected override void WndProc(ref Message m) {
        if (m.Msg == WM_PASTE && TryPasteNormalized())
            return;

        base.WndProc(ref m);
    }

    private bool TryPasteNormalized() {
        string text;
        try {
            if (!Clipboard.ContainsText())
                return false;

            text = Clipboard.GetText();
        }
        catch (ExternalException) {
            return false;
        }

        SelectedText = NormalizeLineEndings(text);
        return true;
    }

    // The native edit control drops a break unless it is CR+LF.
    // Clipboard text is often LF-only or CR-only.
    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
}
