namespace rsaReader;

public partial class FormFileKey : Form {
    private string _storedKey = "";
    private string _phrase = "";

    public FormFileKey() {
        InitializeComponent();
    }

    public FormFileKey(string storedKey, string fileName) : this() {
        _storedKey = storedKey;
        lblIntro.Text = "Enter the passphrase for " + fileName + ".";
    }

    public static string? Prompt(IWin32Window owner, string storedKey, string fileName) {
        using var form = new FormFileKey(storedKey, fileName);
        if (form.ShowDialog(owner) != DialogResult.OK)
            return null;

        var phrase = form._phrase;
        form._phrase = "";
        return phrase;
    }

    private void chkShow_CheckedChanged(object sender, EventArgs e) {
        txtPhrase.UseSystemPasswordChar = !chkShow.Checked;
    }

    private void btnOk_Click(object sender, EventArgs e) {
        if (txtPhrase.Text.Length == 0) {
            MessageBox.Show(this, "Enter a passphrase.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtPhrase.Focus();
            return;
        }

        if (!PhraseHash.Matches(txtPhrase.Text, _storedKey)) {
            MessageBox.Show(this, "The phrase does not match.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtPhrase.Focus();
            txtPhrase.SelectAll();
            return;
        }

        _phrase = txtPhrase.Text;
        txtPhrase.Clear();
        DialogResult = DialogResult.OK;
    }
}
