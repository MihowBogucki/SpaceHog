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
    private readonly FlowLayoutPanel _breadcrumbFlow;
    private readonly FlowLayoutPanel _filterChipFlow;
    private readonly Button _backButton;
    private readonly Button _forwardButton;
    private readonly Button _densityButton;
    private readonly Dictionary<string, Button> _filterChipButtons = new(StringComparer.OrdinalIgnoreCase);

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
    private readonly List<string> _navigationHistory = new();
    private int _navigationIndex = -1;
    private bool _suppressHistoryPush;
    private string _activeCategoryFilter = "All";
    private bool _comfortableDensity = true;

    public MainForm()
    {
        Text = "SpaceHog";
        Size = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiTheme.AppBackground;
        ForeColor = UiTheme.TextPrimary;
        Font = UiTheme.BodyFont;
        MinimumSize = new Size(980, 640);
        DoubleBuffered = true;
        Icon = CreatePigIcon();

        _imageList = new ImageList { ImageSize = new Size(18, 18), ColorDepth = ColorDepth.Depth32Bit };
        BuildImageList();

        // --- Header ---
        _headerPanel = new Panel
        {
            BackColor = UiTheme.SurfaceHeader,
            Dock = DockStyle.Top,
            Height = 98,
            Padding = new Padding(22, 16, 22, 14)
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
            Font = UiTheme.BrandFont,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 2),
            BackColor = Color.Transparent
        };
        _brandSubtitle = new Label
        {
            AutoSize = true,
            Text = "Live disk visibility with cleanup guidance built in",
            Font = UiTheme.BodySmallFont,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(2, 44),
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
            Padding = new Padding(0, 14, 12, 0)
        };

        var driveLabel = new Label
        {
            AutoSize = true,
            Text = "Drive",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.TextMuted,
            Margin = new Padding(0, 10, 10, 0)
        };

        var drivePicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = UiTheme.SurfaceRaised,
            ForeColor = UiTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Width = 210,
            Margin = new Padding(0, 2, 10, 0)
        };
        _driveCombo = drivePicker;
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var label = $"{d.Name.TrimEnd('\\')}  ({DirEntry.FormatSize(d.TotalSize - d.AvailableFreeSpace)} / {DirEntry.FormatSize(d.TotalSize)})";
            _driveCombo.Items.Add(label);
        }
        if (_driveCombo.Items.Count > 0) _driveCombo.SelectedIndex = 0;

        _scanButton = CreateHeaderButton("Scan", UiTheme.Accent, UiTheme.TextPrimary);
        _scanButton.Click += async (_, _) => await StartScan();

        _stopButton = CreateHeaderButton("Stop", UiTheme.SurfaceRaised, UiTheme.Warning);
        _stopButton.Click += (_, _) => StopScan();

        _browseButton = CreateHeaderButton("Browse...", UiTheme.SurfaceRaised, UiTheme.TextPrimary);
        _browseButton.Click += async (_, _) => await BrowseAndScan();

        drivePanel.Controls.AddRange(new Control[] { driveLabel, _driveCombo, _scanButton, _stopButton, _browseButton });

        var searchPanel = new Panel
        {
            Width = 300,
            Height = 46,
            Margin = new Padding(0, 12, 0, 0),
            BackColor = UiTheme.SurfaceInset
        };
        searchPanel.Paint += SearchPanel_Paint;

        _searchBox = new TextBox
        {
            BackColor = UiTheme.SurfaceRaised,
            ForeColor = UiTheme.TextPrimary,
            BorderStyle = BorderStyle.None,
            Size = new Size(242, 24),
            Location = new Point(40, 11),
            Font = UiTheme.BodyFont
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
            BackColor = UiTheme.SurfacePanel,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.BodyFont,
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
            BackColor = UiTheme.SurfacePanel,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.BodyFont,
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
            Font = UiTheme.SectionTitleFont,
            ForeColor = UiTheme.TextPrimary,
            Padding = new Padding(20, 12, 18, 0)
        };

        _treeHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "Scan a drive to surface the biggest directories first.",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.TextSubtle,
            Padding = new Padding(20, 0, 18, 4)
        };

        _treeHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 10),
            BackColor = Color.Transparent
        };
        _treeEmptyState = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceInset
        };
        _treeEmptyState.Paint += TreeEmptyState_Paint;
        _treeHost.Controls.Add(_tree);
        _treeHost.Controls.Add(_treeEmptyState);

        _treePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceCard,
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
            BackColor = UiTheme.SurfaceCard,
            Padding = new Padding(24, 22, 24, 18),
            Margin = new Padding(0)
        };
        _heroPanel.Paint += HeroPanel_Paint;

        _heroTitle = new Label
        {
            AutoSize = true,
            Text = "SpaceHog",
            Font = UiTheme.HeroTitleFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(24, 18)
        };

        _heroSubtitle = new Label
        {
            AutoSize = false,
            Text = "Live disk analysis with a faster sense of what is safe to inspect and what deserves caution.",
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextSoft,
            BackColor = Color.Transparent,
            Location = new Point(24, 58),
            Size = new Size(720, 42)
        };

        _insightFlow = new FlowLayoutPanel
        {
            Location = new Point(24, 110),
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
            BackColor = UiTheme.SurfaceCard,
            Margin = new Padding(0),
            Padding = new Padding(20, 16, 20, 16)
        };

        _guidancePanel = new FolderGuidancePanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceCard,
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
            BackColor = UiTheme.SurfaceCard,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        detailsPanel.Paint += CardPanel_Paint;

        var detailsTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = "Contents",
            Font = UiTheme.SectionTitleFont,
            ForeColor = UiTheme.TextPrimary,
            Padding = new Padding(20, 12, 18, 0)
        };

        var detailsHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "Open folders, compare size share, and filter by name or path.",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.TextSubtle,
            Padding = new Padding(20, 0, 18, 4)
        };

        var detailsToolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(18, 0, 18, 0)
        };
        detailsToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        detailsToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        detailsToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        detailsToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _backButton = CreateToolbarButton("←", "Back");
        _backButton.Click += (_, _) => NavigateHistory(-1);

        _forwardButton = CreateToolbarButton("→", "Forward");
        _forwardButton.Click += (_, _) => NavigateHistory(1);

        _breadcrumbFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(10, 4, 8, 0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        _densityButton = CreateToolbarButton("Aa", "Toggle density");
        _densityButton.Width = 40;
        _densityButton.Click += (_, _) => ToggleDensity();

        detailsToolbar.Controls.Add(_backButton, 0, 0);
        detailsToolbar.Controls.Add(_forwardButton, 1, 0);
        detailsToolbar.Controls.Add(_breadcrumbFlow, 2, 0);
        detailsToolbar.Controls.Add(_densityButton, 3, 0);

        _filterChipFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.Transparent,
            WrapContents = false,
            AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(18, 2, 18, 2)
        };
        ConfigureFilterChips();

        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 10),
            BackColor = Color.Transparent
        };
        detailsHost.Controls.Add(_details);
        detailsPanel.Controls.Add(detailsHost);
        detailsPanel.Controls.Add(_filterChipFlow);
        detailsPanel.Controls.Add(detailsToolbar);
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
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 182));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 208));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rightLayout.Controls.Add(_heroPanel, 0, 0);
        _rightLayout.Controls.Add(_analyticsLayout, 0, 1);
        _rightLayout.Controls.Add(detailsPanel, 0, 2);

        _shellLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.AppBackground,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(16, 14, 16, 12)
        };
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // --- Splitter ---
        _splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = UiTheme.AppBackground,
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
            BackColor = UiTheme.SurfaceHeader,
            ForeColor = UiTheme.TextSoft,
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
            BackColor = UiTheme.SurfaceCard,
            ForeColor = UiTheme.TextPrimary
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
            BackColor = UiTheme.SurfaceCard,
            ForeColor = UiTheme.TextPrimary
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
        ApplyDensityMode();
        UpdateNavigationButtons();
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
            Font = UiTheme.ButtonFont,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(16, 6, 16, 6),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private static Button CreateToolbarButton(string text, string accessibleName)
    {
        return new Button
        {
            AutoSize = false,
            Width = 34,
            Height = 24,
            Text = text,
            AccessibleName = accessibleName,
            FlatStyle = FlatStyle.Flat,
            BackColor = UiTheme.SurfaceRaised,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.CaptionFont,
            Margin = new Padding(0, 4, 6, 0),
            Padding = new Padding(0),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private Button CreateFilterChip(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            Height = 24,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.MicroFont,
            Margin = new Padding(0, 2, 8, 0),
            Padding = new Padding(10, 3, 10, 3),
            FlatAppearance = { BorderSize = 0 }
        };
        button.Click += (_, _) => SetActiveCategoryFilter(text);
        return button;
    }

    private Button CreateBreadcrumbButton(string text, string fullPath)
    {
        var button = new Button
        {
            AutoSize = true,
            Height = 24,
            Text = text,
            Tag = fullPath,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.MicroFont,
            BackColor = Color.Transparent,
            ForeColor = UiTheme.TextSoft,
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(1, 0, 1, 0),
            FlatAppearance = { BorderSize = 0 }
        };
        button.Click += (_, _) => NavigateToPath(fullPath, true);
        return button;
    }

    private void ConfigureFilterChips()
    {
        foreach (var label in new[] { "All", "Big", "User", "Temp", "System" })
        {
            var chip = CreateFilterChip(label);
            _filterChipButtons[label] = chip;
            _filterChipFlow.Controls.Add(chip);
        }

        UpdateFilterChipStyles();
    }

    private void SetActiveCategoryFilter(string filter)
    {
        _activeCategoryFilter = filter;
        UpdateFilterChipStyles();

        if (_tree.SelectedNode?.Tag is DirEntry entry)
            PopulateDetails(entry);
    }

    private void UpdateFilterChipStyles()
    {
        foreach (var pair in _filterChipButtons)
        {
            var active = string.Equals(pair.Key, _activeCategoryFilter, StringComparison.OrdinalIgnoreCase);
            pair.Value.BackColor = active ? UiTheme.Accent : UiTheme.SurfaceRaised;
            pair.Value.ForeColor = active ? UiTheme.TextPrimary : UiTheme.TextSoft;
        }
    }

    private void ToggleDensity()
    {
        _comfortableDensity = !_comfortableDensity;
        ApplyDensityMode();
    }

    private void ApplyDensityMode()
    {
        _densityButton.Text = _comfortableDensity ? "Aa" : "A";
        _tree.ItemHeight = _comfortableDensity ? 30 : 24;
        _tree.Font = _comfortableDensity ? UiTheme.BodyFont : UiTheme.BodySmallFont;
        _details.Font = _comfortableDensity ? UiTheme.BodyFont : UiTheme.BodySmallFont;
        UpdateResponsiveLayout();
        _tree.Invalidate();
        _details.Invalidate();
    }

    private void NavigateHistory(int delta)
    {
        var nextIndex = _navigationIndex + delta;
        if (nextIndex < 0 || nextIndex >= _navigationHistory.Count)
            return;

        _navigationIndex = nextIndex;
        _suppressHistoryPush = true;
        NavigateToPath(_navigationHistory[_navigationIndex], false);
        UpdateNavigationButtons();
    }

    private void PushNavigation(string fullPath)
    {
        if (_navigationIndex >= 0 && _navigationIndex < _navigationHistory.Count &&
            string.Equals(_navigationHistory[_navigationIndex], fullPath, StringComparison.OrdinalIgnoreCase))
        {
            UpdateNavigationButtons();
            return;
        }

        if (_navigationIndex < _navigationHistory.Count - 1)
            _navigationHistory.RemoveRange(_navigationIndex + 1, _navigationHistory.Count - _navigationIndex - 1);

        _navigationHistory.Add(fullPath);
        _navigationIndex = _navigationHistory.Count - 1;
        UpdateNavigationButtons();
    }

    private void ResetNavigation()
    {
        _navigationHistory.Clear();
        _navigationIndex = -1;
        UpdateNavigationButtons();
        _breadcrumbFlow.Controls.Clear();
    }

    private void UpdateNavigationButtons()
    {
        _backButton.Enabled = _navigationIndex > 0;
        _forwardButton.Enabled = _navigationIndex >= 0 && _navigationIndex < _navigationHistory.Count - 1;
    }

    private void NavigateToPath(string fullPath, bool pushHistory)
    {
        var entry = FindEntryByPath(_root ?? _liveRoot, fullPath);
        if (entry is null)
            return;

        var node = FindTreeNode(_tree.Nodes, entry);
        if (node is not null)
        {
            _tree.SelectedNode = node;
            node.Expand();
        }
        else
        {
            if (pushHistory)
                PushNavigation(entry.FullPath);
            PopulateDetails(entry);
        }
    }

    private void UpdateBreadcrumbs(DirEntry entry)
    {
        _breadcrumbFlow.SuspendLayout();
        _breadcrumbFlow.Controls.Clear();

        var root = _root ?? _liveRoot;
        if (root is null)
        {
            _breadcrumbFlow.ResumeLayout();
            return;
        }

        var fullCrumbs = BuildBreadcrumbEntries(root, entry);
        var crumbs = fullCrumbs.Count > 4
            ? new List<DirEntry> { fullCrumbs[0], fullCrumbs[^2], fullCrumbs[^1] }
            : fullCrumbs;

        for (var index = 0; index < crumbs.Count; index++)
        {
            var crumb = crumbs[index];
            _breadcrumbFlow.Controls.Add(CreateBreadcrumbButton(crumb.Name, crumb.FullPath));

            if (index == 0 && fullCrumbs.Count > 4)
            {
                _breadcrumbFlow.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = "› ... ›",
                    Font = UiTheme.MicroFont,
                    ForeColor = UiTheme.TextSubtle,
                    Margin = new Padding(2, 4, 8, 0)
                });
                continue;
            }

            if (!string.Equals(crumb.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                _breadcrumbFlow.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = "›",
                    Font = UiTheme.MicroFont,
                    ForeColor = UiTheme.TextSubtle,
                    Margin = new Padding(2, 4, 8, 0)
                });
            }
        }

        _breadcrumbFlow.ResumeLayout();
    }

    private static List<DirEntry> BuildBreadcrumbEntries(DirEntry root, DirEntry entry)
    {
        var crumbs = new List<DirEntry>();
        var rootPath = root.FullPath.TrimEnd('\\');
        var currentPath = entry.FullPath.TrimEnd('\\');

        while (!string.IsNullOrEmpty(currentPath))
        {
            var match = FindEntryByPath(root, currentPath);
            if (match is not null)
                crumbs.Add(match);

            if (string.Equals(currentPath, rootPath, StringComparison.OrdinalIgnoreCase))
                break;

            currentPath = Path.GetDirectoryName(currentPath)?.TrimEnd('\\') ?? string.Empty;
        }

        crumbs.Reverse();
        return crumbs.Count == 0 ? new List<DirEntry> { entry } : crumbs;
    }

    private static DirEntry? FindEntryByPath(DirEntry? root, string fullPath)
    {
        if (root is null) return null;
        if (string.Equals(root.FullPath.TrimEnd('\\'), fullPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in root.Children)
        {
            var match = FindEntryByPath(child, fullPath);
            if (match is not null)
                return match;
        }

        return null;
    }

    private IEnumerable<DirEntry> GetVisibleChildren(DirEntry parent)
    {
        var searchFilter = _searchBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        foreach (var child in GetSortedChildren(parent))
        {
            if (!string.IsNullOrEmpty(searchFilter) &&
                !child.Name.ToLowerInvariant().Contains(searchFilter) &&
                !child.FullPath.ToLowerInvariant().Contains(searchFilter))
            {
                continue;
            }

            if (!MatchesCategoryFilter(child, parent))
                continue;

            yield return child;
        }
    }

    private bool MatchesCategoryFilter(DirEntry entry, DirEntry parent)
    {
        var path = entry.FullPath.ToLowerInvariant();
        var name = entry.Name.ToLowerInvariant();

        return _activeCategoryFilter switch
        {
            "All" => true,
            "Big" => parent.Size > 0 && (double)entry.Size / parent.Size >= 0.01d,
            "User" => path.Contains("\\users\\") || name is "downloads" or "desktop" or "documents" or "pictures" or "source" or "repos",
            "Temp" => name.Contains("temp") || name.Contains("cache") || name.Contains("packages") || name.Contains("recycle") || path.Contains("\\temp") || path.Contains("\\cache"),
            "System" => path.Contains("\\windows") || path.Contains("\\program files") || path.Contains("\\programdata") || name is "system32" or "winsxs" || path.Contains("system volume information"),
            _ => true
        };
    }

    private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var rect = _headerPanel.ClientRectangle;
        using var brush = new LinearGradientBrush(rect, UiTheme.SurfaceHeaderDeep, UiTheme.SurfaceHeader, LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, rect);
        using var pen = new Pen(UiTheme.BorderStrong);
        e.Graphics.DrawLine(pen, 0, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
    }

    private void SearchPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 14);
        using var bgBrush = new SolidBrush(UiTheme.SurfaceInset);
        using var borderPen = new Pen(UiTheme.Border);
        using var iconBrush = new SolidBrush(UiTheme.TextSubtle);
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);
        e.Graphics.DrawString("⌕", UiTheme.IconFont, iconBrush, new PointF(12, 10));
    }

    private void TreeEmptyState_Paint(object? sender, PaintEventArgs e)
    {
        if (!_treeEmptyState.Visible) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(UiTheme.SurfaceInset);
        using var titleBrush = new SolidBrush(UiTheme.TextPrimary);
        using var bodyBrush = new SolidBrush(UiTheme.TextSoft);
        using var accentBrush = new SolidBrush(Color.FromArgb(52, UiTheme.Accent));
        var centerX = _treeEmptyState.ClientSize.Width / 2f;
        e.Graphics.FillEllipse(accentBrush, centerX - 36, 116, 72, 72);
        e.Graphics.DrawString("🐷", UiTheme.EmptyStateIconFont, titleBrush, new PointF(centerX - 22, 124));
        var sf = new StringFormat { Alignment = StringAlignment.Center };
        e.Graphics.DrawString("Start a scan to build your folder map", UiTheme.EmptyStateTitleFont, titleBrush, new RectangleF(30, 210, _treeEmptyState.Width - 60, 26), sf);
        e.Graphics.DrawString("The left pane will populate live while SpaceHog analyzes the selected drive.", UiTheme.BodyFont, bodyBrush, new RectangleF(40, 244, _treeEmptyState.Width - 80, 48), sf);
    }

    private void UpdateResponsiveLayout()
    {
        var contentWidth = _splitter.Panel2.ClientSize.Width;
        if (contentWidth <= 0) return;

        var compact = contentWidth < 560;
        var medium = contentWidth < 760;

        int heroHeight = compact ? 228 : medium ? 196 : 168;
        _rightLayout.RowStyles[0].Height = heroHeight;
        _rightLayout.RowStyles[1].Height = compact ? 254 : 198;

        _heroSubtitle.Width = Math.Max(220, _heroPanel.ClientSize.Width - 44);
        _heroSubtitle.Height = compact ? 46 : 34;
        _heroSubtitle.MaximumSize = new Size(Math.Max(220, _heroPanel.ClientSize.Width - 44), 0);

        _insightFlow.Location = new Point(24, compact ? 102 : 92);
        _insightFlow.Size = new Size(Math.Max(220, _heroPanel.ClientSize.Width - 44), compact ? 98 : 76);

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

        _usageChart.CompactMode = contentWidth < 620 || !_comfortableDensity;
        _usageChart.Invalidate();
        _guidancePanel.CompactMode = contentWidth < 700 || !_comfortableDensity;
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
        ResetNavigation();
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
        {
            if (_suppressHistoryPush)
                _suppressHistoryPush = false;
            else
                PushNavigation(entry.FullPath);

            PopulateDetails(entry);
        }
    }

    private void PopulateDetails(DirEntry parent)
    {
        _details.BeginUpdate();
        _details.Items.Clear();

        foreach (var child in GetVisibleChildren(parent))
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
        UpdateBreadcrumbs(parent);
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
        var background = selected ? Color.FromArgb(42, 108, 166) : UiTheme.SurfacePanel;
        var foreground = UiTheme.TextPrimary;
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
        using var brush = new LinearGradientBrush(rect, UiTheme.HeroLeft, UiTheme.HeroRight, LinearGradientMode.Horizontal);
        e.Graphics.FillPath(brush, path);

        using var glowBrush = new SolidBrush(Color.FromArgb(34, 255, 255, 255));
        e.Graphics.FillEllipse(glowBrush, rect.Width - 180, -60, 220, 220);

        using var accentBrush = new SolidBrush(Color.FromArgb(24, UiTheme.Accent));
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
        using var borderPen = new Pen(UiTheme.Border);
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);
    }

    private void Splitter_Paint(object? sender, PaintEventArgs e)
    {
        var splitterRect = new Rectangle(_splitter.SplitterDistance, 0, _splitter.SplitterWidth, _splitter.Height);
        using var brush = new SolidBrush(UiTheme.AppBackground);
        e.Graphics.FillRectangle(brush, splitterRect);

        var gripX = splitterRect.X + (splitterRect.Width / 2) - 1;
        using var gripBrush = new SolidBrush(UiTheme.BorderStrong);
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
        using var bgBrush = new SolidBrush(UiTheme.SurfaceRaised);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);
        using var fgBrush = new SolidBrush(UiTheme.TextMuted);
        var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        if (e.Header is not null)
        {
            if (e.Header.TextAlign == HorizontalAlignment.Right)
                sf.Alignment = StringAlignment.Far;
            e.Graphics.DrawString(e.Header.Text, UiTheme.CaptionFont, fgBrush, e.Bounds, sf);
        }
        using var pen = new Pen(UiTheme.Border);
        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        if (e.ColumnIndex == _detailsSortColumn)
        {
            var arrow = _detailsSortAscending ? "▲" : "▼";
            using var sortBrush = new SolidBrush(UiTheme.AccentSoft);
            e.Graphics.DrawString(arrow, UiTheme.CaptionFont, sortBrush, new RectangleF(e.Bounds.Right - 14, e.Bounds.Y + 6, 10, 12));
        }
    }

    private void Details_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null) return;

        // Background
        var bgColor = e.ItemIndex % 2 == 0 ? UiTheme.SurfacePanel : UiTheme.SurfaceInset;
        if (e.Item.Selected) bgColor = Color.FromArgb(42, 108, 166);
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
            using var textBrush = new SolidBrush(UiTheme.TextPrimary);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString($"{pctParent:P1}", UiTheme.CaptionFont, textBrush, barRect, sf);
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
            using var fgBrush = new SolidBrush(UiTheme.TextPrimary);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            e.Graphics.DrawString(e.SubItem.Text, UiTheme.BodyFont, fgBrush, textRect, sf);
            return;
        }

        // Default text draw
        {
            var fgColor = e.ColumnIndex == 6 ? UiTheme.TextSubtle : UiTheme.TextPrimary;
            using var fgBrush = new SolidBrush(fgColor);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            if (e.Item.ListView?.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right)
                sf.Alignment = StringAlignment.Far;
            e.Graphics.DrawString(e.SubItem.Text, UiTheme.BodyFont, fgBrush, e.Bounds, sf);
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
        if (_tree.SelectedNode?.Tag is DirEntry parent)
            PopulateDetails(parent);
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
