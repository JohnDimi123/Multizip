using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Multizip.App.Util;
using Multizip.Core.Providers;

namespace Multizip.App.Forms
{
    /// <summary>About box: version, active providers and 7-Zip engine status.</summary>
    public sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About Multizip";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(440, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AppServices.Theme.Apply(this);

            var icon = new PictureBox { Image = Glyphs.NewArchive(48), Left = 16, Top = 16, Width = 48, Height = 48, SizeMode = PictureBoxSizeMode.Zoom };
            Controls.Add(icon);

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            Controls.Add(new Label { Text = "Multizip", Left = 76, Top = 16, Width = 340, Font = new Font("Segoe UI", 14f, FontStyle.Bold) });
            Controls.Add(new Label { Text = "Version " + v, Left = 78, Top = 46, Width = 340 });

            Controls.Add(new Label
            {
                Left = 16, Top = 76, Width = 410, Height = 36,
                Text = "The all-in-one archive manager with a classic Windows look. " +
                       "Open, create, browse and test archives in dozens of formats."
            });

            var info = new TextBox
            {
                Left = 16, Top = 116, Width = 408, Height = 156,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5f), Text = BuildInfo()
            };
            Controls.Add(info);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 348, Top = 282, Width = 76, Height = 28, FlatStyle = FlatStyle.System };
            Controls.Add(ok);
            AcceptButton = ok;
        }

        private static string BuildInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Engine providers:");
            foreach (var p in AppServices.Engine.Providers)
                sb.AppendLine($"  - {p.Name} (priority {p.Priority})");
            sb.AppendLine();
            sb.AppendLine("7-Zip engine: " + (SevenZipProvider.IsAvailable
                ? "loaded (" + SevenZipProvider.LibraryPath + ")"
                : "not found - using managed fallback"));
            sb.AppendLine();
            sb.AppendLine("Creatable formats:");
            sb.AppendLine("  " + string.Join(", ", AppServices.Engine.WritableFormats.Select(f => f.ToString())));
            sb.AppendLine();
            sb.AppendLine("Third-party: 7-Zip (LGPL), SharpCompress (MIT),");
            sb.AppendLine("SevenZipSharp (LGPL), Newtonsoft.Json (MIT).");
            return sb.ToString();
        }
    }
}
