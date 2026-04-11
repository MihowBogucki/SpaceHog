using System.Drawing.Drawing2D;

namespace SpaceHog;

internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = UiTheme.TextPrimary;
        base.OnRenderItemText(e);
    }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripBorder => UiTheme.Border;
    public override Color ToolStripGradientBegin => UiTheme.SurfaceCard;
    public override Color ToolStripGradientMiddle => UiTheme.SurfaceCard;
    public override Color ToolStripGradientEnd => UiTheme.SurfaceCard;
    public override Color MenuItemSelected => UiTheme.SurfaceRaised;
    public override Color MenuItemBorder => UiTheme.BorderStrong;
    public override Color SeparatorDark => UiTheme.Border;
    public override Color SeparatorLight => UiTheme.Border;
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
        Size = new Size(228, 64);
        Margin = new Padding(0, 0, 12, 0);
        BackColor = Color.FromArgb(32, 44, 62);

        _titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.CaptionFont,
            Location = new Point(14, 10),
            Text = title,
            BackColor = Color.Transparent
        };

        _valueLabel = new Label
        {
            AutoSize = true,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.KpiFont,
            Location = new Point(14, 28),
            Text = value,
            BackColor = Color.Transparent
        };

        _captionLabel = new Label
        {
            AutoEllipsis = true,
            ForeColor = UiTheme.TextSubtle,
            Font = UiTheme.CaptionFont,
            Location = new Point(108, 31),
            Size = new Size(106, 18),
            Text = caption,
            BackColor = Color.Transparent
        };

        Controls.AddRange(new Control[] { _titleLabel, _valueLabel, _captionLabel });
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = MainForm.CreateRoundedRect(new Rectangle(0, 0, ClientRectangle.Width - 1, ClientRectangle.Height - 1), 12);
        using var brush = new SolidBrush(BackColor);
        using var borderPen = new Pen(Color.FromArgb(48, 255, 255, 255));
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
        using var borderPen = new Pen(UiTheme.Border);
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        using var titleBrush = new SolidBrush(UiTheme.TextPrimary);
        using var subBrush = new SolidBrush(UiTheme.TextSubtle);
        e.Graphics.DrawString(Caption, UiTheme.SectionTitleFont, titleBrush, new PointF(18, 14));
        e.Graphics.DrawString("Largest children by size", UiTheme.CaptionFont, subBrush, new PointF(18, 36));

        if (Entries.Count == 0 || TotalSize <= 0)
        {
            e.Graphics.DrawString("Scan a drive to see the biggest folders surface here in real time.", UiTheme.BodyFont, subBrush, new RectangleF(18, 82, Width - 36, 32));
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

            using var labelBrush = new SolidBrush(UiTheme.TextPrimary);
            e.Graphics.DrawString(entry.Name, UiTheme.BodyFont, labelBrush, labelRect, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

            using var trackBrush = new SolidBrush(UiTheme.SurfaceRaised);
            FillRoundedBar(e.Graphics, trackBrush, barRect, 8);

            var fillWidth = Math.Max(6, (int)(barRect.Width * Math.Min(1d, fraction)));
            var fillRect = new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height);
            using var fillBrush = new SolidBrush(SizeBarRenderer.GetColor(index));
            FillRoundedBar(e.Graphics, fillBrush, fillRect, 8);

            using var valueBrush = new SolidBrush(UiTheme.AccentSoft);
            e.Graphics.DrawString($"{fraction:P1}  {DirEntry.FormatSize(entry.Size)}", UiTheme.CaptionFont, valueBrush, valueRect, new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap });
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
        using var borderPen = new Pen(UiTheme.Border);
        e.Graphics.FillPath(bgBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        var guidance = GetGuidance(Entry);

        using var titleBrush = new SolidBrush(UiTheme.TextPrimary);
        using var textBrush = new SolidBrush(UiTheme.TextSoft);
        using var subtleBrush = new SolidBrush(UiTheme.TextSubtle);
        using var badgeBrush = new SolidBrush(guidance.BadgeColor);
        using var badgeTextBrush = new SolidBrush(UiTheme.TextPrimary);

        e.Graphics.DrawString("Cleanup guidance", UiTheme.SectionTitleFont, titleBrush, new PointF(18, 16));
        e.Graphics.DrawString(guidance.Title, UiTheme.CaptionFont, subtleBrush, new PointF(18, 38));

        using var badgePath = MainForm.CreateRoundedRect(new Rectangle(18, 62, CompactMode ? 84 : 104, 28), 12);
        e.Graphics.FillPath(badgeBrush, badgePath);
        e.Graphics.DrawString(guidance.BadgeText, UiTheme.CaptionFont, badgeTextBrush, new RectangleF(18, 68, CompactMode ? 84 : 104, 18), new StringFormat { Alignment = StringAlignment.Center });

        var summaryTop = CompactMode ? 98 : 104;
        e.Graphics.DrawString(guidance.Summary, UiTheme.BodyFont, textBrush, new RectangleF(18, summaryTop, Width - 36, CompactMode ? 34 : 44));

        var bulletTop = CompactMode ? 142 : 152;
        for (var index = 0; index < guidance.Tips.Length; index++)
        {
            var y = bulletTop + index * (CompactMode ? 24 : 28);
            using var dotBrush = new SolidBrush(index == 0 ? guidance.BadgeColor : Color.FromArgb(88, 104, 120));
            e.Graphics.FillEllipse(dotBrush, 20, y + 5, 8, 8);
            e.Graphics.DrawString(guidance.Tips[index], UiTheme.BodySmallFont, textBrush, new RectangleF(36, y, Width - 54, 22));
        }

        if (IsScanning)
        {
            e.Graphics.DrawString("Guidance updates as selection changes during scan.", UiTheme.MicroFont, subtleBrush, new RectangleF(18, Height - 28, Width - 36, 18));
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