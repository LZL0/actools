using System;
using System.IO;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    /// <summary>
    /// Takes file information and, if copy needed, returns destination path.
    /// </summary>
    public interface ICopyCallback {
        [CanBeNull]
        string File ([NotNull] IFileInfo info);

        [CanBeNull]
        string Directory ([NotNull] IDirectoryInfo info);
    }

    public class CopyCallback : ICopyCallback {
        private Func<IFileInfo, string> _file;
        private Func<IDirectoryInfo, string> _directory;

        public CopyCallback(Func<IFileInfo, string> file, Func<IDirectoryInfo, string> directory = null) {
            _file = file;
            _directory = directory;
        }

        private static bool IsToIgnore(string filename) {
            return string.Equals(Path.GetFileName(filename), "Thumbs.db", StringComparison.OrdinalIgnoreCase);
        }

        public string File(IFileInfo info) {
            return IsToIgnore(info.Key) ? null : _file?.Invoke(info);
        }

        public string Directory(IDirectoryInfo info) {
            return _directory?.Invoke(info);
        }
    }
}