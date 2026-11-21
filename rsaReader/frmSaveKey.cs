namespace rsaReader;

public partial class FormSaveKey : Form {
    private bool _useDefault = true;
    private string _phrase = "";

    public FormSaveKey() {
        InitializeComponent();
    }

    public static (bool UseDefault, string Phrase)? Prompt(IWin32Window owner) {
        using var form = new FormSaveKey();
        if (form.ShowDialog(owner) != DialogResult.OK)
            return null;

        var phrase = form._phrase;
        form._phrase = "";
        return (form._useDefault, phrase);
    }

    private void radPhrase_CheckedChanged(object sender, EventArgs e) {
        var custom = radPhrase.Checked;
        txtPhrase.Enabled = custom;
        chkShow.Enabled = custom;
        if (custom)
            txtPhrase.Focus();
    }

    private void chkShow_CheckedChanged(object sender, EventArgs e) {
        txtPhrase.UseSystemPasswordChar = !chkShow.Checked;
    }

    private void btnOk_Click(object sender, EventArgs e) {
        if (radPhrase.Checked && txtPhrase.Text.Length == 0) {
            MessageBox.Show(this, "Enter a passphrase.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtPhrase.Focus();
            return;
        }

        _useDefault = radDefault.Checked;
        _phrase = txtPhrase.Text;
        txtPhrase.Clear();
        DialogResult = DialogResult.OK;
    }
}
