namespace SpaceHog;

public sealed class DirEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long Size { get; set; }
    public int FileCount { get; set; }
    public int DirCount { get; set; }
    public List<DirEntry> Children { get; set; } = new();

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1L << 40) return $"{bytes / (double)(1L << 40):N2} TB";
        if (bytes >= 1L << 30) return $"{bytes / (double)(1L << 30):N2} GB";
        if (bytes >= 1L << 20) return $"{bytes / (double)(1L << 20):N1} MB";
        if (bytes >= 1L << 10) return $"{bytes / (double)(1L << 10):N0} KB";
        return $"{bytes} B";
    }
}
