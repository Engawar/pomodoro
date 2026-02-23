using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PomodoroBlocker;

public class MainForm : Form
{
    private readonly Label _phaseLabel = new();
    private readonly Label _timeLabel = new();
    private readonly NumericUpDown _workMinutes = new();
    private readonly NumericUpDown _breakMinutes = new();
    private readonly Button _startButton = new();
    private readonly Button _pauseButton = new();
    private readonly Button _resetButton = new();
    private readonly Button _compactButton = new();
    private readonly ListBox _blockedList = new();
    private readonly GroupBox _settingsGroup = new();
    private readonly Label _blockedTitle = new();

    private readonly System.Windows.Forms.Timer _tickTimer = new();
    private readonly System.Windows.Forms.Timer _blockTimer = new();

    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _topMostMenuItem = new();
    private readonly ToolStripMenuItem _compactMenuItem = new();

    private int _remainingSeconds;
    private bool _running;
    private bool _isWorkPhase = true;
    private bool _isCompactView;
    private bool _topMostLocked = true;

    private readonly Size _normalSize = new(560, 500);
    private readonly Size _compactSize = new(300, 170);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private readonly HashSet<string> _blockedProcessNames =
    [
        // Browsers
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",
        // Typical game launchers / apps
        "steam", "epicgameslauncher", "riotclientservices", "leagueclient", "valorant",
        "battle.net", "upc", "origin", "eadesktop", "minecraftlauncher"
    ];

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    public MainForm()
    {
        Text = "Pomodoro Blocker";
        Width = _normalSize.Width;
        Height = _normalSize.Height;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;

        InitializeUi();
        InitializeContextMenu();

        Activated += (_, _) => ApplyTopMostState();
        Shown += (_, _) => ApplyTopMostState();

        _tickTimer.Interval = 1000;
        _tickTimer.Tick += (_, _) => TickSecond();

        _blockTimer.Interval = 1000;
        _blockTimer.Tick += (_, _) => EnforceAppBlocking();

        ResetState();
    }

    private void InitializeUi()
    {
        _settingsGroup.Text = "Settings (minutes)";
        _settingsGroup.Left = 20;
        _settingsGroup.Top = 20;
        _settingsGroup.Width = 500;
        _settingsGroup.Height = 90;

        var workLabel = new Label { Text = "Work:", Left = 20, Top = 35, Width = 50 };
        _workMinutes.Left = 80;
        _workMinutes.Top = 30;
        _workMinutes.Width = 70;
        _workMinutes.Minimum = 1;
        _workMinutes.Maximum = 120;
        _workMinutes.Value = 25;

        var breakLabel = new Label { Text = "Break:", Left = 180, Top = 35, Width = 50 };
        _breakMinutes.Left = 240;
        _breakMinutes.Top = 30;
        _breakMinutes.Width = 70;
        _breakMinutes.Minimum = 1;
        _breakMinutes.Maximum = 60;
        _breakMinutes.Value = 5;

        _settingsGroup.Controls.Add(workLabel);
        _settingsGroup.Controls.Add(_workMinutes);
        _settingsGroup.Controls.Add(breakLabel);
        _settingsGroup.Controls.Add(_breakMinutes);

        _phaseLabel.Left = 20;
        _phaseLabel.Top = 130;
        _phaseLabel.Width = 500;
        _phaseLabel.Height = 36;
        _phaseLabel.TextAlign = ContentAlignment.MiddleLeft;
        _phaseLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);

        _timeLabel.Left = 20;
        _timeLabel.Top = 165;
        _timeLabel.Width = 500;
        _timeLabel.Height = 60;
        _timeLabel.AutoSize = false;
        _timeLabel.TextAlign = ContentAlignment.MiddleLeft;
        _timeLabel.Font = new Font("Consolas", 28, FontStyle.Bold);

        _startButton.Text = "Start";
        _startButton.Left = 20;
        _startButton.Top = 240;
        _startButton.Width = 120;
        _startButton.Click += (_, _) => StartTimer();

        _pauseButton.Text = "Pause";
        _pauseButton.Left = 160;
        _pauseButton.Top = 240;
        _pauseButton.Width = 120;
        _pauseButton.Click += (_, _) => PauseTimer();

        _resetButton.Text = "Reset";
        _resetButton.Left = 300;
        _resetButton.Top = 240;
        _resetButton.Width = 120;
        _resetButton.Click += (_, _) => ResetState();

        _compactButton.Text = "Compact View";
        _compactButton.Left = 430;
        _compactButton.Top = 240;
        _compactButton.Width = 90;
        _compactButton.Click += (_, _) => ToggleCompactView();

        _blockedTitle.Text = "Blocked process names while work timer is running:";
        _blockedTitle.Left = 20;
        _blockedTitle.Top = 295;
        _blockedTitle.Width = 500;

        _blockedList.Left = 20;
        _blockedList.Top = 320;
        _blockedList.Width = 500;
        _blockedList.Height = 120;
        _blockedList.Items.AddRange(_blockedProcessNames.OrderBy(x => x).ToArray<object>());

        Controls.Add(_settingsGroup);
        Controls.Add(_phaseLabel);
        Controls.Add(_timeLabel);
        Controls.Add(_startButton);
        Controls.Add(_pauseButton);
        Controls.Add(_resetButton);
        Controls.Add(_compactButton);
        Controls.Add(_blockedTitle);
        Controls.Add(_blockedList);
    }

    private void InitializeContextMenu()
    {
        _topMostMenuItem.Click += (_, _) => ToggleTopMostLock();
        _compactMenuItem.Click += (_, _) => ToggleCompactView();

        _contextMenu.Items.Add(_topMostMenuItem);
        _contextMenu.Items.Add(_compactMenuItem);

        ContextMenuStrip = _contextMenu;
        AttachContextMenuToChildControls(this);
    }

    private void AttachContextMenuToChildControls(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            control.ContextMenuStrip = _contextMenu;
            AttachContextMenuToChildControls(control);
        }
    }

    private void StartTimer()
    {
        if (_remainingSeconds <= 0)
        {
            _isWorkPhase = true;
            _remainingSeconds = (int)_workMinutes.Value * 60;
        }

        _running = true;
        _tickTimer.Start();
        _blockTimer.Start();
        UpdateUi();
        BringToFrontWindow();
    }

    private void PauseTimer()
    {
        _running = false;
        _tickTimer.Stop();
        _blockTimer.Stop();
        UpdateUi();
    }

    private void ResetState()
    {
        _running = false;
        _isWorkPhase = true;
        _tickTimer.Stop();
        _blockTimer.Stop();
        _remainingSeconds = (int)_workMinutes.Value * 60;
        UpdateUi();
    }

    private void TickSecond()
    {
        if (!_running)
        {
            return;
        }

        _remainingSeconds--;

        if (_remainingSeconds <= 0)
        {
            _isWorkPhase = !_isWorkPhase;
            _remainingSeconds = (int)(_isWorkPhase ? _workMinutes.Value : _breakMinutes.Value) * 60;
            PlayPhaseAlarm();
        }

        UpdateUi();
    }

    private void EnforceAppBlocking()
    {
        if (!_running || !_isWorkPhase)
        {
            return;
        }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName.ToLowerInvariant();
                if (_blockedProcessNames.Contains(name))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore processes we cannot access.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void PlayPhaseAlarm()
    {
        if (_isWorkPhase)
        {
            System.Media.SystemSounds.Exclamation.Play();
            System.Media.SystemSounds.Exclamation.Play();
            return;
        }

        System.Media.SystemSounds.Asterisk.Play();
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void ToggleCompactView()
    {
        _isCompactView = !_isCompactView;

        _settingsGroup.Visible = !_isCompactView;
        _startButton.Visible = !_isCompactView;
        _pauseButton.Visible = !_isCompactView;
        _resetButton.Visible = !_isCompactView;
        _compactButton.Visible = !_isCompactView;
        _blockedTitle.Visible = !_isCompactView;
        _blockedList.Visible = !_isCompactView;

        if (_isCompactView)
        {
            Size = _compactSize;
            _phaseLabel.Top = 15;
            _phaseLabel.Width = 260;
            _phaseLabel.TextAlign = ContentAlignment.MiddleCenter;

            _timeLabel.Top = 60;
            _timeLabel.Width = 260;
            _timeLabel.Height = 70;
            _timeLabel.TextAlign = ContentAlignment.MiddleCenter;
        }
        else
        {
            Size = _normalSize;
            _phaseLabel.Top = 130;
            _phaseLabel.Width = 500;
            _phaseLabel.TextAlign = ContentAlignment.MiddleLeft;

            _timeLabel.Top = 165;
            _timeLabel.Width = 500;
            _timeLabel.Height = 60;
            _timeLabel.TextAlign = ContentAlignment.MiddleLeft;
        }

        UpdateUi();
    }

    private void ToggleTopMostLock()
    {
        _topMostLocked = !_topMostLocked;
        UpdateUi();

        if (_running && _topMostLocked)
        {
            BringToFrontWindow();
        }
    }

    private void ApplyTopMostState()
    {
        var shouldPinTopMost = _running && _topMostLocked;
        TopMost = shouldPinTopMost;

        if (!IsHandleCreated)
        {
            return;
        }

        var insertAfter = shouldPinTopMost ? HwndTopMost : HwndNoTopMost;
        _ = SetWindowPos(Handle, insertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
    }

    private void BringToFrontWindow()
    {
        WindowState = FormWindowState.Normal;
        Show();
        Activate();
        BringToFront();
        ApplyTopMostState();
    }

    private void UpdateUi()
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, _remainingSeconds));
        _phaseLabel.Text = _isWorkPhase ? "WORK" : "BREAK";
        _phaseLabel.ForeColor = _isWorkPhase ? Color.Firebrick : Color.SeaGreen;
        _timeLabel.Text = ts.ToString("mm\\:ss");

        _workMinutes.Enabled = !_running;
        _breakMinutes.Enabled = !_running;
        _startButton.Enabled = !_running;
        _pauseButton.Enabled = _running;

        ApplyTopMostState();

        _topMostMenuItem.Text = _topMostLocked ? "右クリック: 最前面固定を解除" : "右クリック: 最前面固定を有効化";
        _compactMenuItem.Text = _isCompactView ? "通常表示に戻す" : "時間のみ表示に切り替え";
    }
}
