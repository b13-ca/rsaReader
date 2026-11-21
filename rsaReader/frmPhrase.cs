namespace rsaReader;

internal sealed class FormPhrase : Form {
    private readonly TextBox _txtPhrase = new() { UseSystemPasswordChar = true };
    private readonly TextBox? _txtConfirm;
    private readonly Func<string, bool>? _verify;
    private string _acceptedPhrase = "";

    private FormPhrase(bool initialize, Func<string, bool>? verify) {
        _verify = verify;

        Text = initialize ? "Initialize rsaReader" : "rsaReader";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(440, initialize ? 248 : 168);

        var intro = new Label {
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(408, initialize ? 52 : 36),
            Text = initialize
                ? "Enter a phrase to initialize rsaReader. A SHA-256 hash of the phrase is saved. The phrase itself is not stored."
                : "Enter the phrase to open rsaReader."
        };
        Controls.Add(intro);

        var y = initialize ? 76 : 60;
        Controls.Add(new Label {
            AutoSize = true,
            Location = new Point(16, y),
            Text = "Phrase"
        });
        y += 20;
        _txtPhrase.Location = new Point(16, y);
        _txtPhrase.Size = new Size(408, 23);
        Controls.Add(_txtPhrase);
        y += 32;

        if (initialize) {
            Controls.Add(new Label {
                AutoSize = true,
                Location = new Point(16, y),
                Text = "Confirm phrase"
            });
            y += 20;
            _txtConfirm = new TextBox {
                UseSystemPasswordChar = true,
                Location = new Point(16, y),
                Size = new Size(408, 23)
            };
            Controls.Add(_txtConfirm);
        }

        var btnCancel = new Button {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 28),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Location = new Point(ClientSize.Width - 16 - 88, ClientSize.Height - 16 - 28)
        };
        var btnOk = new Button {
            Text = initialize ? "Save" : "Unlock",
            Size = new Size(88, 28),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Location = new Point(btnCancel.Left - 8 - 88, btnCancel.Top)
        };
        btnOk.Click += (_, _) => AcceptPhrase();
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.Add(btnOk);
        Controls.Add(btnCancel);

        if (initialize) {
            var chkShowPhrase = new CheckBox {
                AutoSize = true,
                Text = "Show phrase",
                Location = new Point(16, btnCancel.Top + 4)
            };
            chkShowPhrase.CheckedChanged += (_, _) => SetPhraseVisible(chkShowPhrase.Checked);
            Controls.Add(chkShowPhrase);
        }

        ActiveControl = _txtPhrase;
    }

    private void SetPhraseVisible(bool visible) {
        _txtPhrase.UseSystemPasswordChar = !visible;
        if (_txtConfirm is not null)
            _txtConfirm.UseSystemPasswordChar = !visible;
    }

    public static string? Prompt(bool initialize, Func<string, bool>? verify = null) {
        using var form = new FormPhrase(initialize, verify);
        if (form.ShowDialog() != DialogResult.OK)
            return null;

        var phrase = form._acceptedPhrase;
        form._acceptedPhrase = "";
        return phrase;
    }

    private void AcceptPhrase() {
        var phrase = _txtPhrase.Text;
        if (phrase.Length == 0) {
            MessageBox.Show(this, "Enter a phrase.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtPhrase.Focus();
            return;
        }

        if (_txtConfirm is not null && !string.Equals(phrase, _txtConfirm.Text, StringComparison.Ordinal)) {
            MessageBox.Show(this, "The phrases do not match.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtConfirm.Focus();
            _txtConfirm.SelectAll();
            return;
        }

        if (_verify is not null && !_verify(phrase)) {
            MessageBox.Show(this, "The phrase does not match.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPhrase.Focus();
            _txtPhrase.SelectAll();
            return;
        }

        _acceptedPhrase = phrase;
        _txtPhrase.Clear();
        _txtConfirm?.Clear();
        DialogResult = DialogResult.OK;
    }
}
