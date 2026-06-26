using System;
using System.Drawing;
using System.Windows.Forms;
using Multizip.App.Util;
using Multizip.Core.Logging;
using Multizip.Core.Settings;

namespace Multizip.App.Forms
{
    /// <summary>Application settings: appearance, performance, integration and logging.</summary>
    public sealed class SettingsForm : Form
    {
        private CheckBox _dark, _checkUpdates, _confirmDelete, _verboseLog;
        private NumericUpDown _threads, _maxRecent;
        private Button _registerShell, _unregisterShell;
        private Label _shellState;

        public SettingsForm()
        {
            BuildUi();
            LoadValues();
            AppServices.Theme.Apply(this);
        }

        private void BuildUi()
        {
            Text = "Settings";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(460, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var appearance = new GroupBox { Text = "Appearance", Left = 12, Top = 8, Width = 436, Height = 60 };
            _dark = new CheckBox { Text = "Dark mode (keeps the classic Windows look)", Left = 12, Top = 24, Width = 400 };
            appearance.Controls.Add(_dark);
            Controls.Add(appearance);

            var perf = new GroupBox { Text = "Performance", Left = 12, Top = 76, Width = 436, Height = 92 };
            perf.Controls.Add(new Label { Text = "Default threads (0 = auto / all cores):", Left = 12, Top = 26, Width = 240 });
            _threads = new NumericUpDown { Left = 300, Top = 22, Width = 110, Minimum = 0, Maximum = 256 };
            perf.Controls.Add(_threads);
            perf.Controls.Add(new Label { Text = "Recent files to remember:", Left = 12, Top = 56, Width = 240 });
            _maxRecent = new NumericUpDown { Left = 300, Top = 52, Width = 110, Minimum = 0, Maximum = 50 };
            perf.Controls.Add(_maxRecent);
            Controls.Add(perf);

            var integ = new GroupBox { Text = "Windows Explorer integration (per-user)", Left = 12, Top = 176, Width = 436, Height = 90 };
            _shellState = new Label { Left = 12, Top = 22, Width = 412 };
            _registerShell = new Button { Text = "Add context menu", Left = 12, Top = 46, Width = 150, Height = 28, FlatStyle = FlatStyle.System };
            _unregisterShell = new Button { Text = "Remove context menu", Left = 170, Top = 46, Width = 160, Height = 28, FlatStyle = FlatStyle.System };
            _registerShell.Click += (s, e) => { ShellIntegration.Register(); RefreshShellState(); };
            _unregisterShell.Click += (s, e) => { ShellIntegration.Unregister(); RefreshShellState(); };
            integ.Controls.AddRange(new Control[] { _shellState, _registerShell, _unregisterShell });
            Controls.Add(integ);

            var misc = new GroupBox { Text = "Other", Left = 12, Top = 274, Width = 436, Height = 60 };
            _checkUpdates = new CheckBox { Text = "Check for updates on startup", Left = 12, Top = 16, Width = 220 };
            _confirmDelete = new CheckBox { Text = "Confirm deletes", Left = 12, Top = 36, Width = 160 };
            _verboseLog = new CheckBox { Text = "Verbose logging", Left = 240, Top = 16, Width = 180 };
            misc.Controls.AddRange(new Control[] { _checkUpdates, _confirmDelete, _verboseLog });
            Controls.Add(misc);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 290, Top = 344, Width = 74, Height = 28, FlatStyle = FlatStyle.System };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 372, Top = 344, Width = 76, Height = 28, FlatStyle = FlatStyle.System };
            ok.Click += (s, e) => SaveValues();
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadValues()
        {
            var s = AppServices.Settings;
            _dark.Checked = s.Theme == AppTheme.Dark;
            _threads.Value = Clamp(s.DefaultThreadCount, (int)_threads.Minimum, (int)_threads.Maximum);
            _maxRecent.Value = Clamp(s.MaxRecentFiles, (int)_maxRecent.Minimum, (int)_maxRecent.Maximum);
            _checkUpdates.Checked = s.CheckForUpdatesOnStartup;
            _confirmDelete.Checked = s.ConfirmBeforeDelete;
            _verboseLog.Checked = s.VerboseLogging;
            RefreshShellState();
        }

        private void RefreshShellState()
        {
            bool on = ShellIntegration.IsRegistered();
            _shellState.Text = on ? "Context-menu entries are installed." : "Context-menu entries are not installed.";
            _registerShell.Enabled = !on;
            _unregisterShell.Enabled = on;
        }

        private void SaveValues()
        {
            var s = AppServices.Settings;
            s.Theme = _dark.Checked ? AppTheme.Dark : AppTheme.Light;
            s.DefaultThreadCount = (int)_threads.Value;
            s.MaxRecentFiles = (int)_maxRecent.Value;
            s.CheckForUpdatesOnStartup = _checkUpdates.Checked;
            s.ConfirmBeforeDelete = _confirmDelete.Checked;
            s.VerboseLogging = _verboseLog.Checked;
            s.Save();

            if (AppServices.Theme.IsDark != _dark.Checked) AppServices.Theme.Toggle();
            Logger.MinimumLevel = s.VerboseLogging ? LogLevel.Debug : LogLevel.Info;
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);
    }
}
