namespace AutoSync
{
    using System.Collections.Generic;
    using System.IO;

    using KeePass.Forms;
    using KeePass.Plugins;

    using KeePassLib;
    using KeePassLib.Interfaces;
    using KeePassLib.Serialization;

    public class AutoSyncExt : Plugin
    {
        private readonly IDictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>();

        private IPluginHost host;

        public override bool Initialize(IPluginHost host)
        {
            this.host = host;

            this.host.MainWindow.FileOpened += MainWindowOnFileOpened;
            this.host.MainWindow.FileClosingPre += MainWindowOnFileClosingPre;

            return true;
        }

        private void MainWindowOnFileOpened(object sender, FileOpenedEventArgs fileOpenedEventArgs)
        {
            if (!fileOpenedEventArgs.Database.IOConnectionInfo.IsLocalFile())
            {
                return;
            }

            this.AddMonitor(fileOpenedEventArgs.Database.IOConnectionInfo.Path);
        }

        private void MainWindowOnFileClosingPre(object sender, FileClosingEventArgs fileClosingEventArgs)
        {
            if (!fileClosingEventArgs.Database.IOConnectionInfo.IsLocalFile())
            {
                return;
            }

            this.RemoveMonitor(fileClosingEventArgs.Database.IOConnectionInfo.Path);
        }

        private void AddMonitor(string databaseFilename)
        {
            if (watchers.ContainsKey(databaseFilename))
            {
                return;
            }

            var path = Path.GetDirectoryName(databaseFilename);
            var filename = Path.GetFileName(databaseFilename);

            if (filename == null || path == null)
            {
                return;
            }

            var watcher = new FileSystemWatcher(path, filename);
            watcher.Changed += this.MonitorChanged;
            watcher.EnableRaisingEvents = true;

            this.watchers.Add(databaseFilename, watcher);
        }

        private void RemoveMonitor(string databaseFilename)
        {
            if (!watchers.ContainsKey(databaseFilename))
            {
                return;
            }

            this.watchers[databaseFilename].Dispose();
            this.watchers.Remove(databaseFilename);
        }

        private void MonitorChanged(object sender, FileSystemEventArgs e)
        {
            var watcher = sender as FileSystemWatcher;

            if (watcher == null)
            {
                return;
            }

            watcher.EnableRaisingEvents = false;

            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }

            this.SyncDatabase(e.FullPath);

            watcher.EnableRaisingEvents = true;
        }

        private void SyncDatabase(string databaseFilename)
        {
            var db = new PwDatabase();

            db.Open(IOConnectionInfo.FromPath(databaseFilename), this.host.Database.MasterKey, new NullStatusLogger());

            this.host.Database.MergeIn(db, PwMergeMethod.Synchronize);
            this.host.MainWindow.RefreshEntriesList();

            db.Close();
        }
    }
}