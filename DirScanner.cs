namespace SpaceHog;

public sealed class DirScanner
{
    private readonly int _maxDepth;
    private volatile bool _cancelled;
    private long _totalScanned;
    private static readonly EnumerationOptions EnumerateOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    public long TotalScanned => _totalScanned;

    public event Action<string>? ProgressChanged;
    public event Action<DirEntry>? RootChildCompleted;

    public DirScanner(int maxDepth = 6)
    {
        _maxDepth = maxDepth;
    }

    public void Cancel() => _cancelled = true;

    public DirEntry Scan(string rootPath, CancellationToken cancellationToken = default)
    {
        _cancelled = false;
        _totalScanned = 0;
        var result = ScanDir(rootPath, 0, cancellationToken);
        result.Name = rootPath;
        return result;
    }

    private void ThrowIfCancellationRequested(CancellationToken cancellationToken)
    {
        if (_cancelled || cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);
    }

    private DirEntry ScanDir(string path, int depth, CancellationToken cancellationToken)
    {
        ThrowIfCancellationRequested(cancellationToken);

        var entry = new DirEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path
        };

        // Count files
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(path, "*", EnumerateOptions))
            {
                ThrowIfCancellationRequested(cancellationToken);
                try
                {
                    var fi = new FileInfo(filePath);
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
                foreach (var dirPath in Directory.EnumerateDirectories(path, "*", EnumerateOptions))
                {
                    ThrowIfCancellationRequested(cancellationToken);
                    try
                    {
                        var attr = File.GetAttributes(dirPath);
                        if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch { continue; }

                    if (_totalScanned % 100 == 0)
                        ProgressChanged?.Invoke(dirPath);

                    var child = ScanDir(dirPath, depth + 1, cancellationToken);
                    entry.Size += child.Size;
                    entry.FileCount += child.FileCount;
                    entry.DirCount += child.DirCount + 1;
                    entry.Children.Add(child);

                    if (depth == 0)
                        RootChildCompleted?.Invoke(child);
                }
            }
            catch { }
        }

        entry.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
        return entry;
    }
}
