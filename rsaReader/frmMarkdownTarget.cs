namespace rsaReader;

internal sealed class FormMarkdownTarget : Form {
    private readonly TextBox _txtDefinition = new();
    private readonly TextBox _txtUrl = new();
    private string _definition = "";
    private string _url = "";

    private FormMarkdownTarget(bool image, string definition) {
        Text = image ? "Image" : "Link";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(440, 228);

        Controls.Add(new Label {
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(408, 36),
            Text = image
                ? "Enter the definition and URL for the image."
                : "Enter the definition and URL for the link."
        });

        Controls.Add(new Label {
            AutoSize = true,
            Location = new Point(16, 60),
            Text = "Definition"
        });
        _txtDefinition.Location = new Point(16, 80);
        _txtDefinition.Size = new Size(408, 23);
        _txtDefinition.Text = definition;
        Controls.Add(_txtDefinition);

        Controls.Add(new Label {
            AutoSize = true,
            Location = new Point(16, 116),
            Text = "URL"
        });
        _txtUrl.Location = new Point(16, 136);
        _txtUrl.Size = new Size(408, 23);
        Controls.Add(_txtUrl);

        var btnCancel = new Button {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 28),
            Location = new Point(ClientSize.Width - 16 - 88, ClientSize.Height - 16 - 28)
        };
        var btnOk = new Button {
            Text = "Insert",
            Size = new Size(88, 28),
            Location = new Point(btnCancel.Left - 8 - 88, btnCancel.Top)
        };
        btnOk.Click += (_, _) => AcceptTarget();
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        ActiveControl = _txtDefinition;
    }

    public static (string Definition, string Url)? Prompt(IWin32Window owner, bool image, string definition) {
        definition = definition.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        using var form = new FormMarkdownTarget(image, definition);
        if (form.ShowDialog(owner) != DialogResult.OK)
            return null;

        var result = (form._definition, form._url);
        form._definition = "";
        form._url = "";
        return result;
    }

    private void AcceptTarget() {
        var definition = _txtDefinition.Text.Trim();
        var url = _txtUrl.Text.Trim();
        if (definition.Length == 0) {
            MessageBox.Show(this, "Enter a definition.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtDefinition.Focus();
            return;
        }

        if (url.Length == 0) {
            MessageBox.Show(this, "Enter a URL.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtUrl.Focus();
            return;
        }

        _definition = definition;
        _url = url;
        _txtDefinition.Clear();
        _txtUrl.Clear();
        DialogResult = DialogResult.OK;
    }
}
