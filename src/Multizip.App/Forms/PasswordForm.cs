using System;
using System.Drawing;
using System.Windows.Forms;

namespace Multizip.App.Forms
{
    /// <summary>Tiny modal prompt for an archive password.</summary>
    public sealed class PasswordForm : Form
    {
        private readonly TextBox _box;

        public string Password => _box.Text;

        public PasswordForm(string archiveName)
        {
            Text = "Password Required";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 130);
            AppServices.Theme.Apply(this);

            Controls.Add(new Label { Text = $"'{archiveName}' is encrypted.\nEnter the password:", Left = 12, Top = 12, Width = 336, Height = 36 });

            _box = new TextBox { Left = 12, Top = 54, Width = 336, UseSystemPasswordChar = true };
            var show = new CheckBox { Text = "Show", Left = 12, Top = 82, Width = 70 };
            show.CheckedChanged += (s, e) => _box.UseSystemPasswordChar = !show.Checked;

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 192, Top = 92, Width = 74, FlatStyle = FlatStyle.System };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 274, Top = 92, Width = 74, FlatStyle = FlatStyle.System };

            Controls.Add(_box);
            Controls.Add(show);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
