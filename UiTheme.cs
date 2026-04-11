namespace SpaceHog;

internal static class UiTheme
{
    public static readonly Color AppBackground = Color.FromArgb(10, 14, 20);
    public static readonly Color SurfaceHeaderDeep = Color.FromArgb(8, 15, 28);
    public static readonly Color SurfaceHeader = Color.FromArgb(12, 24, 40);
    public static readonly Color SurfaceCard = Color.FromArgb(14, 22, 34);
    public static readonly Color SurfacePanel = Color.FromArgb(11, 18, 30);
    public static readonly Color SurfaceInset = Color.FromArgb(16, 24, 36);
    public static readonly Color SurfaceRaised = Color.FromArgb(31, 43, 59);
    public static readonly Color Border = Color.FromArgb(40, 79, 108);
    public static readonly Color BorderStrong = Color.FromArgb(55, 105, 140);
    public static readonly Color Accent = Color.FromArgb(53, 159, 255);
    public static readonly Color AccentSoft = Color.FromArgb(166, 213, 255);
    public static readonly Color Warning = Color.FromArgb(255, 197, 134);
    public static readonly Color HeroLeft = Color.FromArgb(23, 61, 99);
    public static readonly Color HeroRight = Color.FromArgb(45, 114, 170);
    public static readonly Color TextPrimary = Color.FromArgb(242, 248, 255);
    public static readonly Color TextSoft = Color.FromArgb(194, 214, 232);
    public static readonly Color TextMuted = Color.FromArgb(156, 188, 212);
    public static readonly Color TextSubtle = Color.FromArgb(128, 155, 178);

    public static readonly Font BrandFont = new("Segoe UI Variable Display", 22f, FontStyle.Bold);
    public static readonly Font HeroTitleFont = new("Segoe UI Variable Display", 20f, FontStyle.Bold);
    public static readonly Font SectionTitleFont = new("Segoe UI Semibold", 11f, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font BodySmallFont = new("Segoe UI", 8.8f, FontStyle.Regular);
    public static readonly Font CaptionFont = new("Segoe UI Semibold", 8.5f, FontStyle.Bold);
    public static readonly Font ButtonFont = new("Segoe UI Semibold", 9f, FontStyle.Bold);
    public static readonly Font KpiFont = new("Segoe UI Variable Text", 11.5f, FontStyle.Bold);
    public static readonly Font IconFont = new("Segoe UI Symbol", 12f, FontStyle.Regular);
    public static readonly Font EmptyStateIconFont = new("Segoe UI Emoji", 26f, FontStyle.Regular);
    public static readonly Font EmptyStateTitleFont = new("Segoe UI Semibold", 12f, FontStyle.Bold);
    public static readonly Font MicroFont = new("Segoe UI", 8f, FontStyle.Regular);
}