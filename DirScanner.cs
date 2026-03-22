namespace SpaceHog;

public sealed class DirScanner
{
    private readonly int _maxDepth;
    private volatile bool _cancelled;
    private long _totalScanned;

    public long TotalScanned => _totalScanned;

    public event Action<string>? ProgressChanged;

    public DirScanner(int maxDepth = 6)
    {
        _maxDepth = maxDepth;
    }

    public void Cancel() => _cancelled = true;

    public DirEntry Scan(string rootPath)
    {
        _cancelled = false;
        _totalScanned = 0;
        var result = ScanDir(rootPath, 0);
        result.Name = rootPath;
        return result;
    }

    private DirEntry ScanDir(string path, int depth)
    {
        var entry = new DirEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path
        };

        if (_cancelled) return entry;

        // Count files
        try
        {
            var files = Directory.GetFiles(path);
            foreach (var f in files)
            {
                if (_cancelled) break;
                try
                {
                    var fi = new FileInfo(f);
                    entry.Size += fi.Length;
                    entry.FileCount++;
                    Interlocked.Increment(ref _totalScanned);
                }
                catch { }
            }
        }
        catch { }

        // Recurse into subdirectories
        if (depth < _maxDepth)
        {
            try
            {
                var dirs = Directory.GetDirectories(path);
                foreach (var d in dirs)
                {
                    if (_cancelled) break;
                    try
                    {
                        var attr = File.GetAttributes(d);
                        if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch { continue; }

                    if (_totalScanned % 500 == 0)
                        ProgressChanged?.Invoke(d);

                    var child = ScanDir(d, depth + 1);
                    entry.Size += child.Size;
                    entry.FileCount += child.FileCount;
                    entry.DirCount += child.DirCount + 1;
                    entry.Children.Add(child);
                }
            }
            catch { }
        }

        entry.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
        return entry;
    }
}
