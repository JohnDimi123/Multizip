using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Multizip.App.Forms
{
    /// <summary>
    /// Lightweight preview for common file types pulled out of an archive: plain text
    /// (and source/markup) in a read-only box, images in a zoomable picture box.
    /// </summary>
    public sealed class PreviewForm : Form
    {
        private static readonly string[] TextExt =
        {
            ".txt", ".log", ".md", ".json", ".xml", ".csv", ".ini", ".cfg", ".yml", ".yaml",
            ".cs", ".js", ".ts", ".html", ".htm", ".css", ".c", ".cpp", ".h", ".py", ".java",
            ".sql", ".bat", ".ps1", ".sh", ".gitignore", ".config"
        };
        private static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".webp" };

        public static bool CanPreview(string name)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return TextExt.Contains(ext) || ImageExt.Contains(ext);
        }

        public PreviewForm(string filePath, string displayName)
        {
            Text = "Preview - " + displayName;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterParent;
            AppServices.Theme.Apply(this);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ImageExt.Contains(ext)) ShowImage(filePath);
            else ShowText(filePath);
        }

        private void ShowImage(string path)
        {
            var pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            try { pic.Image = Image.FromFile(path); }
            catch (Exception ex) { ShowError(ex); return; }
            Controls.Add(pic);
        }

        private void ShowText(string path)
        {
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9.5f),
                BackColor = AppServices.Theme.WindowBackground,
                ForeColor = AppServices.Theme.WindowForeground
            };
            try
            {
                const long max = 4 * 1024 * 1024; // cap preview at 4 MB
                var fi = new FileInfo(path);
                if (fi.Length > max)
                {
                    var buffer = new char[max];
                    using (var reader = new StreamReader(path))
                        reader.Read(buffer, 0, buffer.Length);
                    box.Text = new string(buffer) + "\r\n\r\n--- preview truncated ---";
                }
                else
                {
                    box.Text = File.ReadAllText(path);
                }
            }
            catch (Exception ex) { ShowError(ex); return; }
            Controls.Add(box);
        }

        private void ShowError(Exception ex)
        {
            Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Could not preview file:\n" + ex.Message, Padding = new Padding(12) });
        }
    }
}
