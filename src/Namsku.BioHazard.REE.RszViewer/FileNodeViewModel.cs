using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace RszViewer
{
    public class FileNodeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }

        private ObservableCollection<FileNodeViewModel> _children = new ObservableCollection<FileNodeViewModel>();
        public ObservableCollection<FileNodeViewModel> Children
        {
            get => _children;
            set
            {
                if (_children != value)
                {
                    _children = value;
                    OnPropertyChanged(nameof(Children));
                }
            }
        }
        
        // Simple text icons for now, can be replaced with images later
        public string Icon => IsDirectory ? "📁" : "📄";

        private static readonly FileNodeViewModel DummyNode = new FileNodeViewModel("Loading...", false);

        public FileNodeViewModel(string path, bool isDirectory)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(Name) && isDirectory) Name = path; // Root case
            IsDirectory = isDirectory;
            if (IsDirectory) _children.Add(DummyNode); // Placeholder for lazy loading
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                    if (_isExpanded) LoadChildrenLazy();
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        public static ObservableCollection<FileNodeViewModel> LoadDirectory(string path)
        {
            var rootCollection = new ObservableCollection<FileNodeViewModel>();
            if (Directory.Exists(path))
            {
                var root = new FileNodeViewModel(path, true);
                root.Children.Clear(); 
                // For root, trigger immediate load
                LoadChildren(root);
                return root.Children; 
            }
            return rootCollection;
        }

        private void LoadChildrenLazy()
        {
            if (Children.Count == 1 && Children[0] == DummyNode)
            {
                LoadChildren(this);
            }
        }

        private static void LoadChildren(FileNodeViewModel node)
        {
            if (!node.IsDirectory) return;

            try
            {
                var directoryInfo = new DirectoryInfo(node.FullPath);
                var newChildren = new ObservableCollection<FileNodeViewModel>();
                
                // Load Directories
                foreach (var dir in directoryInfo.GetDirectories().OrderBy(d => d.Name))
                {
                    if (DirectoryHasTargetFiles(dir)) 
                    {
                         var dirNode = new FileNodeViewModel(dir.FullName, true);
                         // Check if truly empty
                         if (!DirectoryHasEntries(dir)) dirNode.Children.Clear();
                         newChildren.Add(dirNode);
                    }
                }

                // Load Files
                foreach (var file in directoryInfo.GetFiles().OrderBy(f => f.Name))
                {
                    if (IsTargetFile(file.Name))
                    {
                        var fileNode = new FileNodeViewModel(file.FullName, false);
                        fileNode.Children.Clear(); // Files have no children
                        newChildren.Add(fileNode);
                    }
                }

                // Batch update
                node.Children = newChildren;
            }
            catch (UnauthorizedAccessException) { /* Ignore */ }
            catch (Exception) { /* Ignore */ }
        }

        private static bool DirectoryHasEntries(DirectoryInfo dir)
        {
            try { return dir.EnumerateFileSystemInfos().Any(); } catch { return false; }
        }

        private static bool DirectoryHasTargetFiles(DirectoryInfo dir)
        {
             // Simplest: Always show directories. If they are empty, so be it.
             return true; 
        }

        private static bool IsTargetFile(string filename)
        {
            return filename.Contains(".user.") || filename.Contains(".scn.") || filename.Contains(".pfb.") || filename.Contains(".tex");
        }
    }
}
