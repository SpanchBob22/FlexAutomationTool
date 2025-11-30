using System;
using System.IO;
using System.Threading.Tasks;
using FlexAutomator.Blocks.Base;

namespace FlexAutomator.Blocks.Triggers;

public class FileChangeTriggerBlock : TriggerBlock
{
    public override string Type => "FileChangeTrigger";

    private FileSystemWatcher? _mainWatcher;
    private FileSystemWatcher? _parentWatcher;

    private volatile bool _isTriggered;

    private string _targetPath = string.Empty;

    private string _watchDirectory = string.Empty;
    private string _watchFilter = "*";

    private bool _isWatchingFile = false;

    private string _filterEvent = "All";

    private DateTime _lastEventTime = DateTime.MinValue;
    private string _lastEventPath = string.Empty;
    private readonly object _eventLock = new();
    private const int DebounceMilliseconds = 300;

    public string? DetectedFilePath { get; private set; }

    public void ResetDetectedPath()
    {
        lock (_eventLock)
        {
            DetectedFilePath = null;
        }
    }

    public void StartWatching()
    {
        StopWatching();

        if (!Parameters.TryGetValue("Path", out var rawPath) || string.IsNullOrEmpty(rawPath))
            return;

        string cleanPath = rawPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        bool isDirectory = Directory.Exists(cleanPath);
        bool isFile = File.Exists(cleanPath);

        if (!isDirectory && !isFile)
            return;

        _targetPath = cleanPath;
        _isWatchingFile = isFile;

        if (isDirectory)
        {
            _watchDirectory = _targetPath;
            _watchFilter = "*";
        }
        else
        {
            _watchDirectory = Path.GetDirectoryName(_targetPath) ?? _targetPath;
            _watchFilter = Path.GetFileName(_targetPath);
        }

        _filterEvent = Parameters.TryGetValue("Event", out var targetEvent) && !string.IsNullOrEmpty(targetEvent)
            ? targetEvent
            : "All";

        var recursive = Parameters.TryGetValue("Recursive", out var recursiveStr)
                        && bool.TryParse(recursiveStr, out var rec)
                        && rec;

        if (_isWatchingFile) recursive = false;

        try
        {
            _mainWatcher = new FileSystemWatcher(_watchDirectory, _watchFilter)
            {
                IncludeSubdirectories = recursive,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime
            };

            bool watchAll = string.Equals(_filterEvent, "All", StringComparison.OrdinalIgnoreCase);

            if (watchAll || string.Equals(_filterEvent, "Changed", StringComparison.OrdinalIgnoreCase))
                _mainWatcher.Changed += OnFileChanged;

            if (watchAll || string.Equals(_filterEvent, "Created", StringComparison.OrdinalIgnoreCase))
                _mainWatcher.Created += OnFileChanged;

            if (watchAll || string.Equals(_filterEvent, "Deleted", StringComparison.OrdinalIgnoreCase))
                _mainWatcher.Deleted += OnFileChanged;

            if (watchAll || string.Equals(_filterEvent, "Renamed", StringComparison.OrdinalIgnoreCase))
                _mainWatcher.Renamed += OnFileRenamed;

            _mainWatcher.EnableRaisingEvents = true;


            var parentDirOfWatch = Directory.GetParent(_watchDirectory)?.FullName;
            if (parentDirOfWatch != null)
            {
                var folderName = Path.GetFileName(_watchDirectory);
                _parentWatcher = new FileSystemWatcher(parentDirOfWatch)
                {
                    Filter = folderName,
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
                };
                _parentWatcher.Deleted += OnParentEvent;
                _parentWatcher.Renamed += OnParentEvent;
                _parentWatcher.EnableRaisingEvents = true;
            }
        }
        catch
        {
            StopWatching();
        }
    }

    public void StopWatching()
    {
        if (_mainWatcher != null)
        {
            try
            {
                _mainWatcher.EnableRaisingEvents = false;
                _mainWatcher.Changed -= OnFileChanged;
                _mainWatcher.Created -= OnFileChanged;
                _mainWatcher.Deleted -= OnFileChanged;
                _mainWatcher.Renamed -= OnFileRenamed;
                _mainWatcher.Dispose();
            }
            catch {}
            finally { _mainWatcher = null; }
        }
        if (_parentWatcher != null)
        {
            try
            {
                _parentWatcher.EnableRaisingEvents = false;
                _parentWatcher.Deleted -= OnParentEvent;
                _parentWatcher.Renamed -= OnParentEvent;
                _parentWatcher.Dispose();
            }
            catch {}
            finally { _parentWatcher = null; }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e == null) return;


        if (!string.Equals(_filterEvent, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_filterEvent, e.ChangeType.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ProcessEvent(e.FullPath);
    }


    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (e == null) return;

        bool isRelevant = string.Equals(_filterEvent, "All", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(_filterEvent, "Renamed", StringComparison.OrdinalIgnoreCase);

        if (!isRelevant) return;



        ProcessEvent(e.FullPath);
    }

    private void OnParentEvent(object sender, FileSystemEventArgs e)
    {
        bool shouldTrigger = false;
        string? newDetectedPath = e.FullPath;

        lock (_eventLock)
        {
            if (e is RenamedEventArgs re)
            {
                if (string.Equals(re.OldFullPath, _watchDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    StopWatching();

                    string newWatchDir = re.FullPath;
                    if (_isWatchingFile)
                    {
                        string fileName = Path.GetFileName(_targetPath);
                        _targetPath = Path.Combine(newWatchDir, fileName);
                    }
                    else
                    {
                        _targetPath = newWatchDir;
                    }

                    bool matchesFilter = string.Equals(_filterEvent, "All", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(_filterEvent, "Renamed", StringComparison.OrdinalIgnoreCase);

                    if (matchesFilter)
                    {
                        shouldTrigger = true;
                        newDetectedPath = _targetPath;
                    }

                    Task.Run(() => StartWatching());
                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                if (string.Equals(e.FullPath, _watchDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    bool matchesFilter = string.Equals(_filterEvent, "All", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(_filterEvent, "Deleted", StringComparison.OrdinalIgnoreCase);

                    if (matchesFilter)
                    {
                        shouldTrigger = true;
                        newDetectedPath = e.FullPath;
                    }

                    Task.Run(() => StopWatching());
                }
            }
        }

        if (shouldTrigger)
        {
            ProcessEvent(newDetectedPath!);
        }
    }

    private void ProcessEvent(string fullPath)
    {
        lock (_eventLock)
        {
            var now = DateTime.Now;
            var timeSinceLastEvent = (now - _lastEventTime).TotalMilliseconds;

            if (timeSinceLastEvent < DebounceMilliseconds &&
                string.Equals(_lastEventPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastEventTime = now;
            _lastEventPath = fullPath;
            DetectedFilePath = fullPath;
            _isTriggered = true;
        }
    }

    public override async Task<bool> ShouldTriggerAsync(DateTime? lastExecuted = null)
    {
        await Task.CompletedTask;
        if (_isTriggered)
        {
            _isTriggered = false;
            return true;
        }
        return false;
    }
}