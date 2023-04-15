namespace Resona.Services.Libraries
{
    public interface ILibraryFileWatcher
    {
        Action? ChangesDetected { get; set; }

        void Initialize(IEnumerable<string> directories);
    }

    public class LibraryFileWatcher : ILibraryFileWatcher
    {
        private readonly Timer notificationTimer;
        private readonly List<FileSystemWatcher> watchers = new();

        public LibraryFileWatcher()
        {
            // Create a timer to allow for a debouncing of notifications.
            this.notificationTimer = new Timer(this.FireNotification, null, Timeout.Infinite, Timeout.Infinite);
        }

        public Action? ChangesDetected { get; set; }

        public void Initialize(IEnumerable<string> directories)
        {
            if (this.watchers.Count > 0)
            {
                throw new InvalidOperationException("Already initialized");
            }

            foreach (var directory in directories)
            {
                this.StartWatching(directory);
            }
        }

        private void StartWatching(string directory)
        {
            if (Directory.Exists(directory))
            {
                var fileWatcher = new FileSystemWatcher(directory);
                fileWatcher.Changed += this.OnFilesChanged;
                fileWatcher.Deleted += this.OnFilesChanged;
                fileWatcher.Created += this.OnFilesChanged;
                fileWatcher.Renamed += this.OnFilesChanged;
                fileWatcher.IncludeSubdirectories = true;
                fileWatcher.EnableRaisingEvents = true;
            }
        }

        private void FireNotification(object? state)
        {
            this.ChangesDetected?.Invoke();
        }

        private void OnFilesChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce a change detection by resetting the notification timer for 5 seconds in the future
            this.notificationTimer.Change(5000, Timeout.Infinite);
        }

        public void Initialize(IReadOnlyList<string> directories)
        {
            throw new NotImplementedException();
        }
    }
}
