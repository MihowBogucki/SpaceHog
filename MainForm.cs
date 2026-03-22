using System.Diagnostics;

namespace SpaceHog;

public sealed class MainForm : Form
{
    private readonly TreeView _tree;
    private readonly ListView _details;
    private readonly SplitContainer _splitter;
    private readonly ToolStrip _toolbar;
    private readonly StatusStrip _statusBar;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _statusSize;
    private readonly ToolStripProgressBar _progressBar;
    private readonly ToolStripComboBox _driveCombo;
    private readonly ToolStripTextBox _searchBox;
    private readonly ImageList _imageList;

    private DirEntry? _root;
    private DirScanner? _scanner;
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = "SpaceHog - Disk Space Analyzer";
        Size = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(220, 220, 220);
        Font = new Font("Segoe UI", 9.5f);

        _imageList = new ImageList { ImageSize = new Size(18, 18), ColorDepth = ColorDepth.Depth32Bit };
        BuildImageList();

        // --- Toolbar ---
        _toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.FromArgb(220, 220, 220),
            Renderer = new DarkToolStripRenderer(),
            Padding = new Padding(4, 2, 4, 2)
        };

        _driveCombo = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.FromArgb(220, 220, 220),
            FlatStyle = FlatStyle.Flat
        };
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var label = $"{d.Name.TrimEnd('\\')}  ({DirEntry.FormatSize(d.TotalSize - d.AvailableFreeSpace)} / {DirEntry.FormatSize(d.TotalSize)})";
            _driveCombo.Items.Add(label);
        }
        if (_driveCombo.Items.Count > 0) _driveCombo.SelectedIndex = 0;

        var scanBtn = new ToolStripButton("  Scan  ") { BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White };
        scanBtn.Click += async (_, _) => await StartScan();

        var stopBtn = new ToolStripButton("  Stop  ") { ForeColor = Color.FromArgb(240, 128, 128) };
        stopBtn.Click += (_, _) => StopScan();

        var browseBtn = new ToolStripButton("  Browse...  ");
        browseBtn.Click += async (_, _) => await BrowseAndScan();

        _searchBox = new ToolStripTextBox
        {
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(180, 25)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();
        if (_searchBox.Control is TextBox tb)
            tb.PlaceholderText = "\U0001f50d Filter...";

        _toolbar.Items.AddRange(new ToolStripItem[] {
            new ToolStripLabel("Drive: "), _driveCombo,
            new ToolStripSeparator(), scanBtn, stopBtn, browseBtn,
            new ToolStripSeparator(),
            new ToolStripLabel("Filter: "), _searchBox
        });

        // --- Tree view ---
        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9.5f),
            ImageList = _imageList,
            BorderStyle = BorderStyle.None,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            FullRowSelect = true,
            ItemHeight = 24
        };
        _tree.AfterSelect += Tree_AfterSelect;
        _tree.BeforeExpand += Tree_BeforeExpand;

        // --- Details ListView ---
        _details = new ListView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 9.5f),
            View = View.Details,
            FullRowSelect = true,
            BorderStyle = BorderStyle.None,
            SmallImageList = _imageList,
            OwnerDraw = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
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

        // --- Splitter ---
        _splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = Color.FromArgb(45, 45, 48),
            SplitterDistance = 380,
            SplitterWidth = 4
        };
        _splitter.Panel1.Controls.Add(_tree);
        _splitter.Panel2.Controls.Add(_details);

        // --- Status bar ---
        _statusBar = new StatusStrip
        {
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            SizingGrip = false
        };
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
        _statusSize = new ToolStripStatusLabel("") { TextAlign = System.Drawing.ContentAlignment.MiddleRight, AutoSize = false, Width = 300 };
        _progressBar = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, Visible = false, Size = new Size(150, 16) };
        _statusBar.Items.AddRange(new ToolStripItem[] { _statusLabel, _progressBar, _statusSize });

        // --- Layout ---
        Controls.Add(_splitter);
        Controls.Add(_toolbar);
        Controls.Add(_statusBar);

        // Context menu on tree
        var ctxMenu = new ContextMenuStrip();
        var openFolder = new ToolStripMenuItem("Open in Explorer");
        openFolder.Click += (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is DirEntry entry)
            {
                try { Process.Start("explorer.exe", entry.FullPath); } catch { }
            }
        };
        var copyPath = new ToolStripMenuItem("Copy Path");
        copyPath.Click += (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is DirEntry entry)
                Clipboard.SetText(entry.FullPath);
        };
        ctxMenu.Items.AddRange(new ToolStripItem[] { openFolder, copyPath });
        _tree.ContextMenuStrip = ctxMenu;
        _details.ContextMenuStrip = ctxMenu;
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

        _tree.Nodes.Clear();
        _details.Items.Clear();
        _progressBar.Visible = true;
        _statusLabel.Text = $"Scanning {path}...";
        _statusSize.Text = "";

        var sw = Stopwatch.StartNew();
        var progressTimer = new System.Windows.Forms.Timer { Interval = 250 };
        var lastPath = "";
        _scanner.ProgressChanged += p => lastPath = p;
        progressTimer.Tick += (_, _) =>
        {
            _statusLabel.Text = $"Scanning... {_scanner.TotalScanned:N0} items | {lastPath}";
        };
        progressTimer.Start();

        try
        {
            var root = await Task.Run(() => _scanner.Scan(path), _cts.Token);
            sw.Stop();
            _root = root;

            progressTimer.Stop();
            _progressBar.Visible = false;

            _statusLabel.Text = $"Done in {sw.Elapsed.TotalSeconds:N1}s — {root.FileCount:N0} files, {root.DirCount:N0} folders";
            _statusSize.Text = $"Total: {DirEntry.FormatSize(root.Size)}";

            PopulateTree(root);
        }
        catch (OperationCanceledException)
        {
            progressTimer.Stop();
            _progressBar.Visible = false;
            _statusLabel.Text = "Scan cancelled.";
        }
    }

    private void StopScan()
    {
        _scanner?.Cancel();
        _cts?.Cancel();
    }

    private void PopulateTree(DirEntry root)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

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

        foreach (var child in parent.Children)
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
            foreach (var child in parent.Children)
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
    public override Color ToolStripBorder => Color.FromArgb(45, 45, 48);
    public override Color ToolStripGradientBegin => Color.FromArgb(45, 45, 48);
    public override Color ToolStripGradientMiddle => Color.FromArgb(45, 45, 48);
    public override Color ToolStripGradientEnd => Color.FromArgb(45, 45, 48);
    public override Color MenuItemSelected => Color.FromArgb(62, 62, 66);
    public override Color MenuItemBorder => Color.FromArgb(62, 62, 66);
    public override Color SeparatorDark => Color.FromArgb(60, 60, 65);
    public override Color SeparatorLight => Color.FromArgb(60, 60, 65);
}
