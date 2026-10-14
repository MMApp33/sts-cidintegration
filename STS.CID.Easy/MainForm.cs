using System.Diagnostics;

namespace STS.CID.Easy;

public sealed class MainForm : Form
{
    // ScanToServe brand (scantoserve.com): primary CTA green #96CC28
    private static readonly Color Brand = Color.FromArgb(150, 204, 40);
    private static readonly Color BrandDark = Color.FromArgb(120, 168, 28);
    private static readonly Color BrandSoft = Color.FromArgb(245, 252, 230);
    private static readonly Color Bg = Color.FromArgb(248, 250, 252);
    private static readonly Color Surface = Color.White;
    private static readonly Color Border = Color.FromArgb(226, 232, 240);
    private static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);
    private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
    private static readonly Color TextSecondary = Color.FromArgb(51, 65, 85);
    private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
    private static readonly Color Ok = Color.FromArgb(22, 163, 74);
    private static readonly Color Bad = Color.FromArgb(220, 38, 38);

    private readonly AppSettings _settings;
    private readonly CallStore _store;
    private readonly LocalApiServer _server;
    private readonly string? _startError;

    private readonly NotifyIcon _trayIcon;
    private readonly StatusRow _apiRow = new("Local API");
    private readonly StatusRow _easyHostRow = new("Easy Host");
    private readonly StatusRow _forwardRow = new("Forwarding");
    private readonly Label _callsEmpty = new();
    private readonly Label _callsCount = new();
    private readonly ListView _calls = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
        BackColor = Surface,
        ForeColor = TextPrimary,
        Font = new Font("Segoe UI", 9.5f),
        HideSelection = false
    };
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 3000 };
    private readonly System.Windows.Forms.Timer _copyFeedbackTimer = new() { Interval = 2000 };
    private Button? _copyUrlButton;
    private string _copyUrlDefaultText = "Copy URL";

    private bool _allowVisible;
    private bool _exiting;
    private bool _shownHideTip;

    public MainForm(AppSettings settings, CallStore store, LocalApiServer server, bool startMinimized, string? startError)
    {
        _settings = settings;
        _store = store;
        _server = server;
        _startError = startError;
        _allowVisible = !startMinimized;

        Text = "ScanToServe CID Easy";
        Icon = LoadAppIcon(null);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 700);
        Size = new Size(780, 760);
        BackColor = Bg;
        Font = new Font("Segoe UI", 9f);
        ForeColor = TextPrimary;
        Padding = new Padding(0);

        _calls.Columns.Add("Time", 150);
        _calls.Columns.Add("Phone number", 180);
        _calls.Columns.Add("Line", 70);
        _calls.Columns.Add("Device", 160);

        BuildLayout();
        LoadCallHistory();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("View logs", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(SystemInformation.SmallIconSize),
            Text = "ScanToServe CID Easy",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        _copyFeedbackTimer.Tick += (_, _) =>
        {
            _copyFeedbackTimer.Stop();
            if (_copyUrlButton is not null)
            {
                _copyUrlButton.Text = _copyUrlDefaultText;
            }
        };

        _store.CallReceived += OnCallReceived;
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
        RefreshStatus();
    }

    private void BuildLayout()
    {
        var brandBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = Brand
        };

        var footer = BuildFooter();
        footer.Dock = DockStyle.Bottom;

        // Fixed-height recent calls so it never collapses under the top content.
        var calls = BuildCallsCard();
        calls.Dock = DockStyle.Bottom;
        calls.Height = 230;

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Bg,
            AutoScroll = true,
            Padding = new Padding(24, 18, 24, 12)
        };

        var stack = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Bg
        };
        // Dock.Top: last added is laid out first (topmost).
        stack.Controls.Add(BuildActionsAndHint());
        stack.Controls.Add(BuildStatusCard());
        stack.Controls.Add(BuildHeader());
        body.Controls.Add(stack);

        // Dock order: last added claims its edge first.
        Controls.Add(body);
        Controls.Add(calls);
        Controls.Add(footer);
        Controls.Add(brandBar);
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 4, 0, 16)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brand = new Label
        {
            Text = "ScanToServe",
            AutoSize = true,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Brand,
            Margin = new Padding(0, 0, 0, 2)
        };

        var product = new Label
        {
            Text = "CID Easy Connector",
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = TextSecondary,
            Margin = new Padding(0, 2, 0, 2)
        };
        var subtitle = new Label
        {
            Text = "Forwards caller ID from Easy Host into ScanToServe",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextMuted,
            Margin = new Padding(0)
        };
        var version = new Label
        {
            Text = $"v{LocalApiServer.Version}",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextMuted,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(12, 8, 0, 0),
            Padding = new Padding(8, 3, 8, 3),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        panel.Controls.Add(brand, 0, 0);
        panel.Controls.Add(version, 1, 0);
        panel.SetRowSpan(version, 2);
        panel.Controls.Add(product, 0, 1);
        panel.Controls.Add(subtitle, 0, 2);
        panel.SetColumnSpan(subtitle, 2);
        return panel;
    }

    private Control BuildStatusCard()
    {
        var card = CreateCard();
        card.Margin = new Padding(0, 0, 0, 12);
        card.Padding = new Padding(16, 14, 16, 14);

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4
        };
        for (var i = 0; i < 4; i++)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var heading = new Label
        {
            Text = "Connection status",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 10)
        };

        stack.Controls.Add(heading, 0, 0);
        stack.Controls.Add(_apiRow, 0, 1);
        stack.Controls.Add(_easyHostRow, 0, 2);
        stack.Controls.Add(_forwardRow, 0, 3);
        card.Controls.Add(stack);
        return card;
    }

    private Control BuildActionsAndHint()
    {
        var wrap = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 0, 0, 4)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        buttons.Controls.Add(CreatePrimaryButton("Configure Easy Host", ConfigureEasyHost));
        buttons.Controls.Add(CreateSecondaryButton("Send test call", async () => await SendTestCallAsync()));
        buttons.Controls.Add(CreateSecondaryButton("Copy forward URL", CopyForwardUrl));
        buttons.Controls.Add(CreateSecondaryButton("View logs", OpenLogFolder));

        var startWithWindows = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            Checked = WindowsStartup.IsEnabled() || _settings.StartWithWindows,
            Margin = new Padding(8, 8, 3, 3),
            ForeColor = TextSecondary,
            FlatStyle = FlatStyle.System
        };
        startWithWindows.CheckedChanged += (_, _) =>
        {
            _settings.StartWithWindows = startWithWindows.Checked;
            _settings.Save();
            WindowsStartup.SetEnabled(startWithWindows.Checked);
        };
        buttons.Controls.Add(startWithWindows);

        var hint = BuildHintCard();
        wrap.Controls.Add(hint);
        wrap.Controls.Add(buttons);
        return wrap;
    }

    private Control BuildHintCard()
    {
        var card = CreateCard();
        card.Padding = new Padding(14, 12, 14, 12);
        card.BackColor = BrandSoft;
        card.MinimumSize = new Size(0, 110);
        card.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(Brand);
            e.Graphics.FillRectangle(brush, 0, 0, 4, card.Height);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(6, 0, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "ScanToServe setup",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = BrandDark,
            Margin = new Padding(0, 0, 0, 4)
        };
        var body = new Label
        {
            Text = "Settings → Integrations → Phone Integration — paste this URL. Closing this window keeps the connector in the tray.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = TextSecondary,
            Margin = new Padding(0, 0, 0, 8)
        };
        var url = new Label
        {
            Text = _server.BaseUrl,
            AutoSize = true,
            Font = new Font("Consolas", 10f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 8, 8, 0)
        };

        _copyUrlDefaultText = "Copy URL";
        _copyUrlButton = CreatePrimaryButton(_copyUrlDefaultText, CopyBaseUrl);
        _copyUrlButton.Margin = new Padding(0, 4, 0, 0);

        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);
        layout.Controls.Add(body, 0, 1);
        layout.SetColumnSpan(body, 2);
        layout.Controls.Add(url, 0, 2);
        layout.Controls.Add(_copyUrlButton, 1, 2);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildCallsCard()
    {
        var outer = new Panel
        {
            BackColor = Bg,
            Padding = new Padding(24, 0, 24, 8)
        };

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(0)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Surface
        };
        var title = new Label
        {
            Text = "Recent calls",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 11)
        };
        _callsCount.AutoSize = true;
        _callsCount.Font = new Font("Segoe UI", 8.5f);
        _callsCount.ForeColor = TextMuted;
        header.Controls.Add(title);
        header.Controls.Add(_callsCount);
        header.Resize += (_, _) =>
        {
            _callsCount.Left = Math.Max(16, header.ClientSize.Width - _callsCount.Width - 16);
            _callsCount.Top = 13;
        };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 8),
            BackColor = Surface
        };

        _callsEmpty.Text = "No calls yet — use Send test call to verify the pipeline.";
        _callsEmpty.AutoSize = false;
        _callsEmpty.Dock = DockStyle.Fill;
        _callsEmpty.TextAlign = ContentAlignment.MiddleCenter;
        _callsEmpty.ForeColor = TextMuted;
        _callsEmpty.Font = new Font("Segoe UI", 9.5f);
        _callsEmpty.BackColor = Surface;

        listHost.Controls.Add(_calls);
        listHost.Controls.Add(_callsEmpty);
        _callsEmpty.BringToFront();

        card.Controls.Add(listHost);
        card.Controls.Add(header);
        outer.Controls.Add(card);
        return outer;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Height = 40,
            BackColor = Surface
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var label = new Label
        {
            Text = "Powered by ScanToServe",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextMuted,
            Location = new Point(24, 12)
        };
        footer.Controls.Add(label);
        return footer;
    }

    private static Panel CreateCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Surface,
            Padding = new Padding(12)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        };
        return card;
    }

    private static Button CreatePrimaryButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(0, 34),
            Padding = new Padding(14, 5, 14, 5),
            Margin = new Padding(0, 0, 8, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Brand,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = BrandDark;
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button CreateSecondaryButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(0, 34),
            Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(0, 0, 8, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Surface,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9f),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = BorderStrong;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Icon LoadAppIcon(Size? size)
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("STS.CID.Easy.app.ico");
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        return size is { } s ? new Icon(stream, s) : new Icon(stream);
    }

    private void RefreshStatus()
    {
        if (_server.IsRunning)
        {
            _apiRow.Set($"Listening on {_server.BaseUrl}", ok: true);
        }
        else
        {
            _apiRow.Set($"Not running — {_startError ?? "unknown error"} (is port {_settings.ApiPort} in use?)", ok: false);
        }

        var easyRunning = EasyHost.IsRunning();
        _easyHostRow.Set(
            easyRunning ? "Running" : "Not running — start Easy Host so calls are detected",
            easyRunning);

        if (EasyHost.FindHostKeyName() is null)
        {
            _forwardRow.Set("Easy Host settings not found — install it and run it once", ok: false);
        }
        else if (EasyHost.IsConfiguredFor(_settings.ApiPort))
        {
            _forwardRow.Set("Easy Host forwards calls to this app", ok: true);
        }
        else
        {
            _forwardRow.Set("Not configured — click Configure Easy Host", ok: false);
        }
    }

    private void LoadCallHistory()
    {
        _calls.Items.Clear();
        foreach (var call in _store.History)
        {
            AddCallRow(call, insertAtTop: false);
        }

        UpdateCallsEmptyState();
    }

    private void OnCallReceived(object? sender, IncomingCall call)
    {
        void Add()
        {
            AddCallRow(call, insertAtTop: true);
            while (_calls.Items.Count > 50)
            {
                _calls.Items.RemoveAt(_calls.Items.Count - 1);
            }

            UpdateCallsEmptyState();
        }

        if (!IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(Add);
        }
        else
        {
            Add();
        }
    }

    private void AddCallRow(IncomingCall call, bool insertAtTop)
    {
        var item = new ListViewItem(call.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        item.SubItems.Add(call.PhoneNumber);
        item.SubItems.Add(call.InputPort ?? string.Empty);
        item.SubItems.Add(call.DeviceSerial ?? string.Empty);
        if (insertAtTop)
        {
            _calls.Items.Insert(0, item);
        }
        else
        {
            _calls.Items.Add(item);
        }
    }

    private void UpdateCallsEmptyState()
    {
        var empty = _calls.Items.Count == 0;
        _callsEmpty.Visible = empty;
        if (empty)
        {
            _callsEmpty.BringToFront();
        }
        else
        {
            _calls.BringToFront();
        }

        _callsCount.Text = empty ? string.Empty : $"{_calls.Items.Count} shown";
        if (_callsCount.Parent is { } parent)
        {
            _callsCount.Left = Math.Max(16, parent.ClientSize.Width - _callsCount.Width - 16);
        }
    }

    private void ConfigureEasyHost()
    {
        try
        {
            var result = EasyHost.Configure(_settings.ApiPort);
            MessageBox.Show(result.Message, "Configure Easy Host", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            Log.Error("Configure Easy Host failed.", ex);
            MessageBox.Show($"Could not update Easy Host settings:\n{ex.Message}", "Configure Easy Host",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        RefreshStatus();
    }

    private async Task SendTestCallAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"{_server.BaseUrl}/callerid?caller=0871234567&devserial=TEST&port=1" +
                      $"&date={DateTime.Now:yyyyMMdd}&time={DateTime.Now:HHmm}";
            var body = await http.GetStringAsync(url);
            Log.Info($"Test call sent; response {body}");

            // Ensure the list reflects the store even if the event raced the UI.
            if (IsHandleCreated)
            {
                BeginInvoke(LoadCallHistory);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Test call failed.", ex);
            MessageBox.Show($"Test call failed:\n{ex.Message}", "Send Test Call",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyForwardUrl()
    {
        Clipboard.SetText(EasyHost.BuildForwardUrl(_settings.ApiPort));
        MessageBox.Show(
            "Copied. Paste it into the Easy Host registry value \"Web Connection URL/Post Suffix\" " +
            "if you prefer to configure it manually.",
            "Copy Forward URL", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopyBaseUrl()
    {
        Clipboard.SetText(_server.BaseUrl);
        if (_copyUrlButton is not null)
        {
            _copyUrlButton.Text = "Copied";
            _copyFeedbackTimer.Stop();
            _copyFeedbackTimer.Start();
        }
    }

    private static void OpenLogFolder()
    {
        Directory.CreateDirectory(Log.Directory);
        Process.Start(new ProcessStartInfo { FileName = Log.Directory, UseShellExecute = true });
    }

    private void ShowWindow()
    {
        _allowVisible = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exiting = true;
        Close();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (!_allowVisible)
        {
            value = false;
            if (!IsHandleCreated)
            {
                CreateHandle();
            }
        }

        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_shownHideTip)
            {
                _shownHideTip = true;
                _trayIcon.ShowBalloonTip(
                    2500,
                    "Still running",
                    "CID Easy stays in the tray. Right-click the icon to exit.",
                    ToolTipIcon.Info);
            }

            return;
        }

        _statusTimer.Stop();
        _copyFeedbackTimer.Stop();
        _store.CallReceived -= OnCallReceived;
        _trayIcon.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _statusTimer.Dispose();
            _copyFeedbackTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class StatusRow : Panel
    {
        private readonly Label _dot = new()
        {
            Text = "●",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 8, 0)
        };
        private readonly Label _name = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 10, 0),
            MinimumSize = new Size(88, 0)
        };
        private readonly Label _value = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextMuted,
            MaximumSize = new Size(520, 0)
        };

        public StatusRow(string name)
        {
            _name.Text = name;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Dock = DockStyle.Top;
            Margin = new Padding(0, 2, 0, 2);

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            flow.Controls.Add(_dot);
            flow.Controls.Add(_name);
            flow.Controls.Add(_value);
            Controls.Add(flow);
            Set("Checking…", ok: false);
        }

        public void Set(string text, bool ok)
        {
            _value.Text = text;
            _dot.ForeColor = ok ? Ok : Bad;
            _value.ForeColor = ok ? TextPrimary : Bad;
        }
    }
}
