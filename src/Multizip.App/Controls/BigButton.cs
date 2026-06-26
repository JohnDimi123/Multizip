using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Multizip.App.Theme;

namespace Multizip.App.Controls
{
    /// <summary>
    /// The large, icon-over-caption launcher button used on the main window, styled
    /// after ImgBurn's home screen: a big glyph with a short title underneath and a
    /// gentle hover highlight. Owner-drawn so it stays crisp and classic-looking.
    /// </summary>
    public sealed class BigButton : Button
    {
        private bool _hover;
        private ThemeManager _theme;

        public BigButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(190, 110);
            Cursor = Cursors.Hand;
            Title = "Button";
        }

        /// <summary>The caption rendered under the glyph.</summary>
        public string Title { get; set; }

        /// <summary>Optional glyph image (24-48 px looks best).</summary>
        public Image Glyph { get; set; }

        public void UseTheme(ThemeManager theme) => _theme = theme;

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color back = Parent?.BackColor ?? SystemColors.Control;
            Color hoverBack = _theme?.HoverBackground ?? Color.FromArgb(229, 243, 255);
            Color border = _theme?.Accent ?? Color.FromArgb(0, 120, 215);
            Color text = _theme?.Foreground ?? SystemColors.ControlText;

            g.Clear(back);

            var rect = new Rectangle(2, 2, Width - 5, Height - 5);
            if (_hover || Focused)
            {
                using (var b = new SolidBrush(hoverBack)) g.FillRectangle(b, rect);
                using (var p = new Pen(border)) g.DrawRectangle(p, rect);
            }

            // Glyph centered, near the top third.
            int glyphSize = 48;
            if (Glyph != null)
            {
                int gx = (Width - glyphSize) / 2;
                int gy = Height / 2 - glyphSize / 2 - 12;
                g.DrawImage(Glyph, new Rectangle(gx, gy, glyphSize, glyphSize));
            }

            // Caption under the glyph.
            var textRect = new Rectangle(6, Height / 2 + 16, Width - 12, Height / 2 - 18);
            TextRenderer.DrawText(g, Title, Font, textRect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }
}
