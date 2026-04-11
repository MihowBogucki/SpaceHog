using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace SpaceHog;

public sealed class MainForm : Form
{
    private readonly TreeView _tree;
    private readonly ListView _details;
    private readonly SplitContainer _splitter;
    private readonly StatusStrip _statusBar;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _statusSize;
    private readonly ToolStripProgressBar _progressBar;
    private readonly Panel _headerPanel;
    private readonly TableLayoutPanel _headerLayout;
    private readonly Label _brandTitle;
    private readonly Label _brandSubtitle;
    private readonly ComboBox _driveCombo;
    private readonly TextBox _searchBox;
    private readonly Button _scanButton;
    private readonly Button _stopButton;
    private readonly Button _browseButton;
    private readonly ImageList _imageList;
    private readonly TableLayoutPanel _shellLayout;
    private readonly Panel _treePanel;
    private readonly Label _treeTitle;
    private readonly Label _treeHint;
    private readonly Panel _treeHost;
    private readonly Panel _treeEmptyState;
    private readonly TableLayoutPanel _rightLayout;
    private readonly TableLayoutPanel _analyticsLayout;
    private readonly Panel _heroPanel;
    private readonly Label _heroTitle;
    private readonly Label _heroSubtitle;
    private readonly FlowLayoutPanel _insightFlow;
    private readonly InsightCard _scanInsight;
    private readonly InsightCard _sizeInsight;
    private readonly InsightCard _focusInsight;
    private readonly UsageChartPanel _usageChart;
    private readonly FolderGuidancePanel _guidancePanel;

    private DirEntry? _root;
    private DirEntry? _liveRoot;
    private DirScanner? _scanner;
    private CancellationTokenSource? _cts;
    private TreeNode? _liveRootNode;
    private string _lastProgressPath = "";
    private int _detailsSortColumn = 1;
    private bool _detailsSortAscending;
    private bool _isScanning;
    private DateTime _scanStartedUtc;

    public MainForm()
    {
        Text = "SpaceHog";
        Size = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(220, 220, 220);
        Font = new Font("Segoe UI", 9.5f);
        MinimumSize = new Size(980, 640);
        DoubleBuffered = true;
        Icon = CreatePigIcon();

        _imageList = new ImageList { ImageSize = new Size(18, 18), ColorDepth = ColorDepth.Depth32Bit };
        BuildImageList();

        // --- Header ---
        _headerPanel = new Panel
        {
            BackColor = Color.FromArgb(20, 24, 30),
            Dock = DockStyle.Top,
            Height = 92,
            Padding = new Padding(18, 14, 18, 12)
        };
        _headerPanel.Paint += HeaderPanel_Paint;

        _headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var brandPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };
        _brandTitle = new Label
        {
            AutoSize = true,
            Text = "SpaceHog",
            Font = new Font("Segoe UI Variable Display", 20f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(0, 2),
            BackColor = Color.Transparent
        };
        _brandSubtitle = new Label
        {
            AutoSize = true,
            Text = "Live disk visibility with cleanup guidance built in",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(165, 190, 210),
            Location = new Point(2, 40),
            BackColor = Color.Transparent
        };
        brandPanel.Controls.AddRange(new Control[] { _brandTitle, _brandSubtitle });

        var drivePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 12, 14, 0)
        };

        var driveLabel = new Label
        {
            AutoSize = true,
            Text = "Drive",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 205, 220),
            Margin = new Padding(0, 10, 8, 0)
        };

        var drivePicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(38, 44, 52),
            ForeColor = Color.FromArgb(220, 220, 220),
            FlatStyle = FlatStyle.Flat,
            Width = 188,
            Margin = new Padding(0, 2, 10, 0)
        };
        _driveCombo = drivePicker;
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var label = $"{d.Name.TrimEnd('\\')}  ({DirEntry.FormatSize(d.TotalSize - d.AvailableFreeSpace)} / {DirEntry.FormatSize(d.TotalSize)})";
            _driveCombo.Items.Add(label);
        }
        if (_driveCombo.Items.Count > 0) _driveCombo.SelectedIndex = 0;

        _scanButton = CreateHeaderButton("Scan", Color.FromArgb(33, 150, 243), Color.White);
        _scanButton.Click += async (_, _) => await StartScan();

        _stopButton = CreateHeaderButton("Stop", Color.FromArgb(58, 64, 74), Color.FromArgb(255, 200, 140));
        _stopButton.Click += (_, _) => StopScan();

        _browseButton = CreateHeaderButton("Browse...", Color.FromArgb(38, 44, 52), Color.White);
        _browseButton.Click += async (_, _) => await BrowseAndScan();

        drivePanel.Controls.AddRange(new Control[] { driveLabel, _driveCombo, _scanButton, _stopButton, _browseButton });

        var searchPanel = new Panel
        {
            Width = 270,
            Height = 44,
            Margin = new Padding(0, 12, 0, 0),
            BackColor = Color.FromArgb(15, 19, 24)
        };
        searchPanel.Paint += SearchPanel_Paint;

        _searchBox = new TextBox
        {
            BackColor = Color.FromArgb(38, 44, 52),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.None,
            Size = new Size(214, 24),
            Location = new Point(38, 11),
            Font = new Font("Segoe UI", 9.5f)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();
        _searchBox.PlaceholderText = "Filter folders or paths";
        searchPanel.Controls.Add(_searchBox);

        _headerLayout.Controls.Add(brandPanel, 0, 0);
        _headerLayout.Controls.Add(drivePanel, 1, 0);
        _headerLayout.Controls.Add(searchPanel, 2, 0);
        _headerPanel.Controls.Add(_headerLayout);

        // --- Tree view ---
        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 21, 27),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9.5f),
            ImageList = _imageList,
            BorderStyle = BorderStyle.None,
            ShowLines = false,
            ShowPlusMinus = true,
            ShowRootLines = false,
            HideSelection = false,
            FullRowSelect = true,
            DrawMode = TreeViewDrawMode.OwnerDrawText,
            ItemHeight = 28,
            Indent = 18
        };
        _tree.AfterSelect += Tree_AfterSelect;
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.DrawNode += Tree_DrawNode;

        // --- Details ListView ---
        _details = new ListView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 21, 27),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9.5f),
            View = View.Details,
            FullRowSelect = true,
            BorderStyle = BorderStyle.None,
            SmallImageList = _imageList,
            OwnerDraw = true,
            HeaderStyle = ColumnHeaderStyle.Clickable,
            GridLines = false
        };
        _details.Columns.Add("Name", 250);
        _details.Columns.Add("Size", 100, HorizontalAlignment.Right);
        _details.Columns.Add("% of Parent", 160);
        _details.Columns.Add("% of Total", 80, HorizontalAlignment.Right);
        _details.Columns.Add("Files", 80, HorizontalAlignment.Right);
        _details.Columns.Add("Folders", 80, HorizontalAlignment.Right);
        _details.Columns.Add("Path", 300);
        _details.DrawColumnHeader += Details_DrawColumnHeader;
        _details.DrawSubItem += Details_DrawSubItem;
        _details.DoubleClick += Details_DoubleClick;
        _details.ColumnClick += Details_ColumnClick;
        _details.Resize += (_, _) => LayoutDetailsColumns();

        _treeTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "Folders",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            Padding = new Padding(18, 12, 18, 0)
        };

        _treeHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "Scan a drive to surface the biggest directories first.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = Color.FromArgb(134, 153, 168),
            Padding = new Padding(18, 0, 18, 4)
        };

        _treeHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 8, 8),
            BackColor = Color.Transparent
        };
        _treeEmptyState = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(16, 20, 26)
        };
        _treeEmptyState.Paint += TreeEmptyState_Paint;
        _treeHost.Controls.Add(_tree);
        _treeHost.Controls.Add(_treeEmptyState);

        _treePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 30),
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _treePanel.Paint += CardPanel_Paint;
        _treePanel.Controls.Add(_treeHost);
        _treePanel.Controls.Add(_treeHint);
        _treePanel.Controls.Add(_treeTitle);

        _heroPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 30),
            Padding = new Padding(22, 20, 22, 16),
            Margin = new Padding(0)
        };
        _heroPanel.Paint += HeroPanel_Paint;

        _heroTitle = new Label
        {
            AutoSize = true,
            Text = "SpaceHog",
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(22, 18)
        };

        _heroSubtitle = new Label
        {
            AutoSize = false,
            Text = "Live disk analysis with a faster sense of what is safe to inspect and what deserves caution.",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(200, 220, 235),
            BackColor = Color.Transparent,
            Location = new Point(22, 54),
            Size = new Size(720, 42)
        };

        _insightFlow = new FlowLayoutPanel
        {
            Location = new Point(22, 104),
            Size = new Size(760, 96),
            BackColor = Color.Transparent,
            WrapContents = true,
            AutoScroll = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _scanInsight = new InsightCard("Activity", "0", "items scanned");
        _sizeInsight = new InsightCard("Selected Size", "--", "waiting for scan");
        _focusInsight = new InsightCard("Focus", "--", "largest folders");
        _insightFlow.Controls.AddRange(new Control[] { _scanInsight, _sizeInsight, _focusInsight });
        _heroPanel.Controls.AddRange(new Control[] { _heroTitle, _heroSubtitle, _insightFlow });

        _usageChart = new UsageChartPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 30),
            Margin = new Padding(0),
            Padding = new Padding(18, 14, 18, 14)
        };

        _guidancePanel = new FolderGuidancePanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 30),
            Margin = new Padding(0)
        };

        _analyticsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _analyticsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        _analyticsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        _analyticsLayout.Controls.Add(_usageChart, 0, 0);
        _analyticsLayout.Controls.Add(_guidancePanel, 1, 0);

        var detailsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 24, 30),
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        detailsPanel.Paint += CardPanel_Paint;

        var detailsTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "Contents",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            Padding = new Padding(18, 12, 18, 0)
        };

        var detailsHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "Open folders, compare size share, and filter by name or path.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = Color.FromArgb(134, 153, 168),
            Padding = new Padding(18, 0, 18, 4)
        };

        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 8, 8),
            BackColor = Color.Transparent
        };
        detailsHost.Controls.Add(_details);
        detailsPanel.Controls.Add(detailsHost);
        detailsPanel.Controls.Add(detailsHint);
        detailsPanel.Controls.Add(detailsTitle);

        _rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rightLayout.Controls.Add(_heroPanel, 0, 0);
        _rightLayout.Controls.Add(_analyticsLayout, 0, 1);
        _rightLayout.Controls.Add(detailsPanel, 0, 2);

        _shellLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 14, 18),
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(14, 12, 14, 10)
        };
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // --- Splitter ---
        _splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = Color.FromArgb(12, 14, 18),
            SplitterWidth = 8,
            IsSplitterFixed = false,
            FixedPanel = FixedPanel.None
        };
        _splitter.Panel1.Padding = new Padding(0, 0, 10, 0);
        _splitter.Panel2.Padding = new Padding(10, 0, 0, 0);
        _splitter.Panel1.Controls.Add(_treePanel);
        _splitter.Panel2.Controls.Add(_rightLayout);
        _splitter.Paint += Splitter_Paint;
        _shellLayout.Controls.Add(_splitter, 0, 0);

        // --- Status bar ---
        _statusBar = new StatusStrip
        {
            BackColor = Color.FromArgb(15, 19, 24),
            ForeColor = Color.FromArgb(214, 224, 232),
            SizingGrip = false
        };
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        _statusSize = new ToolStripStatusLabel("") { TextAlign = System.Drawing.ContentAlignment.MiddleRight, AutoSize = false, Width = 300 };
        _progressBar = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, Visible = false, Size = new Size(150, 16) };
        _statusBar.Items.AddRange(new ToolStripItem[] { _statusLabel, _progressBar, _statusSize });

        // --- Layout ---
        Controls.Add(_shellLayout);
        Controls.Add(_headerPanel);
        Controls.Add(_statusBar);

        // Context menu on tree
        var treeMenu = new ContextMenuStrip
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = Color.FromArgb(20, 24, 30),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        var openTreeFolder = new ToolStripMenuItem("Open in Explorer");
        openTreeFolder.Click += (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is DirEntry entry)
            {
                try { Process.Start("explorer.exe", entry.FullPath); } catch { }
            }
        };
        var copyTreePath = new ToolStripMenuItem("Copy Path");
        copyTreePath.Click += (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is DirEntry entry)
                Clipboard.SetText(entry.FullPath);
        };
        treeMenu.Items.AddRange(new ToolStripItem[] { openTreeFolder, copyTreePath });
        _tree.ContextMenuStrip = treeMenu;

        var detailsMenu = new ContextMenuStrip
        {
            Renderer = new DarkToolStripRenderer(),
            BackColor = Color.FromArgb(20, 24, 30),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        detailsMenu.Opening += (_, _) => EnsureDetailsSelectionAtCursor();
        var openDetailsFolder = new ToolStripMenuItem("Open in Explorer");
        openDetailsFolder.Click += (_, _) =>
        {
            if (_details.SelectedItems.Count > 0 && _details.SelectedItems[0].Tag is DirEntry entry)
            {
                try { Process.Start("explorer.exe", entry.FullPath); } catch { }
            }
        };
        var copyDetailsPath = new ToolStripMenuItem("Copy Path");
        copyDetailsPath.Click += (_, _) =>
        {
            if (_details.SelectedItems.Count > 0 && _details.SelectedItems[0].Tag is DirEntry entry)
                Clipboard.SetText(entry.FullPath);
        };
        detailsMenu.Items.AddRange(new ToolStripItem[] { openDetailsFolder, copyDetailsPath });
        _details.ContextMenuStrip = detailsMenu;

        Resize += (_, _) => UpdateResponsiveLayout();
        Shown += (_, _) =>
        {
            ConfigureSplitter();
            InitializeSplitterDistance();
            UpdateResponsiveLayout();
            LayoutDetailsColumns();
        };
        _stopButton.Enabled = false;
        UpdateInsights(null);
    }

    private void ConfigureSplitter()
    {
        _splitter.Panel1MinSize = 280;
        _splitter.Panel2MinSize = 360;
    }

    private void InitializeSplitterDistance()
    {
        var min = _splitter.Panel1MinSize;
        var max = _splitter.Width - _splitter.Panel2MinSize;
        if (max < min) return;

        _splitter.SplitterDistance = Math.Clamp(410, min, max);
    }

    private static Icon CreatePigIcon()
    {
        const int sz = 64;
        using var bmp = DrawPigBitmap(sz);
        using var pngMs = new MemoryStream();
        bmp.Save(pngMs, System.Drawing.Imaging.ImageFormat.Png);
        var png = pngMs.ToArray();
        using var icoMs = new MemoryStream();
        icoMs.Write(new byte[] { 0, 0, 1, 0, 1, 0 });
        var entry = new byte[16];
        entry[0] = (byte)sz; entry[1] = (byte)sz;
        entry[4] = 1; entry[5] = 0;
        entry[6] = 32; entry[7] = 0;
        BitConverter.GetBytes((uint)png.Length).CopyTo(entry, 8);
        BitConverter.GetBytes(22u).CopyTo(entry, 12);
        icoMs.Write(entry);
        icoMs.Write(png);
        icoMs.Position = 0;
        return new Icon(icoMs);
    }

    /// Draw a coloured pig face using Graphics primitives (emoji rendering is monochrome in GDI+).
    private static Bitmap DrawPigBitmap(int sz)
    {
        var bmp = new Bitmap(sz, sz);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float s = sz / 64f; // scale factor

        // Ears (painted behind face)
        using var earBrush = new SolidBrush(Color.FromArgb(240, 140, 160));
        g.FillEllipse(earBrush, 3 * s, 2 * s, 17 * s, 20 * s);
        g.FillEllipse(earBrush, 44 * s, 2 * s, 17 * s, 20 * s);

        // Inner ears
        using var innerEarBrush = new SolidBrush(Color.FromArgb(210, 100, 130));
        g.FillEllipse(innerEarBrush, 6 * s, 5 * s, 10 * s, 13 * s);
        g.FillEllipse(innerEarBrush, 48 * s, 5 * s, 10 * s, 13 * s);

        // Face
        using var faceBrush = new SolidBrush(Color.FromArgb(255, 182, 193));
        g.FillEllipse(faceBrush, 5 * s, 12 * s, 54 * s, 50 * s);

        // Snout
        using var snoutBrush = new SolidBrush(Color.FromArgb(240, 150, 170));
        g.FillEllipse(snoutBrush, 16 * s, 35 * s, 32 * s, 22 * s);

        // Nostrils
        using var nostrilBrush = new SolidBrush(Color.FromArgb(160, 70, 90));
        g.FillEllipse(nostrilBrush, 20 * s, 40 * s,  9 * s, 8 * s);
        g.FillEllipse(nostrilBrush, 35 * s, 40 * s,  9 * s, 8 * s);

        // Eyes
        using var eyeBrush = new SolidBrush(Color.FromArgb(50, 30, 40));
        g.FillEllipse(eyeBrush, 17 * s, 22 * s, 10 * s, 10 * s);
        g.FillEllipse(eyeBrush, 37 * s, 22 * s, 10 * s, 10 * s);

        // Eye shine
        using var shineBrush = new SolidBrush(Color.White);
        g.FillEllipse(shineBrush, 19 * s, 24 * s, 4 * s, 4 * s);
        g.FillEllipse(shineBrush, 39 * s, 24 * s, 4 * s, 4 * s);

        return bmp;
    }

    private void BuildImageList()
    {
        // Simple colored squares as icons
        _imageList.Images.Add("folder", MakeIcon(Color.FromArgb(86, 156, 214)));
        _imageList.Images.Add("folder-large", MakeIcon(Color.FromArgb(220, 180, 80)));
        _imageList.Images.Add("drive", MakeIcon(Color.FromArgb(78, 201, 176)));
    }

    private static Bitmap MakeIcon(Color color)
    {
        var bmp = new Bitmap(18, 18);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, 2, 3, 14, 12);
        // tab on top for folder look
        g.FillRectangle(brush, 2, 1, 7, 3);
        return bmp;
    }

    private static Button CreateHeaderButton(string text, Color backColor, Color foreColor)
    {
        return new Button
        {
            AutoSize = true,
            Text = text,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(14, 6, 14, 6),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var rect = _headerPanel.ClientRectangle;
        using var brush = new LinearGradientBrush(rect, Color.FromArgb(14, 17, 22), Color.FromArgb(18, 28, 40), LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, rect);
        using var pen = new Pen(Color.FromArgb(40, 78, 104));
        e.Graphics.DrawLine(pen, 0, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
    }

    private void SearchPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 14);
        using var bgBrush = new SolidBrush(Color.FromArgb(15, 19, 24));
        using var borderPen = new Pen(Color.FromArgb(44, 70, 90));
        using var iconBrush = new SolidBrush(Color.FromArgb(134, 153, 168));
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);
        e.Graphics.DrawString("⌕", new Font("Segoe UI Symbol", 12f), iconBrush, new PointF(12, 10));
    }

    private void TreeEmptyState_Paint(object? sender, PaintEventArgs e)
    {
        if (!_treeEmptyState.Visible) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Color.FromArgb(16, 20, 26));
        using var titleBrush = new SolidBrush(Color.White);
        using var bodyBrush = new SolidBrush(Color.FromArgb(148, 165, 178));
        using var accentBrush = new SolidBrush(Color.FromArgb(48, 151, 209, 255));
        var centerX = _treeEmptyState.ClientSize.Width / 2f;
        e.Graphics.FillEllipse(accentBrush, centerX - 36, 116, 72, 72);
        e.Graphics.DrawString("🐷", new Font("Segoe UI Emoji", 26f), titleBrush, new PointF(centerX - 22, 124));
        var sf = new StringFormat { Alignment = StringAlignment.Center };
        e.Graphics.DrawString("Start a scan to build your folder map", new Font("Segoe UI Semibold", 12f, FontStyle.Bold), titleBrush, new RectangleF(30, 210, _treeEmptyState.Width - 60, 26), sf);
        e.Graphics.DrawString("The left pane will populate live while SpaceHog analyzes the selected drive.", new Font("Segoe UI", 9.5f), bodyBrush, new RectangleF(40, 244, _treeEmptyState.Width - 80, 48), sf);
    }

    private void UpdateResponsiveLayout()
    {
        var contentWidth = _splitter.Panel2.ClientSize.Width;
        if (contentWidth <= 0) return;

        var compact = contentWidth < 560;
        var medium = contentWidth < 760;

        int heroHeight = compact ? 280 : medium ? 240 : 220;
        _rightLayout.RowStyles[0].Height = heroHeight;
        _rightLayout.RowStyles[1].Height = compact ? 320 : 260;

        _heroSubtitle.Width = Math.Max(220, _heroPanel.ClientSize.Width - 44);
        _heroSubtitle.Height = compact ? 54 : 40;
        _heroSubtitle.MaximumSize = new Size(Math.Max(220, _heroPanel.ClientSize.Width - 44), 0);

        _insightFlow.Location = new Point(22, compact ? 124 : 104);
        _insightFlow.Size = new Size(Math.Max(220, _heroPanel.ClientSize.Width - 44), compact ? 112 : 96);

        var cardWidth = compact ? _insightFlow.Width : Math.Max(180, (_insightFlow.Width - 24) / 3);
        foreach (Control control in _insightFlow.Controls)
            control.Width = cardWidth;

        _treeHint.Text = compact
            ? "Scan a drive to build a live map of the biggest directories."
            : "Scan a drive to surface the biggest directories first.";

        if (contentWidth < 780)
        {
            _analyticsLayout.ColumnStyles[0].SizeType = SizeType.Percent;
            _analyticsLayout.ColumnStyles[0].Width = 100f;
            _analyticsLayout.ColumnStyles[1].SizeType = SizeType.Absolute;
            _analyticsLayout.ColumnStyles[1].Width = 0f;
            _analyticsLayout.RowCount = 2;
            if (_analyticsLayout.RowStyles.Count == 1)
            {
                _analyticsLayout.RowStyles.Clear();
                _analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));
                _analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            }
            _analyticsLayout.Controls.SetChildIndex(_usageChart, 0);
            _analyticsLayout.Controls.SetChildIndex(_guidancePanel, 1);
            _analyticsLayout.SetColumn(_usageChart, 0);
            _analyticsLayout.SetRow(_usageChart, 0);
            _analyticsLayout.SetColumn(_guidancePanel, 0);
            _analyticsLayout.SetRow(_guidancePanel, 1);
        }
        else
        {
            _analyticsLayout.RowCount = 1;
            _analyticsLayout.RowStyles.Clear();
            _analyticsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _analyticsLayout.ColumnStyles[0].SizeType = SizeType.Percent;
            _analyticsLayout.ColumnStyles[0].Width = 62f;
            _analyticsLayout.ColumnStyles[1].SizeType = SizeType.Percent;
            _analyticsLayout.ColumnStyles[1].Width = 38f;
            _analyticsLayout.SetColumn(_usageChart, 0);
            _analyticsLayout.SetRow(_usageChart, 0);
            _analyticsLayout.SetColumn(_guidancePanel, 1);
            _analyticsLayout.SetRow(_guidancePanel, 0);
        }

        _usageChart.CompactMode = contentWidth < 620;
        _usageChart.Invalidate();
        _guidancePanel.CompactMode = contentWidth < 700;
        _guidancePanel.Invalidate();
        _heroPanel.Invalidate();
        LayoutDetailsColumns();
    }

    private void LayoutDetailsColumns()
    {
        if (_details.Columns.Count < 7) return;

        var width = _details.ClientSize.Width;
        if (width <= 0) return;

        var nameWidth = Math.Clamp((int)(width * 0.29), 190, 360);
        const int sizeWidth = 110;
        const int pctParentWidth = 170;
        const int pctTotalWidth = 95;
        const int filesWidth = 82;
        const int foldersWidth = 86;
        var pathWidth = Math.Max(220, width - (nameWidth + sizeWidth + pctParentWidth + pctTotalWidth + filesWidth + foldersWidth + 6));

        _details.Columns[0].Width = nameWidth;
        _details.Columns[1].Width = sizeWidth;
        _details.Columns[2].Width = pctParentWidth;
        _details.Columns[3].Width = pctTotalWidth;
        _details.Columns[4].Width = filesWidth;
        _details.Columns[5].Width = foldersWidth;
        _details.Columns[6].Width = pathWidth;
    }

    private string GetSelectedDrive()
    {
        if (_driveCombo.SelectedItem is string s)
        {
            var idx = s.IndexOf(' ');
            return idx > 0 ? s[..idx] + "\\" : s;
        }
        return "C:\\";
    }

    private async Task BrowseAndScan()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select folder to scan", UseDescriptionForTitle = true };
        if (dlg.ShowDialog() == DialogResult.OK)
            await RunScan(dlg.SelectedPath);
    }

    private async Task StartScan()
    {
        await RunScan(GetSelectedDrive());
    }

    private async Task RunScan(string path)
    {
        StopScan();
        _cts = new CancellationTokenSource();
        _scanner = new DirScanner(6);
        _lastProgressPath = path;
        _scanStartedUtc = DateTime.UtcNow;

        PrepareLiveScan(path);
        SetScanState(true);
        _statusLabel.Text = $"Scanning {path}...";
        _statusSize.Text = "";

        var sw = Stopwatch.StartNew();
        var progressTimer = new System.Windows.Forms.Timer { Interval = 250 };
        Action<string> progressHandler = p => _lastProgressPath = p;
        Action<DirEntry> rootChildHandler = child =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(new Action(() => AddLiveRootChild(child)));
        };
        _scanner.ProgressChanged += progressHandler;
        _scanner.RootChildCompleted += rootChildHandler;
        progressTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _scanStartedUtc;
            _statusLabel.Text = $"Scanning... {_scanner.TotalScanned:N0} items | {elapsed:mm\\:ss} | {_lastProgressPath}";
            UpdateInsights(_tree.SelectedNode?.Tag as DirEntry ?? _liveRoot);
        };
        progressTimer.Start();

        try
        {
            var root = await Task.Run(() => _scanner.Scan(path, _cts.Token), _cts.Token);
            sw.Stop();
            _root = root;

            progressTimer.Stop();

            _statusLabel.Text = $"Done in {sw.Elapsed.TotalSeconds:N1}s — {root.FileCount:N0} files, {root.DirCount:N0} folders";
            _statusSize.Text = $"Total: {DirEntry.FormatSize(root.Size)}";

            PopulateTree(root);
            UpdateInsights(root);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Scan cancelled.";
            UpdateInsights(_liveRoot);
        }
        finally
        {
            progressTimer.Stop();
            progressTimer.Dispose();
            SetScanState(false);
            _scanner.ProgressChanged -= progressHandler;
            _scanner.RootChildCompleted -= rootChildHandler;
        }
    }

    private void StopScan()
    {
        _scanner?.Cancel();
        _cts?.Cancel();
    }

    private void SetScanState(bool scanning)
    {
        _isScanning = scanning;
        _progressBar.Visible = scanning;
        _scanButton.Enabled = !scanning;
        _browseButton.Enabled = !scanning;
        _driveCombo.Enabled = !scanning;
        _stopButton.Enabled = scanning;
    }

    private void PopulateTree(DirEntry root)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        _treeEmptyState.Visible = false;

        var rootNode = CreateTreeNode(root);
        rootNode.ImageKey = rootNode.SelectedImageKey = "drive";
        _tree.Nodes.Add(rootNode);

        // Add first level children directly
        foreach (var child in root.Children)
        {
            var childNode = CreateTreeNode(child);
            AddPlaceholder(childNode, child);
            rootNode.Nodes.Add(childNode);
        }

        rootNode.Expand();
        _tree.EndUpdate();

        if (rootNode.Nodes.Count > 0)
            _tree.SelectedNode = rootNode;

        UpdateInsights(root);
    }

    private void PrepareLiveScan(string path)
    {
        _root = null;
        _liveRoot = new DirEntry
        {
            Name = path,
            FullPath = path
        };

        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        _details.Items.Clear();
        _liveRootNode = new TreeNode($"{path}  (warming up...)")
        {
            Tag = _liveRoot,
            ImageKey = "drive",
            SelectedImageKey = "drive"
        };
        _tree.Nodes.Add(_liveRootNode);
        _tree.SelectedNode = _liveRootNode;
        _treeEmptyState.Visible = false;
        _tree.EndUpdate();

        UpdateInsights(_liveRoot);
        _usageChart.Caption = "Top folders will appear here while the scan runs";
        _usageChart.Entries = new List<DirEntry>();
        _usageChart.TotalSize = 0;
        _usageChart.Invalidate();
    }

    private void AddLiveRootChild(DirEntry child)
    {
        if (_liveRoot is null || _liveRootNode is null) return;

        _liveRoot.Children.Add(child);
        _liveRoot.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
        _liveRoot.Size += child.Size;
        _liveRoot.FileCount += child.FileCount;
        _liveRoot.DirCount += child.DirCount + 1;

        _liveRootNode.Text = $"{_liveRoot.Name}  ({DirEntry.FormatSize(_liveRoot.Size)})";
        var existing = FindTreeNode(_liveRootNode.Nodes, child);
        if (existing is null)
        {
            var childNode = CreateTreeNode(child);
            AddPlaceholder(childNode, child);
            _liveRootNode.Nodes.Add(childNode);
        }

        if (_tree.SelectedNode == _liveRootNode)
            PopulateDetails(_liveRoot);
        else
            UpdateInsights(_tree.SelectedNode?.Tag as DirEntry ?? _liveRoot);
    }

    private static TreeNode CreateTreeNode(DirEntry entry)
    {
        var pctText = "";
        var iconKey = entry.Size > 1L << 30 ? "folder-large" : "folder";
        var node = new TreeNode($"{entry.Name}  ({DirEntry.FormatSize(entry.Size)}){pctText}")
        {
            Tag = entry,
            ImageKey = iconKey,
            SelectedImageKey = iconKey
        };
        return node;
    }

    private static void AddPlaceholder(TreeNode node, DirEntry entry)
    {
        if (entry.Children.Count > 0)
            node.Nodes.Add(new TreeNode("Loading...") { ForeColor = Color.Gray });
    }

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is null) return;
        if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag is null)
        {
            // Replace placeholder with real children
            e.Node.Nodes.Clear();
            if (e.Node.Tag is DirEntry entry)
            {
                foreach (var child in entry.Children)
                {
                    var childNode = CreateTreeNode(child);
                    AddPlaceholder(childNode, child);
                    e.Node.Nodes.Add(childNode);
                }
            }
        }
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is DirEntry entry)
            PopulateDetails(entry);
    }

    private void PopulateDetails(DirEntry parent)
    {
        _details.BeginUpdate();
        _details.Items.Clear();

        foreach (var child in GetSortedChildren(parent))
        {
            var pctParent = parent.Size > 0 ? (double)child.Size / parent.Size : 0;
            var pctTotal = _root?.Size > 0 ? (double)child.Size / _root.Size : 0;
            var item = new ListViewItem(child.Name, child.Size > 1L << 30 ? "folder-large" : "folder")
            {
                Tag = child,
                UseItemStyleForSubItems = false
            };
            item.SubItems.Add(DirEntry.FormatSize(child.Size));
            item.SubItems.Add($"{pctParent:P1}");  // will be drawn as bar
            item.SubItems.Add($"{pctTotal:P1}");
            item.SubItems.Add($"{child.FileCount:N0}");
            item.SubItems.Add($"{child.DirCount:N0}");
            item.SubItems.Add(child.FullPath);
            _details.Items.Add(item);
        }

        _details.EndUpdate();
        UpdateInsights(parent);
    }

    private void UpdateInsights(DirEntry? focus)
    {
        var active = focus ?? _root ?? _liveRoot;
        _treeEmptyState.Visible = _tree.Nodes.Count == 0;
        var scanValue = _scanner is not null && _isScanning
            ? $"{_scanner.TotalScanned:N0}"
            : active is null
                ? "0"
                : $"{active.FileCount + active.DirCount:N0}";
        _scanInsight.Value = scanValue;
        _scanInsight.Caption = _isScanning ? "items scanned" : "indexed items";

        _sizeInsight.Value = active is null ? "--" : DirEntry.FormatSize(active.Size);
        _sizeInsight.Caption = active is null ? "waiting for scan" : TrimCaption(active.FullPath);

        var largestChild = active?.Children.OrderByDescending(c => c.Size).FirstOrDefault();
        _focusInsight.Value = largestChild is null ? "--" : largestChild.Name;
        _focusInsight.Caption = largestChild is null ? "largest folder" : DirEntry.FormatSize(largestChild.Size);

        _heroSubtitle.Text = _isScanning
            ? $"Scanning live. Current path: {TrimCaption(_lastProgressPath)}"
            : "See the largest folders first, compare proportions, and avoid guessing where the space went.";

        _usageChart.Caption = active is null
            ? "Top folders"
            : $"Top space consumers in {active.Name}";
        _usageChart.TotalSize = active?.Size ?? 0;
        _usageChart.Entries = active?.Children.Take(6).ToList() ?? new List<DirEntry>();
        _usageChart.Invalidate();
        _guidancePanel.Entry = active;
        _guidancePanel.IsScanning = _isScanning;
        _guidancePanel.Invalidate();
    }

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node is null) return;

        var selected = (e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected;
        var background = selected ? Color.FromArgb(42, 108, 166) : Color.FromArgb(18, 21, 27);
        var foreground = selected ? Color.White : Color.FromArgb(220, 220, 220);
        var rowRect = new Rectangle(0, e.Bounds.Top, _tree.ClientSize.Width, e.Bounds.Height);

        using var backgroundBrush = new SolidBrush(background);
        e.Graphics.FillRectangle(backgroundBrush, rowRect);

        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            _tree.Font,
            e.Bounds,
            foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if ((e.State & TreeNodeStates.Focused) == TreeNodeStates.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, foreground, background);
    }

    private static string TrimCaption(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Length <= 72 ? value : $"...{value[^69..]}";
    }

    private void HeroPanel_Paint(object? sender, PaintEventArgs e)
    {
        var rect = _heroPanel.ClientRectangle;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedRect(new Rectangle(0, 0, rect.Width - 1, rect.Height - 1), 22);
        using var brush = new LinearGradientBrush(rect, Color.FromArgb(22, 48, 76), Color.FromArgb(36, 98, 153), LinearGradientMode.Horizontal);
        e.Graphics.FillPath(brush, path);

        using var glowBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
        e.Graphics.FillEllipse(glowBrush, rect.Width - 180, -60, 220, 220);

        using var accentBrush = new SolidBrush(Color.FromArgb(26, 151, 209, 255));
        e.Graphics.FillEllipse(accentBrush, rect.Width - 120, rect.Height - 84, 96, 96);

        using var borderPen = new Pen(Color.FromArgb(58, 255, 255, 255));
        e.Graphics.DrawPath(borderPen, path);
    }

    private void CardPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;

        var rect = new Rectangle(0, 0, panel.ClientSize.Width - 1, panel.ClientSize.Height - 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedRect(rect, 18);
        using var bgBrush = new SolidBrush(panel.BackColor);
        using var borderPen = new Pen(Color.FromArgb(42, 72, 92));
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);
    }

    private void Splitter_Paint(object? sender, PaintEventArgs e)
    {
        var splitterRect = new Rectangle(_splitter.SplitterDistance, 0, _splitter.SplitterWidth, _splitter.Height);
        using var brush = new SolidBrush(Color.FromArgb(12, 14, 18));
        e.Graphics.FillRectangle(brush, splitterRect);

        var gripX = splitterRect.X + (splitterRect.Width / 2) - 1;
        using var gripBrush = new SolidBrush(Color.FromArgb(48, 92, 120));
        for (var y = splitterRect.Height / 2 - 20; y <= splitterRect.Height / 2 + 20; y += 10)
            e.Graphics.FillEllipse(gripBrush, gripX, y, 3, 3);
    }

    internal static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    // --- Custom drawing for the details list ---
    private void Details_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var bgBrush = new SolidBrush(Color.FromArgb(45, 45, 48));
        e.Graphics.FillRectangle(bgBrush, e.Bounds);
        using var fgBrush = new SolidBrush(Color.FromArgb(180, 180, 180));
        var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        if (e.Header is not null)
        {
            if (e.Header.TextAlign == HorizontalAlignment.Right)
                sf.Alignment = StringAlignment.Far;
            e.Graphics.DrawString(e.Header.Text, Font, fgBrush, e.Bounds, sf);
        }
        using var pen = new Pen(Color.FromArgb(60, 60, 65));
        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    private void Details_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null) return;

        // Background
        var bgColor = e.ItemIndex % 2 == 0 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(35, 35, 38);
        if (e.Item.Selected) bgColor = Color.FromArgb(0, 90, 158);
        using var bgBrush = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        // Column 2 (% of Parent) - draw as bar
        if (e.ColumnIndex == 2 && e.Item.Tag is DirEntry entry)
        {
            var parentEntry = _tree.SelectedNode?.Tag as DirEntry;
            var pctParent = parentEntry?.Size > 0 ? (double)entry.Size / parentEntry.Size : 0;
            var barRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, e.Bounds.Width - 8, e.Bounds.Height - 6);
            SizeBarRenderer.DrawBar(e.Graphics, barRect, pctParent, SizeBarRenderer.GetColor(e.ItemIndex));

            // Draw percentage text on top
            using var textBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString($"{pctParent:P1}", Font, textBrush, barRect, sf);
            return;
        }

        // Icon for first column
        if (e.ColumnIndex == 0)
        {
            if (_imageList.Images.ContainsKey(e.Item.ImageKey))
            {
                var img = _imageList.Images[e.Item.ImageKey];
                if (img is not null)
                    e.Graphics.DrawImage(img, e.Bounds.X + 4, e.Bounds.Y + (e.Bounds.Height - 18) / 2, 18, 18);
            }
            var textRect = new Rectangle(e.Bounds.X + 26, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height);
            using var fgBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            e.Graphics.DrawString(e.SubItem.Text, Font, fgBrush, textRect, sf);
            return;
        }

        // Default text draw
        {
            var fgColor = e.ColumnIndex == 6 ? Color.FromArgb(140, 140, 150) : Color.FromArgb(220, 220, 220);
            using var fgBrush = new SolidBrush(fgColor);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            if (e.Item.ListView?.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right)
                sf.Alignment = StringAlignment.Far;
            e.Graphics.DrawString(e.SubItem.Text, Font, fgBrush, e.Bounds, sf);
        }
    }

    private void Details_DoubleClick(object? sender, EventArgs e)
    {
        if (_details.SelectedItems.Count == 0) return;
        if (_details.SelectedItems[0].Tag is DirEntry entry && entry.Children.Count > 0)
        {
            // Navigate to this folder in the tree
            var node = FindTreeNode(_tree.Nodes, entry);
            if (node is not null)
            {
                _tree.SelectedNode = node;
                node.Expand();
            }
        }
    }

    private void Details_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_detailsSortColumn == e.Column)
            _detailsSortAscending = !_detailsSortAscending;
        else
        {
            _detailsSortColumn = e.Column;
            _detailsSortAscending = e.Column == 0;
        }

        if (_tree.SelectedNode?.Tag is DirEntry entry)
            PopulateDetails(entry);
    }

    private static TreeNode? FindTreeNode(TreeNodeCollection nodes, DirEntry target)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is DirEntry e && e.FullPath == target.FullPath)
                return node;
            // Expand and search children
            var found = FindTreeNode(node.Nodes, target);
            if (found is not null) return found;
        }
        return null;
    }

    private void ApplyFilter()
    {
        var filter = _searchBox.Text?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(filter))
        {
            // Show all
            if (_tree.SelectedNode?.Tag is DirEntry entry)
                PopulateDetails(entry);
            return;
        }

        // Filter details list
        if (_tree.SelectedNode?.Tag is DirEntry parent)
        {
            _details.BeginUpdate();
            _details.Items.Clear();
            foreach (var child in GetSortedChildren(parent))
            {
                if (!child.Name.ToLowerInvariant().Contains(filter) &&
                    !child.FullPath.ToLowerInvariant().Contains(filter))
                    continue;

                var pctParent = parent.Size > 0 ? (double)child.Size / parent.Size : 0;
                var pctTotal = _root?.Size > 0 ? (double)child.Size / _root.Size : 0;
                var item = new ListViewItem(child.Name, child.Size > 1L << 30 ? "folder-large" : "folder")
                {
                    Tag = child,
                    UseItemStyleForSubItems = false
                };
                item.SubItems.Add(DirEntry.FormatSize(child.Size));
                item.SubItems.Add($"{pctParent:P1}");
                item.SubItems.Add($"{pctTotal:P1}");
                item.SubItems.Add($"{child.FileCount:N0}");
                item.SubItems.Add($"{child.DirCount:N0}");
                item.SubItems.Add(child.FullPath);
                _details.Items.Add(item);
            }
            _details.EndUpdate();
        }
    }

    private IEnumerable<DirEntry> GetSortedChildren(DirEntry parent)
    {
        var children = parent.Children;
        Func<DirEntry, object> keySelector = _detailsSortColumn switch
        {
            0 => static child => child.Name,
            1 => static child => child.Size,
            2 => static child => child.Size,
            3 => static child => child.Size,
            4 => static child => child.FileCount,
            5 => static child => child.DirCount,
            6 => static child => child.FullPath,
            _ => static child => child.Size
        };

        return _detailsSortAscending
            ? children.OrderBy(keySelector).ToList()
            : children.OrderByDescending(keySelector).ToList();
    }

    private void EnsureDetailsSelectionAtCursor()
    {
        var point = _details.PointToClient(Cursor.Position);
        var hit = _details.HitTest(point);
        if (hit.Item is null) return;

        _details.SelectedItems.Clear();
        hit.Item.Selected = true;
        _details.FocusedItem = hit.Item;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopScan();
        base.OnFormClosing(e);
    }
}

// Dark theme renderer for toolstrip
file sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.FromArgb(220, 220, 220);
        base.OnRenderItemText(e);
    }
}

file sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripBorder => Color.FromArgb(20, 24, 30);
    public override Color ToolStripGradientBegin => Color.FromArgb(20, 24, 30);
    public override Color ToolStripGradientMiddle => Color.FromArgb(20, 24, 30);
    public override Color ToolStripGradientEnd => Color.FromArgb(20, 24, 30);
    public override Color MenuItemSelected => Color.FromArgb(38, 44, 52);
    public override Color MenuItemBorder => Color.FromArgb(56, 74, 90);
    public override Color SeparatorDark => Color.FromArgb(44, 54, 62);
    public override Color SeparatorLight => Color.FromArgb(44, 54, 62);
}

sealed class InsightCard : Panel
{
    private readonly Label _titleLabel;
    private readonly Label _valueLabel;
    private readonly Label _captionLabel;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => _valueLabel.Text;
        set => _valueLabel.Text = value;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => _captionLabel.Text;
        set => _captionLabel.Text = value;
    }

    public InsightCard(string title, string value, string caption)
    {
        Size = new Size(220, 54);
        Margin = new Padding(0, 0, 12, 0);
        BackColor = Color.FromArgb(30, 41, 57);

        _titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(150, 190, 215),
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            Location = new Point(12, 9),
            Text = title,
            BackColor = Color.Transparent
        };

        _valueLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            Location = new Point(12, 24),
            Text = value,
            BackColor = Color.Transparent
        };

        _captionLabel = new Label
        {
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(148, 165, 178),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(95, 26),
            Size = new Size(112, 18),
            Text = caption,
            BackColor = Color.Transparent
        };

        Controls.AddRange(new Control[] { _titleLabel, _valueLabel, _captionLabel });
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = MainForm.CreateRoundedRect(new Rectangle(0, 0, ClientRectangle.Width - 1, ClientRectangle.Height - 1), 14);
        using var brush = new SolidBrush(BackColor);
        using var borderPen = new Pen(Color.FromArgb(40, 255, 255, 255));
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(borderPen, path);
        base.OnPaint(e);
    }
}

sealed class UsageChartPanel : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public List<DirEntry> Entries { get; set; } = new();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public long TotalSize { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = "Top folders";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool CompactMode { get; set; }

    public UsageChartPanel()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(BackColor);
        using var path = MainForm.CreateRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 18);
        using var borderPen = new Pen(Color.FromArgb(42, 72, 92));
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        using var titleBrush = new SolidBrush(Color.White);
        using var subBrush = new SolidBrush(Color.FromArgb(136, 160, 180));
        e.Graphics.DrawString(Caption, new Font("Segoe UI Semibold", 11f, FontStyle.Bold), titleBrush, new PointF(18, 14));
        e.Graphics.DrawString("Largest children by size", new Font("Segoe UI", 8.5f), subBrush, new PointF(18, 36));

        if (Entries.Count == 0 || TotalSize <= 0)
        {
            e.Graphics.DrawString("Scan a drive to see the biggest folders surface here in real time.", new Font("Segoe UI", 10f), subBrush, new RectangleF(18, 82, Width - 36, 32));
            return;
        }

        var top = 68;
        var rowHeight = CompactMode ? 14 : 18;
        var gap = CompactMode ? 16 : 12;
        for (var index = 0; index < Entries.Count && index < 6; index++)
        {
            var entry = Entries[index];
            var fraction = TotalSize > 0 ? (double)entry.Size / TotalSize : 0;
            var y = top + index * (rowHeight + gap);
            var labelWidth = CompactMode ? Math.Max(120, Width - 36) : 190;
            var labelRect = new Rectangle(18, y - 2, labelWidth, rowHeight);
            var barY = CompactMode ? y + 18 : y;
            var barX = CompactMode ? 18 : 210;
            var barWidth = CompactMode ? Math.Max(110, Width - 154) : Math.Max(80, Width - 360);
            var barRect = new Rectangle(barX, barY, barWidth, rowHeight - 2);
            var valueRect = CompactMode
                ? new Rectangle(Math.Max(140, Width - 126), barY - 2, 108, rowHeight)
                : new Rectangle(Width - 136, y - 2, 118, rowHeight);

            using var labelBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
            e.Graphics.DrawString(entry.Name, new Font("Segoe UI", 9f), labelBrush, labelRect, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

            using var trackBrush = new SolidBrush(Color.FromArgb(38, 52, 68));
            FillRoundedBar(e.Graphics, trackBrush, barRect, 8);

            var fillWidth = Math.Max(6, (int)(barRect.Width * Math.Min(1d, fraction)));
            var fillRect = new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height);
            using var fillBrush = new SolidBrush(SizeBarRenderer.GetColor(index));
            FillRoundedBar(e.Graphics, fillBrush, fillRect, 8);

            using var valueBrush = new SolidBrush(Color.FromArgb(170, 210, 230));
            e.Graphics.DrawString($"{fraction:P1}  {DirEntry.FormatSize(entry.Size)}", new Font("Segoe UI", 8.5f), valueBrush, valueRect, new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap });
        }
    }

    private static void FillRoundedBar(Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}

sealed class FolderGuidancePanel : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DirEntry? Entry { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsScanning { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool CompactMode { get; set; }

    public FolderGuidancePanel()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(BackColor);
        using var path = MainForm.CreateRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 18);
        using var borderPen = new Pen(Color.FromArgb(42, 72, 92));
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        var guidance = GetGuidance(Entry);

        using var titleBrush = new SolidBrush(Color.White);
        using var textBrush = new SolidBrush(Color.FromArgb(160, 182, 198));
        using var subtleBrush = new SolidBrush(Color.FromArgb(120, 144, 160));
        using var badgeBrush = new SolidBrush(guidance.BadgeColor);
        using var badgeTextBrush = new SolidBrush(Color.White);

        e.Graphics.DrawString("Cleanup guidance", new Font("Segoe UI Semibold", 11f, FontStyle.Bold), titleBrush, new PointF(18, 16));
        e.Graphics.DrawString(guidance.Title, new Font("Segoe UI", 8.5f), subtleBrush, new PointF(18, 38));

        using var badgePath = MainForm.CreateRoundedRect(new Rectangle(18, 62, CompactMode ? 84 : 104, 28), 12);
        e.Graphics.FillPath(badgeBrush, badgePath);
        e.Graphics.DrawString(guidance.BadgeText, new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), badgeTextBrush, new RectangleF(18, 68, CompactMode ? 84 : 104, 18), new StringFormat { Alignment = StringAlignment.Center });

        var summaryTop = CompactMode ? 98 : 104;
        e.Graphics.DrawString(guidance.Summary, new Font("Segoe UI", 9f), textBrush, new RectangleF(18, summaryTop, Width - 36, CompactMode ? 34 : 44));

        var bulletTop = CompactMode ? 142 : 152;
        for (var index = 0; index < guidance.Tips.Length; index++)
        {
            var y = bulletTop + index * (CompactMode ? 24 : 28);
            using var dotBrush = new SolidBrush(index == 0 ? guidance.BadgeColor : Color.FromArgb(88, 104, 120));
            e.Graphics.FillEllipse(dotBrush, 20, y + 5, 8, 8);
            e.Graphics.DrawString(guidance.Tips[index], new Font("Segoe UI", 8.8f), textBrush, new RectangleF(36, y, Width - 54, 22));
        }

        if (IsScanning)
        {
            e.Graphics.DrawString("Guidance updates as selection changes during scan.", new Font("Segoe UI", 8f), subtleBrush, new RectangleF(18, Height - 28, Width - 36, 18));
        }
    }

    private static GuidanceModel GetGuidance(DirEntry? entry)
    {
        if (entry is null)
        {
            return new GuidanceModel(
                "Pick a folder",
                "Start with the biggest categories first",
                "Use the chart and folder list together. Large temp, download, and cache locations are usually your first review points.",
                "Review top-level folders before deleting anything",
                Color.FromArgb(78, 201, 176),
                "Review",
                new[]
                {
                    "Check Downloads, Temp, and recycle bin candidates first.",
                    "Avoid deleting system folders directly from Windows root.",
                    "Use app uninstall flows before removing program files manually."
                });
        }

        var path = entry.FullPath.ToLowerInvariant();
        var name = entry.Name.ToLowerInvariant();

        if (path.Contains("\\windows") || name is "windows" or "system32" or "winsxs" or "recovery" || path.Contains("system volume information"))
        {
            return new GuidanceModel(
                entry.Name,
                "Windows-managed content",
                "Do not delete files here manually unless you know the servicing and recovery impact. Use built-in cleanup tools instead.",
                "Avoid manual deletion",
                Color.FromArgb(209, 72, 54),
                "Avoid",
                new[]
                {
                    "Use Storage Sense or Disk Cleanup for Windows-managed files.",
                    "WinSxS and System32 are not normal cleanup targets.",
                    "Recovery and System Volume Information can break rollback or restore features."
                });
        }

        if (name.Contains("program files") || path.Contains("\\program files") || path.Contains("\\programdata") || path.Contains("\\appdata"))
        {
            return new GuidanceModel(
                entry.Name,
                "Application-owned data",
                "This space often belongs to installed apps. It can be reclaimed, but usually through uninstalling apps or clearing app-specific caches.",
                "Proceed carefully",
                Color.FromArgb(242, 170, 76),
                "Caution",
                new[]
                {
                    "Prefer uninstalling software instead of deleting folders by hand.",
                    "AppData may contain safe caches, but also active settings and app state.",
                    "ProgramData often stores shared app data used across users."
                });
        }

        if (name.Contains("downloads") || name == "temp" || path.Contains("\\temp") || name.Contains("recycle"))
        {
            return new GuidanceModel(
                entry.Name,
                "Typical user cleanup target",
                "This location is commonly worth reviewing first. Large installers, exports, cache files, and deleted items often accumulate here.",
                "Usually safe to review",
                Color.FromArgb(78, 201, 176),
                "Safer",
                new[]
                {
                    "Confirm files are not still needed before deleting them.",
                    "Temp folders are generally safer than system directories.",
                    "Empty the recycle bin after verifying recoverable items are not needed."
                });
        }

        return new GuidanceModel(
            entry.Name,
            "Needs a quick review",
            "This folder may be fine to clean, but the right action depends on what created the files and whether they are still in use.",
            "Review before deleting",
            Color.FromArgb(64, 140, 214),
            "Review",
            new[]
            {
                "Open the folder in Explorer if the contents are unfamiliar.",
                "Sort by size and age before deleting large unknown files.",
                "If it belongs to an installed app, look for that app's own cleanup or uninstall option."
            });
    }

    private sealed record GuidanceModel(string Title, string Subtitle, string Summary, string BadgeHint, Color BadgeColor, string BadgeText, string[] Tips);
}
