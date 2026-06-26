using System.Drawing;
using System.Windows.Forms;
using Multizip.Core.Settings;

namespace Multizip.App.Theme
{
    /// <summary>
    /// Applies a light or dark palette to forms. The light palette is the standard
    /// Windows 7 system theme (so the app looks native); the dark palette keeps the
    /// same flat, classic metrics - just darker surfaces - rather than adopting a
    /// modern Fluent style.
    /// </summary>
    public sealed class ThemeManager
    {
        public AppTheme Current { get; private set; }

        public ThemeManager(AppTheme theme) => Current = theme;

        public bool IsDark => Current == AppTheme.Dark;

        // Surfaces
        public Color Background => IsDark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
        public Color WindowBackground => IsDark ? Color.FromArgb(30, 30, 30) : SystemColors.Window;
        public Color Foreground => IsDark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
        public Color WindowForeground => IsDark ? Color.FromArgb(240, 240, 240) : SystemColors.WindowText;
        public Color Accent => IsDark ? Color.FromArgb(0, 120, 215) : Color.FromArgb(0, 102, 204);
        public Color Border => IsDark ? Color.FromArgb(63, 63, 70) : SystemColors.ControlDark;
        public Color HoverBackground => IsDark ? Color.FromArgb(62, 62, 66) : Color.FromArgb(229, 243, 255);

        public void Toggle()
        {
            Current = IsDark ? AppTheme.Light : AppTheme.Dark;
        }

        /// <summary>Recursively apply the palette to a control tree.</summary>
        public void Apply(Control root)
        {
            if (root is Form form)
            {
                form.BackColor = Background;
                form.ForeColor = Foreground;
            }
            ApplyTo(root);
        }

        private void ApplyTo(Control control)
        {
            foreach (Control child in control.Controls)
            {
                switch (child)
                {
                    case TextBox _:
                    case ListView _:
                    case TreeView _:
                    case ListBox _:
                        child.BackColor = WindowBackground;
                        child.ForeColor = WindowForeground;
                        break;
                    case Button btn:
                        btn.BackColor = IsDark ? Color.FromArgb(62, 62, 66) : SystemColors.Control;
                        btn.ForeColor = Foreground;
                        btn.FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.System;
                        if (IsDark) btn.FlatAppearance.BorderColor = Border;
                        break;
                    case Label _:
                    case CheckBox _:
                    case RadioButton _:
                    case GroupBox _:
                        child.BackColor = Color.Transparent;
                        child.ForeColor = Foreground;
                        break;
                    case Panel _:
                    case TableLayoutPanel _:
                    case FlowLayoutPanel _:
                        child.BackColor = Background;
                        child.ForeColor = Foreground;
                        break;
                    case MenuStrip ms:
                        ms.BackColor = Background;
                        ms.ForeColor = Foreground;
                        break;
                    case ToolStrip ts:
                        ts.BackColor = Background;
                        ts.ForeColor = Foreground;
                        break;
                    case StatusStrip ss:
                        ss.BackColor = Background;
                        ss.ForeColor = Foreground;
                        break;
                    default:
                        child.ForeColor = Foreground;
                        break;
                }

                if (child.HasChildren) ApplyTo(child);
            }
        }
    }
}
