using System.Diagnostics;

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
    private readonly ListBox _blockedList = new();

    private readonly System.Windows.Forms.Timer _tickTimer = new();
    private readonly System.Windows.Forms.Timer _blockTimer = new();

    private int _remainingSeconds;
    private bool _running;
    private bool _isWorkPhase = true;

    private readonly HashSet<string> _blockedProcessNames =
    [
        // Browsers
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",
        // Typical game launchers / apps
        "steam", "epicgameslauncher", "riotclientservices", "leagueclient", "valorant",
        "battle.net", "upc", "origin", "eadesktop", "minecraftlauncher"
    ];

    public MainForm()
    {
        Text = "Pomodoro Blocker";
        Width = 560;
        Height = 500;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        InitializeUi();

        _tickTimer.Interval = 1000;
        _tickTimer.Tick += (_, _) => TickSecond();

        _blockTimer.Interval = 1000;
        _blockTimer.Tick += (_, _) => EnforceAppBlocking();

        ResetState();
    }

    private void InitializeUi()
    {
        var settingsGroup = new GroupBox
        {
            Text = "Settings (minutes)",
            Left = 20,
            Top = 20,
            Width = 500,
            Height = 90
        };

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

        settingsGroup.Controls.Add(workLabel);
        settingsGroup.Controls.Add(_workMinutes);
        settingsGroup.Controls.Add(breakLabel);
        settingsGroup.Controls.Add(_breakMinutes);

        _phaseLabel.Left = 20;
        _phaseLabel.Top = 130;
        _phaseLabel.Width = 500;
        _phaseLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);

        _timeLabel.Left = 20;
        _timeLabel.Top = 165;
        _timeLabel.Width = 500;
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

        var blockedTitle = new Label
        {
            Text = "Blocked process names while work timer is running:",
            Left = 20,
            Top = 295,
            Width = 500
        };

        _blockedList.Left = 20;
        _blockedList.Top = 320;
        _blockedList.Width = 500;
        _blockedList.Height = 120;
        _blockedList.Items.AddRange(_blockedProcessNames.OrderBy(x => x).ToArray<object>());

        Controls.Add(settingsGroup);
        Controls.Add(_phaseLabel);
        Controls.Add(_timeLabel);
        Controls.Add(_startButton);
        Controls.Add(_pauseButton);
        Controls.Add(_resetButton);
        Controls.Add(blockedTitle);
        Controls.Add(_blockedList);
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
            System.Media.SystemSounds.Exclamation.Play();
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

    private void UpdateUi()
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, _remainingSeconds));
        _phaseLabel.Text = _isWorkPhase ? "Phase: WORK (blocking enabled)" : "Phase: BREAK (blocking off)";
        _timeLabel.Text = ts.ToString("mm\\:ss");

        _workMinutes.Enabled = !_running;
        _breakMinutes.Enabled = !_running;
        _startButton.Enabled = !_running;
        _pauseButton.Enabled = _running;
    }
}
