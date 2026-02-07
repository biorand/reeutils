using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IntelOrca.Biohazard.REE.Rsz;
using Microsoft.Win32;

namespace RszViewer
{
    public partial class LinkTracerControl : UserControl
    {
        private ResourceLinker? _linker;
        private RszGameObject? _sourceObject;
        private ObservableCollection<LinkResult> _results = new ObservableCollection<LinkResult>();

        public LinkTracerControl()
        {
            InitializeComponent();
            TreeResults.ItemsSource = _results;
            Loaded += LinkTracerControl_Loaded;
        }

        private void LinkTracerControl_Loaded(object sender, RoutedEventArgs e)
        {
            var config = MainWindow.Instance?.Config;
            if (config?.LastLinkerFolder != null && Directory.Exists(config.LastLinkerFolder))
            {
                TxtFolderPath.Text = config.LastLinkerFolder;
            }
        }

        public void Initialize(RszTypeRepository repo, RszGameObject sourceObject)
        {
            _sourceObject = sourceObject;
            _linker = new ResourceLinker(repo);
            
            _linker.OnFileProcessed += (path) => Dispatcher.Invoke(() => TxtStatus.Text = $"Scanning: {Path.GetFileName(path)}");
            _linker.OnStatusUpdate += (status) => Dispatcher.Invoke(() => TxtStatus.Text = status);

            TxtSourceName.Text = sourceObject.Name;
            TxtSourceGuid.Text = sourceObject.Guid.ToString();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                TxtFolderPath.Text = dialog.FolderName;
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.Config.LastLinkerFolder = dialog.FolderName;
                    MainWindow.Instance.Config.Save();
                }
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_linker == null || _sourceObject == null)
            {
                MessageBox.Show("Please select a source object from RszCompare first.");
                return;
            }

            string folder = TxtFolderPath.Text;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("Please select a valid folder.");
                return;
            }

            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.Config.LastLinkerFolder = folder;
                MainWindow.Instance.Config.Save();
            }

            BtnStart.IsEnabled = false;
            ProgressSearch.Visibility = Visibility.Visible;
            _results.Clear();
            TxtNoResults.Visibility = Visibility.Collapsed;
            TxtStatus.Text = "Starting search...";

            try
            {
                var results = await Task.Run(() => _linker.TraceDependencies(folder, _sourceObject));
                foreach (var res in results)
                {
                    _results.Add(res);
                }
                
                if (_results.Count == 0)
                {
                    TxtNoResults.Visibility = Visibility.Visible;
                    TxtNoResults.Text = "No dependencies found.";
                }
                else
                {
                    TxtStatus.Text = $"Found {_results.Count} root matches.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Search error: " + ex.Message);
                TxtStatus.Text = "Search failed.";
            }
            finally
            {
                BtnStart.IsEnabled = true;
                ProgressSearch.Visibility = Visibility.Collapsed;
                if (TxtStatus.Text.StartsWith("Scanning")) TxtStatus.Text = "Search Complete.";
            }
        }

        // Double-click on link result opens the file
        private void LinkResult_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is LinkResult result)
            {
                OpenFileInViewer(result.FilePath);
                e.Handled = true;
            }
        }

        private void LinkResult_OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LinkResult result)
            {
                OpenFileInViewer(result.FilePath);
            }
        }

        private void LinkResult_OpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LinkResult result)
            {
                if (File.Exists(result.FilePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result.FilePath}\"");
                }
            }
        }

        private void LinkResult_CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LinkResult result)
            {
                Clipboard.SetText(result.FilePath);
                TxtStatus.Text = "Path copied to clipboard!";
            }
        }

        private void OpenFileInViewer(string filePath)
        {
            if (File.Exists(filePath) && MainWindow.Instance != null)
            {
                MainWindow.Instance.LoadFileForView(filePath);
                TxtStatus.Text = $"Opened: {Path.GetFileName(filePath)}";
            }
            else
            {
                MessageBox.Show($"File not found:\n{filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
