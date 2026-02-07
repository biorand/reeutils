using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REE;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Variables;
using IntelOrca.Biohazard.REE.Package;
using Microsoft.Win32;
using System.Threading;
using System.Collections.Immutable;

namespace RszViewer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static MainWindow? Instance { get; private set; }
        public AppConfig Config => _config;
        
        private string _searchQuery = "";
        public string SearchQuery { get => _searchQuery; set { if (_searchQuery != value) { _searchQuery = value; OnPropertyChanged(); } } }


        private RszTypeRepository _repo = new RszTypeRepository(); // Safe default
        private SearchManager _searchManager = new SearchManager();
        private AppConfig _config = AppConfig.Load();
        private ObservableCollection<RszTabViewModel> _rszTabs = new();
        private IList<RszNodeViewModel> _cachedCompareLeft = new List<RszNodeViewModel>();
        private IList<RszNodeViewModel> _cachedCompareRight = new List<RszNodeViewModel>();
        
        public ObservableCollection<RszTabViewModel> RszTabs => _rszTabs;
        
        private int _matchIndex = -1;
        private List<RszNodeViewModel> _currentMatches = new List<RszNodeViewModel>();
        
        // View search
        private string _viewSearchQuery = "";
        public string ViewSearchQuery { get => _viewSearchQuery; set { if (_viewSearchQuery != value) { _viewSearchQuery = value; OnPropertyChanged(); } } }

        private int _viewMatchIndex = -1;
        private CancellationTokenSource? _viewSearchCts;
        private ObservableCollection<RszNodeViewModel> _viewMatches = new ObservableCollection<RszNodeViewModel>();
        public ObservableCollection<RszNodeViewModel> ViewMatches => _viewMatches;

        // RszSheet
        public RszSheetViewModel SheetVM { get; } = new RszSheetViewModel();

        public ObservableCollection<string> RecentFolders { get; } = new();
        public bool IsExplorerVisible
        {
            get => ColExplorer.Width.Value > 0;
            set
            {
                ColExplorer.Width = value ? new GridLength(250) : new GridLength(0);
                MenuShowExplorer.IsChecked = value;
                _config.IsExplorerVisible = value;
                _config.Save();
            }
        }

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            
            // Ensure resources are loaded
            if (!Resources.Contains("LinkIconConverter")) Resources.Add("LinkIconConverter", new LinkIconConverter());
            if (!Resources.Contains("PathToNameConverter")) Resources.Add("PathToNameConverter", new PathToNameConverter());
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_config.RszRepoPath != null && File.Exists(_config.RszRepoPath))
            {
                try { _repo = RszRepositorySerializer.Default.FromJsonFile(_config.RszRepoPath); } catch { }
            }
            
            // Load Recent Folders
            RecentFolders.Clear();
            foreach (var f in _config.RecentFolders) RecentFolders.Add(f);
            UpdateRecentFoldersMenu();

            if (_config.LastViewFolder != null && Directory.Exists(_config.LastViewFolder))
            {
                OpenFolder(_config.LastViewFolder);
            }

            // Restore opened tabs from config
            foreach (var tabPath in _config.OpenedTabPaths)
            {
                if (File.Exists(tabPath))
                {
                    try { LoadFileForView(tabPath); } catch { }
                }
            }
            
            // Restore selected tab index
            if (_config.SelectedTabIndex >= 0 && _config.SelectedTabIndex < _rszTabs.Count)
            {
                FileTabControl.SelectedIndex = _config.SelectedTabIndex;
            }

            IsExplorerVisible = _config.IsExplorerVisible;
            
            if (_config.LeftFilePath != null && File.Exists(_config.LeftFilePath)) LoadFile(true, _config.LeftFilePath);
            if (_config.RightFilePath != null && File.Exists(_config.RightFilePath)) LoadFile(false, _config.RightFilePath);
            
            UpdateRszSheet();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save all currently opened tabs
            _config.OpenedTabPaths = _rszTabs.Select(t => t.FullPath).Where(p => !string.IsNullOrEmpty(p)).ToList()!;
            _config.SelectedTabIndex = FileTabControl.SelectedIndex;
            _config.Save();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    string lower = filePath.ToLower();
                    if (lower.Contains(".scn.") || lower.Contains(".pfb.") || lower.Contains(".user.") || lower.Contains(".aimap") || lower.Contains(".tex") || lower.Contains(".msg") || lower.Contains(".uvar") || lower.Contains(".pak"))
                    {
                        LoadFileForView(filePath);
                    }
                }
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) { }



        private void TreeA_Loaded(object sender, RoutedEventArgs e)
        {
            if (TreeA.ItemsSource == null && _cachedCompareLeft != null) TreeA.ItemsSource = _cachedCompareLeft;
        }

        private void TreeB_Loaded(object sender, RoutedEventArgs e)
        {
            if (TreeB.ItemsSource == null && _cachedCompareRight != null) TreeB.ItemsSource = _cachedCompareRight;
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                OpenFolder(dialog.FolderName);
            }
        }

        private void OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            _config.LastViewFolder = path;
            UpdateRecentFolders(path);
            _config.Save();

            UpdateBreadcrumbs(path);
            RefreshFileList(path);
            
            if (!IsExplorerVisible) IsExplorerVisible = true;
        }

        private void UpdateRecentFolders(string path)
        {
            if (_config.RecentFolders.Contains(path)) _config.RecentFolders.Remove(path);
            _config.RecentFolders.Insert(0, path);
            if (_config.RecentFolders.Count > 10) _config.RecentFolders.RemoveAt(10);
            
            RecentFolders.Clear();
            foreach (var f in _config.RecentFolders) RecentFolders.Add(f);
            UpdateRecentFoldersMenu();
        }

        private void UpdateRecentFoldersMenu()
        {
            if (MenuRecentFolders == null) return;
            MenuRecentFolders.Items.Clear();
            if (RecentFolders.Count == 0)
            {
                MenuRecentFolders.Items.Add(new MenuItem { Header = "No recent folders", IsEnabled = false });
                return;
            }

            foreach (var folder in RecentFolders)
            {
                var item = new MenuItem { Header = folder };
                item.Click += (s, e) => OpenFolder(folder);
                MenuRecentFolders.Items.Add(item);
            }
        }

        private void UpdateBreadcrumbs(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                TxtBreadcrumbs.Text = "No folder opened";
                return;
            }
            TxtBreadcrumbs.Text = path.Replace(Path.DirectorySeparatorChar, ' ').Replace(' ', ' ').Replace(" ", " › ");
            // Actually it's better to just use string join or simple replace
            TxtBreadcrumbs.Text = path.Replace(Path.DirectorySeparatorChar.ToString(), " › ").Replace(Path.AltDirectorySeparatorChar.ToString(), " › ");
        }

        private void BtnToggleExplorer_Click(object sender, RoutedEventArgs e)
        {
            IsExplorerVisible = !IsExplorerVisible;
        }

        private void BtnViewBrowse_Click(object sender, RoutedEventArgs e)
        {
            BtnOpenFolder_Click(sender, e);
        }

        private void RefreshFileList_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_config.LastViewFolder))
            {
                RefreshFileList(_config.LastViewFolder);
            }
        }

        private void RefreshFileList(string folder)
        {
            try
            {
                var rootNodes = FileNodeViewModel.LoadDirectory(folder);
                TreeFiles.ItemsSource = rootNodes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error listing files: " + ex.Message);
            }
        }

        private void TreeFiles_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileNodeViewModel node && !node.IsDirectory) LoadFileForView(node.FullPath);
        }

        private void TreeFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
             if (TreeFiles.SelectedItem is FileNodeViewModel node && !node.IsDirectory)
             {
                 LoadFileForView(node.FullPath);
             }
        }

        public void LoadFileForView(string filePath)
        {
            try
            {
                string lower = filePath.ToLower();
                // Special handling for textures (no RSZ needed)
                if (lower.Contains(".tex"))
                {
                   // Check if already open
                    var existingTex = _rszTabs.FirstOrDefault(t => t.FullPath == filePath);
                    if (existingTex != null)
                    {
                        FileTabControl.SelectedItem = existingTex;
                        return;
                    }

                    var texTab = new TextureTabViewModel { Header = Path.GetFileName(filePath), FullPath = filePath };
                    _rszTabs.Add(texTab);
                    FileTabControl.SelectedItem = texTab;
                    return;
                }

                // Check if already open
                var existing = _rszTabs.FirstOrDefault(t => t.FullPath == filePath);
                if (existing != null)
                {
                    FileTabControl.SelectedItem = existing;
                    return;
                }

                byte[] data = File.ReadAllBytes(filePath);
                IList<RszNodeViewModel> viewModels;

                // File types that don't require RSZ definitions
                if (lower.Contains(".msg"))
                {
                    viewModels = LoadMsgFile(data, filePath);
                }
                else if (lower.Contains(".uvar"))
                {
                    viewModels = LoadUvarFile(data, filePath);
                }
                else if (lower.EndsWith(".pak") || lower.Contains(".pak."))
                {
                    viewModels = LoadPakFile(filePath);
                }
                // File types that require RSZ definitions
                else if (!EnsureRszLoaded())
                {
                    return;
                }
                else if (lower.Contains(".scn."))
                {
                    int version = GetVersion(lower, ".scn.");
                    var scnFile = new ScnFile(version, data);
                    var scene = scnFile.ReadScene(_repo);
                    viewModels = CategorizeSceneNodes(scene);
                }
                else if (lower.Contains(".pfb."))
                {
                    int version = GetVersion(lower, ".pfb.");
                    var pfbFile = new PfbFile(version, data);
                    var scene = pfbFile.ReadScene(_repo);
                    viewModels = CategorizeSceneNodes(scene);
                }
                else if (lower.Contains(".aimap"))
                {
                    var aimap = new AimapFile(data);

                    // Create structured view with metadata + components + RSZ
                    viewModels = new List<RszNodeViewModel>();

                    // AIMAP metadata root node
                    var aimapInfoNode = new RszNodeViewModel($"📍 {aimap.MapName}", aimap.ToString(), "AimapFile");
                    aimapInfoNode.Children.Add(new RszNodeViewModel("GUID", aimap.MapGuid.ToString(), "System.Guid"));
                    
                    // Show each component with paginated point display
                    foreach (var comp in aimap.Components)
                    {
                        var compNode = new RszNodeViewModel(
                            $"📦 {comp.ShortTypeName} [{comp.PointCount}]", 
                            $"{comp.TypeName} ({comp.DataSize:N0} bytes)", 
                            "Component");
                        
                        // Create paginated chunks of 100
                        const int CHUNK_SIZE = 100;
                        int chunkCount = (comp.PointCount + CHUNK_SIZE - 1) / CHUNK_SIZE;
                        
                        for (int chunk = 0; chunk < chunkCount; chunk++)
                        {
                            int start = chunk * CHUNK_SIZE;
                            int end = Math.Min(start + CHUNK_SIZE - 1, comp.PointCount - 1);
                            
                            var chunkNode = new RszNodeViewModel($"[{start}-{end}]", $"{end - start + 1} points", "Array");
                            
                            // Add individual points within chunk
                            for (int i = start; i <= end; i++)
                            {
                                var (x, y, z) = comp.GetPointPosition(i);
                                chunkNode.Children.Add(new RszNodeViewModel(
                                    $"[{i}]", 
                                    $"({x:F2}, {y:F2}, {z:F2})", 
                                    "Vec3"));
                            }
                            
                            compNode.Children.Add(chunkNode);
                        }
                        
                        aimapInfoNode.Children.Add(compNode);
                    }
                    viewModels.Add(aimapInfoNode);

                    // RSZ instances - the actual navigable objects
                    var instanceList = aimap.Rsz.ReadInstanceList(_repo);
                    var rszNode = new RszNodeViewModel($"RSZ Objects [{instanceList.Length}]", $"{instanceList.Length} instances", "RszFile");
                    foreach (var inst in instanceList)
                    {
                        if (inst.Value != null)
                        {
                            rszNode.Children.Add(new RszNodeViewModel(inst.Value, $"Instance {inst.Id.Index}"));
                        }
                    }
                    viewModels.Add(rszNode);
                }
                else
                {
                    var userFile = new UserFile(data);
                    var builder = userFile.ToBuilder(_repo);
                    viewModels = builder.Objects.Select((n, i) => new RszNodeViewModel(n, $"Object {i}")).ToList();
                }

                var tab = new RszTabViewModel { Header = Path.GetFileName(filePath), FullPath = filePath, Nodes = new ObservableCollection<RszNodeViewModel>(viewModels) };
                _rszTabs.Add(tab);
                FileTabControl.SelectedItem = tab;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file:\n{ex.Message}");
            }
        }

        private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RszTabViewModel tab)
            {
                _rszTabs.Remove(tab);
            }
            else if (FileTabControl.SelectedItem is RszTabViewModel selectedTab)
            {
                _rszTabs.Remove(selectedTab);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
             var win = new SettingsWindow(_config);
             win.Owner = this;
             if (win.ShowDialog() == true)
             {
                 if (!string.IsNullOrEmpty(_config.SpreadsheetPath))
                 {
                     UpdateRszSheet();
                 }
                 MessageBox.Show("Settings saved.");
             }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This software is sponsoreed by Pizza and Korean Eggs", "About RSZViewer");
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            _config.Save();
            Application.Current.Shutdown();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) => LogOutput("Save operation not yet fully implemented.");
        private void BtnSaveAs_Click(object sender, RoutedEventArgs e) => LogOutput("Save As operation not yet fully implemented.");
        private void BtnReload_Click(object sender, RoutedEventArgs e) => RefreshFileList_Click(sender, e);
        
        private void BtnUndo_Click(object sender, RoutedEventArgs e) => LogOutput("Undo requested.");
        private void BtnRedo_Click(object sender, RoutedEventArgs e) => LogOutput("Redo requested.");
        private void BtnCut_Click(object sender, RoutedEventArgs e) => LogOutput("Cut requested.");
        private void BtnCopy_Click(object sender, RoutedEventArgs e) => LogOutput("Copy requested.");
        private void BtnPaste_Click(object sender, RoutedEventArgs e) => LogOutput("Paste requested.");
        private void PerformSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchPanel.Visibility = Visibility.Visible;
            TxtViewSearch.Focus();
        }

        private void BtnToggleOutput_Click(object sender, RoutedEventArgs e)
        {
            PnlOutput.Visibility = PnlOutput.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            LogOutput("Output panel toggled.");
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) { MainScaleTransform.ScaleX += 0.1; MainScaleTransform.ScaleY += 0.1; }
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) { if (MainScaleTransform.ScaleX > 0.5) { MainScaleTransform.ScaleX -= 0.1; MainScaleTransform.ScaleY -= 0.1; } }
        private void BtnZoomReset_Click(object sender, RoutedEventArgs e) { MainScaleTransform.ScaleX = 1.0; MainScaleTransform.ScaleY = 1.0; }

        private void BtnTheme_Click(object sender, RoutedEventArgs e) => LogOutput("Theme switching logic will be implemented in Phase 3.");
        private void BtnDoc_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/namsku/reeutils") { UseShellExecute = true });

        private void LogOutput(string message)
        {
            TxtOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            TxtOutput.ScrollToEnd();
            TxtStatusMessage.Text = message;
        }

        private IList<RszNodeViewModel> CategorizeSceneNodes(RszScene scene)
        {
            var gameObjects = new List<RszNodeViewModel>();
            var folders = new List<RszNodeViewModel>();

            foreach (var node in scene.Children)
            {
                if (node is RszGameObject go)
                {
                    gameObjects.Add(new RszNodeViewModel(go));
                }
                else if (node is RszFolder folder)
                {
                    folders.Add(new RszNodeViewModel(folder));
                }
                else
                {
                    // Fallback for any other IRszSceneNode types
                    gameObjects.Add(new RszNodeViewModel(node));
                }
            }

            var rootNodes = new List<RszNodeViewModel>();
            if (gameObjects.Any())
            {
                var goRoot = new RszNodeViewModel("Game Objects", "", "List") { Icon = "\uE8B7" }; // Cube icon
                foreach (var go in gameObjects) goRoot.Children.Add(go);
                rootNodes.Add(goRoot);
            }
            if (folders.Any())
            {
                var folderRoot = new RszNodeViewModel("Folders", "", "List") { Icon = "\uE8B7" }; // Folder icon
                foreach (var folder in folders) folderRoot.Children.Add(folder);
                rootNodes.Add(folderRoot);
            }

            return rootNodes.Any() ? rootNodes : gameObjects.Concat(folders).ToList();
        }

        private int GetVersion(string fileName, string ext)
        {
            try
            {
                int index = fileName.LastIndexOf(ext);
                if (index != -1 && int.TryParse(fileName.Substring(index + ext.Length), out int version))
                {
                    return version;
                }
            }
            catch { }
            return 20;
        }

        private void Exit_Click(object sender, RoutedEventArgs e) { _config.Save(); Close(); }

        private void SearchOption_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) PerformSearch(); }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMatches.Count > 0)
            {
                _matchIndex--;
                if (_matchIndex < 0) _matchIndex = _currentMatches.Count - 1;
                NavigateToMatch();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMatches.Count > 0)
            {
                _matchIndex++;
                if (_matchIndex >= _currentMatches.Count) _matchIndex = 0;
                NavigateToMatch();
            }
        }

        private void NavigateToMatch()
        {
            if (_matchIndex >= 0 && _matchIndex < _currentMatches.Count)
            {
                var match = _currentMatches[_matchIndex];
                TxtMatchStatus.Text = $"{_matchIndex + 1}/{_currentMatches.Count}";
                
                if (IsInTree(TreeA.ItemsSource as IEnumerable<RszNodeViewModel>, match)) SelectAndScroll(TreeA, match);
                else if (IsInTree(TreeB.ItemsSource as IEnumerable<RszNodeViewModel>, match)) SelectAndScroll(TreeB, match);
            }
        }

        private bool IsInTree(IEnumerable<RszNodeViewModel>? nodes, RszNodeViewModel target)
        {
            if (nodes == null) return false;
            foreach (var node in nodes)
            {
                if (node == target) return true;
                if (IsInTree(node.Children, target)) return true;
            }
            return false;
        }

        private void SelectAndScroll(TreeView tree, RszNodeViewModel node)
        {
            ExpandParents(tree.ItemsSource as IEnumerable<RszNodeViewModel>, node);
            node.IsSelected = true;
            node.IsExpanded = true;
        }

        private bool ExpandParents(IEnumerable<RszNodeViewModel>? nodes, RszNodeViewModel target)
        {
            if (nodes == null) return false;
            foreach (var node in nodes)
            {
                if (node == target) return true;
                if (ExpandParents(node.Children, target))
                {
                    node.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        private void BtnSaveSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery) && !_config.SearchHistory.Contains(SearchQuery))
            {
                _config.SearchHistory.Insert(0, SearchQuery);
                if (_config.SearchHistory.Count > 20) _config.SearchHistory.RemoveAt(20);
                _config.Save();
            }
        }

        // ======== VIEW SEARCH (Ctrl+F) ========

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchPanel.Visibility = Visibility.Visible;
                TxtViewSearch.Focus();
                TxtViewSearch.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && SearchPanel.Visibility == Visibility.Visible)
            {
                BtnCloseSearch_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void TxtViewSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                    BtnViewPrev_Click(sender, new RoutedEventArgs());
                else if (_viewMatches.Count == 0)
                    BtnViewSearch_Click(sender, new RoutedEventArgs());
                else
                    BtnViewNext_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void BtnViewSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformViewSearch();
        }

        private void OpenInlineSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            // Check if we're on the RSZ COMPARE tab
            if (MainTabControl.SelectedItem is TabItem tab && tab.Header?.ToString() == "RSZ COMPARE")
            {
                // Route to compare search
                SearchQuery = query;
                _searchManager.Type = SearchManager.SearchType.Both;
                PerformSearch();
                return;
            }
            
            // Default: RSZ VIEW inline search
            SearchPanel.Visibility = Visibility.Visible;
            TxtViewSearch.Text = query;
            TxtViewSearch.Focus();
            if (CboSearchScope != null) CboSearchScope.SelectedIndex = 0; // Force "Current File"
            PerformViewSearch();
        }

        private void BtnViewPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_viewMatches.Count > 0)
            {
                _viewMatchIndex--;
                if (_viewMatchIndex < 0) _viewMatchIndex = _viewMatches.Count - 1;
                NavigateToViewMatch();
            }
        }

        private void BtnViewNext_Click(object sender, RoutedEventArgs e)
        {
            if (_viewMatches.Count > 0)
            {
                _viewMatchIndex++;
                if (_viewMatchIndex >= _viewMatches.Count) _viewMatchIndex = 0;
                NavigateToViewMatch();
            }
        }

        private void BtnCloseSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchPanel.Visibility = Visibility.Collapsed;
            // Clear highlighting
            foreach (var match in _viewMatches)
            {
                match.IsSearchMatch = false;
                match.OnPropertyChanged(nameof(RszNodeViewModel.IsSearchMatch));
            }
            _viewMatches.Clear();
            _viewMatchIndex = -1;
            TxtViewMatchStatus.Text = "0/0";
        }

        private void BtnViewStop_Click(object sender, RoutedEventArgs e)
        {
            _viewSearchCts?.Cancel();
        }

        private void ViewResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewResultsGrid.SelectedItem is RszNodeViewModel match)
            {
                _viewMatchIndex = _viewMatches.IndexOf(match);
                NavigateToViewMatch();
            }
        }

        private async void PerformViewSearch()
        {
            string query = TxtViewSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) return;

            // Cancel previous
            _viewSearchCts?.Cancel();
            _viewSearchCts = new CancellationTokenSource();
            var token = _viewSearchCts.Token;

            // Clear previous
            foreach (var m in _viewMatches)
            {
                m.IsSearchMatch = false;
                m.OnPropertyChanged(nameof(RszNodeViewModel.IsSearchMatch));
            }
            _viewMatches.Clear();
            _viewMatchIndex = -1;
            TxtViewMatchStatus.Text = "0/0";
            
            // UI State
            BtnViewStop.Visibility = Visibility.Visible;
            PrgViewSearch.Visibility = Visibility.Visible;
            TxtStatusMessage.Text = $"Searching for \"{query}\"...";

            bool matchCase = ChkViewMatchCase?.IsChecked ?? false;
            bool useRegex = ChkViewRegex?.IsChecked ?? false;
            int scope = CboSearchScope?.SelectedIndex ?? 0;

            // Capture UI state
            var currentTab = FileTabControl.SelectedItem as RszTabViewModel;
            var allTabs = _rszTabs.ToList();

            try
            {
                await Task.Run(() =>
                {
                    System.Text.RegularExpressions.Regex? regex = null;
                    if (useRegex)
                    {
                        try
                        {
                            var options = matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                            regex = new System.Text.RegularExpressions.Regex(query, options);
                        }
                        catch 
                        { 
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Invalid regex pattern.")); 
                            return; 
                        }
                    }

                    var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                    if (scope == 0) // Current File
                    {
                        if (currentTab?.Nodes != null)
                            SearchNodes(currentTab.Nodes, query, comparison, regex, token);
                    }
                    else if (scope == 1) // Current Directory
                    {
                        foreach (var tab in allTabs)
                        {
                            if (tab.Nodes != null) SearchNodes(tab.Nodes, query, comparison, regex, token);
                        }
                    }
                    else if (scope == 2) // Deep Scan
                    {
                        PerformDeepScan(query, comparison, regex, token);
                    }
                }, token);

                TxtStatusMessage.Text = $"Search complete: {_viewMatches.Count} matches found.";
                if (_viewMatches.Count > 0)
                {
                    _viewMatchIndex = 0;
                    TxtViewMatchStatus.Text = $"1/{_viewMatches.Count}";
                    NavigateToViewMatch();
                }
            }
            catch (OperationCanceledException)
            {
                TxtStatusMessage.Text = "Search canceled.";
            }
            finally
            {
                BtnViewStop.Visibility = Visibility.Collapsed;
                PrgViewSearch.Visibility = Visibility.Collapsed;
                _viewSearchCts?.Dispose();
                _viewSearchCts = null;
            }
        }

        private void SearchNodes(IEnumerable<RszNodeViewModel> nodes, string query, StringComparison comparison, System.Text.RegularExpressions.Regex? regex, System.Threading.CancellationToken token)
        {
            foreach (var node in nodes)
            {
                if (token.IsCancellationRequested) return;

                bool nameMatch = regex != null ? regex.IsMatch(node.Name ?? "") : (node.Name ?? "").Contains(query, comparison);
                bool valueMatch = regex != null ? regex.IsMatch(node.Value ?? "") : (node.Value ?? "").Contains(query, comparison);
                bool typeMatch = regex != null ? regex.IsMatch(node.TypeName ?? "") : (node.TypeName ?? "").Contains(query, comparison);

                if (nameMatch || valueMatch || typeMatch)
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        node.IsSearchMatch = true;
                        node.OnPropertyChanged(nameof(RszNodeViewModel.IsSearchMatch));
                        _viewMatches.Add(node);
                    });
                }

                if (node.Children != null && node.Children.Count > 0)
                    SearchNodes(node.Children, query, comparison, regex, token);
            }
        }

        private void NavigateToViewMatch()
        {
            if (_viewMatchIndex < 0 || _viewMatchIndex >= _viewMatches.Count) return;
            var match = _viewMatches[_viewMatchIndex];
            TxtViewMatchStatus.Text = $"{_viewMatchIndex + 1}/{_viewMatches.Count}";

            // Find which tab this match belongs to and select it
            foreach (var tab in _rszTabs.OfType<RszTabViewModel>())
            {
                if (tab.Nodes != null && ContainsNode(tab.Nodes, match))
                {
                    FileTabControl.SelectedItem = tab;
                    break;
                }
            }

            // Expand parents and select
            var currentTab = FileTabControl.SelectedItem as RszTabViewModel;
            if (currentTab?.Nodes != null)
            {
                ExpandParents(currentTab.Nodes, match);
                match.IsSelected = true;
                match.IsExpanded = true;
            }
        }

        private bool ContainsNode(IEnumerable<RszNodeViewModel> nodes, RszNodeViewModel target)
        {
            foreach (var node in nodes)
            {
                if (node == target) return true;
                if (node.Children != null && ContainsNode(node.Children, target)) return true;
            }
            return false;
        }

        private void PerformDeepScan(string query, StringComparison comparison, System.Text.RegularExpressions.Regex? regex, CancellationToken token)
        {
            if (string.IsNullOrEmpty(_config.NativesPath) || !Directory.Exists(_config.NativesPath))
            {
                MessageBox.Show("Set Natives path in settings first for Deep Scan.");
                return;
            }

            // Use the current directory from the explorer
            var currentDir = _config.NativesPath;
            Application.Current.Dispatcher.Invoke(() => TxtStatusMessage.Text = $"Deep scanning: {currentDir}...");

            try
            {
                var targetFiles = Directory.EnumerateFiles(currentDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => FileNodeViewModel.IsTargetFileStatic(Path.GetFileName(f)))
                    .Take(100) // Limit to prevent freezing
                    .ToList();

                foreach (var filePath in targetFiles)
                {
                    if (token.IsCancellationRequested) return;

                    try
                    {
                        // Check if already loaded
                        var existing = _rszTabs.OfType<RszTabViewModel>().FirstOrDefault(t => t.FullPath == filePath);
                        if (existing != null)
                        {
                            if (existing.Nodes != null) SearchNodes(existing.Nodes, query, comparison, regex, token);
                            continue;
                        }

                        // Try to load and search
                        string lower = filePath.ToLower();
                        byte[] data = File.ReadAllBytes(filePath);
                        IList<RszNodeViewModel>? viewModels = null;

                        if (lower.Contains(".msg")) viewModels = LoadMsgFile(data, filePath);
                        else if (lower.Contains(".uvar")) viewModels = LoadUvarFile(data, filePath);
                        else if (lower.EndsWith(".pak") || lower.Contains(".pak.")) viewModels = LoadPakFile(filePath);
                        else if (_repo != null)
                        {
                            if (lower.Contains(".scn."))
                            {
                                var version = GetVersion(lower, ".scn.");
                                var scnFile = new ScnFile(version, data);
                                var scene = scnFile.ReadScene(_repo);
                                viewModels = CategorizeSceneNodes(scene);
                            }
                            else if (lower.Contains(".pfb."))
                            {
                                var version = GetVersion(lower, ".pfb.");
                                var pfbFile = new PfbFile(version, data);
                                var scene = pfbFile.ReadScene(_repo);
                                viewModels = CategorizeSceneNodes(scene);
                            }
                            else if (lower.Contains(".user."))
                            {
                                var userFile = new UserFile(data);
                                var builder = userFile.ToBuilder(_repo);
                                viewModels = builder.Objects.Select((n, i) => new RszNodeViewModel(n) { Name = $"Object {i}" }).Cast<RszNodeViewModel>().ToList();
                            }
                        }

                        if (viewModels != null && viewModels.Count > 0)
                        {
                            // Search without loading tab (SearchNodes dispatches match additions)
                            SearchNodes(viewModels, query, comparison, regex, token);
                            
                            // Only add tab if matches found
                            if (viewModels.Any(vm => ContainsAnyMatch(vm)))
                            {
                                Application.Current.Dispatcher.Invoke(() => 
                                {
                                    var tab = new RszTabViewModel { Header = Path.GetFileName(filePath), FullPath = filePath, Nodes = new ObservableCollection<RszNodeViewModel>(viewModels) };
                                    _rszTabs.Add(tab);
                                });
                            }
                        }
                    }
                    catch { /* Skip files that fail to load */ }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Deep scan error: {ex.Message}"); }
        }



        private bool ContainsAnyMatch(RszNodeViewModel node)
        {
            if (node.IsSearchMatch) return true;
            if (node.Children != null)
                foreach (var child in node.Children)
                    if (ContainsAnyMatch(child)) return true;
            return false;
        }

        private void PerformSearch()
        {
            if (_repo == null) return;
            
            _searchManager.Query = SearchQuery;
            _searchManager.MatchCase = SearchChkMatchCase?.IsChecked ?? false;
            _searchManager.UseRegex = SearchChkRegex?.IsChecked ?? false;
            _searchManager.ShowDifferencesOnly = SearchChkDiffOnly?.IsChecked ?? false;
            
            if (SearchRadPropName?.IsChecked == true) _searchManager.Type = SearchManager.SearchType.PropertyName;
            else if (SearchRadPropValue?.IsChecked == true) _searchManager.Type = SearchManager.SearchType.PropertyValue;
            else _searchManager.Type = SearchManager.SearchType.Both;

            if (TreeA.ItemsSource is IEnumerable<RszNodeViewModel> nodesA) _searchManager.ExecuteSearch(nodesA);
            if (TreeB.ItemsSource is IEnumerable<RszNodeViewModel> nodesB) _searchManager.ExecuteSearch(nodesB);

            _currentMatches = _searchManager.Matches.Where(m => m.Node != null).Select(m => m.Node!).ToList();
            _matchIndex = _currentMatches.Count > 0 ? 0 : -1;
            TxtMatchStatus.Text = _currentMatches.Count > 0 ? $"1/{_currentMatches.Count}" : "0/0";

            RefreshTreeVisibility(TreeA.ItemsSource as IEnumerable<RszNodeViewModel>);
            RefreshTreeVisibility(TreeB.ItemsSource as IEnumerable<RszNodeViewModel>);
            
            if (_currentMatches.Count > 0) NavigateToMatch();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        private void TxtCompareSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                PerformSearch();
            }
        }

        private void RefreshTreeVisibility(IEnumerable<RszNodeViewModel>? nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                node.OnPropertyChanged("NodeVisibility");
                if (node.HasMatchInChildren || node.IsSearchMatch) node.IsExpanded = true;
                RefreshTreeVisibility(node.Children);
            }
        }

        private void LoadRsz_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "RSZ JSON|*.json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _repo = RszRepositorySerializer.Default.FromJsonFile(dlg.FileName);
                    _config.RszRepoPath = dlg.FileName;
                    _config.Save();
                    MessageBox.Show("RSZ Definitions loaded successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading RSZ JSON: " + ex.Message);
                }
            }
        }

        private void BtnLoadA_Click(object sender, RoutedEventArgs e) => LoadFileWithDialog(true);
        private void BtnLoadB_Click(object sender, RoutedEventArgs e) => LoadFileWithDialog(false);

        private void FileA_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadFile(true, files[0]);
            }
        }

        private void FileB_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadFile(false, files[0]);
            }
        }

        private void LoadFileWithDialog(bool isA)
        {
            if (!EnsureRszLoaded()) return;
            var dlg = new OpenFileDialog { Filter = "Engine Files|*.user.*;*.scn.*;*.pfb.*;*.msg.*;*.uvar*;*.pak|All files|*.*" };
            if (dlg.ShowDialog() == true) LoadFile(isA, dlg.FileName);
        }

        private bool EnsureRszLoaded()
        {
            if (_repo == null)
            {
                if (MessageBox.Show("RSZ Definitions not loaded. Do you want to load them now?", "Missing RSZ", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    LoadRsz_Click(this, new RoutedEventArgs());
                    return _repo != null;
                }
                return false;
            }
            return true;
        }

        private void LoadFile(bool isA, string filePath)
        {
            try
            {
                string lower = filePath.ToLower();
                // Special handling for textures (no RSZ needed)
                // Special handling for textures (no RSZ needed)
                if (lower.Contains(".tex"))
                {
                    // Check if already open
                    var existingTex = _rszTabs.FirstOrDefault(t => t.FullPath == filePath);
                    if (existingTex != null)
                    {
                        FileTabControl.SelectedItem = existingTex;
                        return;
                    }

                    var texTab = new TextureTabViewModel { Header = Path.GetFileName(filePath), FullPath = filePath };
                    _rszTabs.Add(texTab);
                    FileTabControl.SelectedItem = texTab;
                    return;
                }

                byte[] data = File.ReadAllBytes(filePath);
                IList<RszNodeViewModel> viewModels;

                // File types that don't require RSZ definitions
                if (lower.EndsWith(".msg") || lower.Contains(".msg."))
                {
                    viewModels = LoadMsgFile(data, filePath);
                }
                else if (lower.EndsWith(".uvar") || lower.Contains(".uvar."))
                {
                    viewModels = LoadUvarFile(data, filePath);
                }
                else if (lower.EndsWith(".pak") || lower.Contains(".pak."))
                {
                    viewModels = LoadPakFile(filePath);
                }
                else
                {
                if (!EnsureRszLoaded()) return;

                if (lower.Contains(".scn."))
                {
                    int version = GetVersion(lower, ".scn.");
                    var scnFile = new ScnFile(version, data);
                    var scene = scnFile.ReadScene(_repo);
                    viewModels = scene.Children.Select(n => new RszNodeViewModel(n)).ToList();
                }
                else if (lower.Contains(".pfb."))
                {
                    int version = GetVersion(lower, ".pfb.");
                    var pfbFile = new PfbFile(version, data);
                    var scene = pfbFile.ReadScene(_repo);
                    viewModels = scene.Children.Select(n => new RszNodeViewModel(n)).ToList();
                }
                else if (lower.Contains(".tex"))
                {
                    viewModels = new List<RszNodeViewModel>(); // No RSZ nodes for textures
                    
                    var mainTab = new TabItem { Header = Path.GetFileName(filePath) };
                    var mainViewer = new TextureViewer();
                    mainViewer.LoadTexture(filePath);
                    mainTab.Content = mainViewer;
                    
                    // Add close button (simple approach for now, or use style)
                    // For consistency we should probably use a similar header template, but standard is fine for now.
                    
                    MainTabControl.Items.Add(mainTab);
                    MainTabControl.SelectedItem = mainTab;
                    
                    AddToHistory();
                    return;
                }
                else if (lower.Contains(".aimap"))
                {
                    var aimap = new AimapFile(data);

                    // Create a structured view with AIMAP metadata + RSZ content
                    viewModels = new List<RszNodeViewModel>();

                    // Create a root node for the AIMAP info
                    var aimapInfoNode = new RszNodeViewModel($"📍 {aimap.MapName}", aimap.ToString(), "AimapFile");
                    aimapInfoNode.Children.Add(new RszNodeViewModel("GUID", aimap.MapGuid.ToString(), "System.Guid"));
                    
                    // Show each component
                    foreach (var comp in aimap.Components)
                    {
                        var shortName = comp.TypeName.Split('.').LastOrDefault() ?? comp.TypeName;
                        var compNode = new RszNodeViewModel($"📦 {shortName}", $"{comp.TypeName} ({comp.DataSize:N0} bytes)", "Component");
                        aimapInfoNode.Children.Add(compNode);
                    }
                    viewModels.Add(aimapInfoNode);

                    // RSZ objects
                    var instanceList = aimap.Rsz.ReadInstanceList(_repo);
                    var rszNode = new RszNodeViewModel($"RSZ Objects [{instanceList.Length}]", $"{instanceList.Length} instances", "RszFile");
                    foreach (var inst in instanceList)
                    {
                        if (inst.Value != null)
                        {
                            rszNode.Children.Add(new RszNodeViewModel(inst.Value, $"Instance {inst.Id.Index}"));
                        }
                    }
                    viewModels.Add(rszNode);
                }
                else
                {
                    var userFile = new UserFile(data);
                    var builder = userFile.ToBuilder(_repo);
                    viewModels = builder.Objects.Select((n, i) => new RszNodeViewModel(n, $"Object {i}")).ToList();
                }
                } // end else (RSZ-requiring types)

                if (isA) { _cachedCompareLeft = viewModels; TreeA.ItemsSource = viewModels; TxtFileA.Text = filePath; _config.LeftFilePath = filePath; }
                else { _cachedCompareRight = viewModels; TreeB.ItemsSource = viewModels; TxtFileB.Text = filePath; _config.RightFilePath = filePath; }
                
                AddToHistory();
                _config.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}");
            }
        }

        private IList<RszNodeViewModel> LoadMsgFile(byte[] data, string filePath)
        {
            var list = new List<RszNodeViewModel>();
            try
            {
                var msgFile = new MsgFile(data);
                var root = new RszNodeViewModel($"\uD83D\uDCAC Messages", $"{msgFile.Count} entries", "MsgFile") { Icon = "\uE8B7" };

                for (int i = 0; i < msgFile.Count; i++)
                {
                    var msg = msgFile.GetMessage(i);
                    var msgNode = new RszNodeViewModel(msg.Name ?? $"Entry {i}", msg.Guid.ToString(), "Message") { Icon = "\uE735" };

                    // Add language values
                    foreach (var val in msg.Values)
                    {
                        msgNode.Children.Add(new RszNodeViewModel(val.Language.ToString(), val.Text ?? "", "String"));
                    }

                    // Add attributes
                    if (msg.Attributes.Count > 0)
                    {
                        var attrNode = new RszNodeViewModel("Attributes", $"{msg.Attributes.Count}", "List") { Icon = "\uE71D" };
                        foreach (var attr in msg.Attributes)
                        {
                            attrNode.Children.Add(new RszNodeViewModel(attr.Name, attr.Value?.ToString() ?? "null", attr.Type.ToString()));
                        }
                        msgNode.Children.Add(attrNode);
                    }

                    root.Children.Add(msgNode);
                }
                list.Add(root);
            }
            catch (Exception ex)
            {
                list.Add(new RszNodeViewModel("Error", ex.Message, "Error"));
            }
            return list;
        }

        private IList<RszNodeViewModel> LoadUvarFile(byte[] data, string filePath)
        {
            var list = new List<RszNodeViewModel>();
            try
            {
                var uvar = new UvarFile(data);
                var builder = uvar.ToBuilder();

                var root = new RszNodeViewModel($"\uD83D\uDD27 {builder.Name}", $"v{uvar.Version} — {builder.Variables.Count} variables", "UvarFile") { Icon = "\uE8B7" };

                foreach (var v in builder.Variables)
                {
                    root.Children.Add(new RszNodeViewModel(v.Name, v.Value.ToString("F4"), "Float"));
                }

                // Embedded children
                for (int i = 0; i < builder.Children.Count; i++)
                {
                    var child = builder.Children[i];
                    var childNode = new RszNodeViewModel($"\uD83D\uDCC1 {child.Name}", $"{child.Variables.Count} variables", "UvarFile");
                    foreach (var v in child.Variables)
                    {
                        childNode.Children.Add(new RszNodeViewModel(v.Name, v.Value.ToString("F4"), "Float"));
                    }
                    root.Children.Add(childNode);
                }

                list.Add(root);
            }
            catch (Exception ex)
            {
                list.Add(new RszNodeViewModel("Error", ex.Message, "Error"));
            }
            return list;
        }

        private IList<RszNodeViewModel> LoadPakFile(string filePath)
        {
            var list = new List<RszNodeViewModel>();
            try
            {
                using var pak = new PakFile(filePath);
                var root = new RszNodeViewModel($"\uD83D\uDCE6 {Path.GetFileName(filePath)}", $"{pak.EntryCount} files", "PakFile") { Icon = "\uE8B7" };

                for (int i = 0; i < pak.EntryCount; i++)
                {
                    var hash = pak.GetEntryHash(i);
                    root.Children.Add(new RszNodeViewModel($"0x{hash:X16}", $"Entry {i}", "PakEntry"));
                }
                list.Add(root);
            }
            catch (Exception ex)
            {
                list.Add(new RszNodeViewModel("Error", ex.Message, "Error"));
            }
            return list;
        }

        private void AddToHistory()
        {
            if (string.IsNullOrEmpty(_config.LeftFilePath) || string.IsNullOrEmpty(_config.RightFilePath)) return;
            var existing = _config.RecentFiles.FirstOrDefault(x => x.LeftPath == _config.LeftFilePath && x.RightPath == _config.RightFilePath);
            if (existing != null)
            {
                _config.RecentFiles.Remove(existing);
                existing.LastAccessed = DateTime.Now;
                _config.RecentFiles.Insert(0, existing);
            }
            else
            {
                _config.RecentFiles.Insert(0, new FileHistoryItem { LeftPath = _config.LeftFilePath, RightPath = _config.RightFilePath, LastAccessed = DateTime.Now });
            }
            if (_config.RecentFiles.Count > 10) _config.RecentFiles.RemoveAt(_config.RecentFiles.Count - 1);
        }

        private void TreeA_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => TryCompare();
        private void TreeB_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => TryCompare();

        private void TryCompare()
        {
            try
            {
                if (TreeA.SelectedItem is RszNodeViewModel itemA && TreeB.SelectedItem is RszNodeViewModel itemB)
                {
                    List<DiffItem> diffs = new List<DiffItem>();
                    if (itemA.GameObject != null && itemB.GameObject != null) diffs = CompareGameObjects(itemA.GameObject, itemB.GameObject);
                    else if (itemA.Node is RszObjectNode nA && itemB.Node is RszObjectNode nB) diffs = CompareNodes(nA, nB);
                    GridDiff.ItemsSource = diffs;
                }
                else GridDiff.ItemsSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error comparing objects: " + ex.ToString());
            }
        }

        private List<DiffItem> CompareGameObjects(RszGameObject goA, RszGameObject goB)
        {
            var diffs = new List<DiffItem>();
            foreach(var compA in goA.Components)
            {
                var compB = goB.Components.FirstOrDefault(c => c.Type.Name == compA.Type.Name);
                if (compB != null) CompareValues(diffs, compA.Type.Name, "", compA, compB);
                else diffs.Add(new DiffItem { ComponentName = "GameObject", FieldName = "Simple Component", ValueA = compA.Type.Name, ValueB = "<Missing>" });
            }
            foreach(var compB in goB.Components)
            {
                if (goA.Components.All(c => c.Type.Name != compB.Type.Name))
                {
                    diffs.Add(new DiffItem { ComponentName = "GameObject", FieldName = "Simple Component", ValueA = "<Missing>", ValueB = compB.Type.Name });
                }
            }
            return diffs;
        }

        private List<DiffItem> CompareNodes(RszObjectNode nodeA, RszObjectNode nodeB)
        {
            var diffs = new List<DiffItem>();
            if (nodeA.Type != nodeB.Type)
            {
                diffs.Add(new DiffItem { ComponentName = "Object", FieldName = "Type", ValueA = nodeA.Type.Name, ValueB = nodeB.Type.Name });
                return diffs;
            }
            for (int i = 0; i < nodeA.Type.Fields.Length; i++)
            {
                CompareValues(diffs, nodeA.Type.Name, nodeA.Type.Fields[i].Name, nodeA.Children[i], nodeB.Children[i]);
            }
            return diffs;
        }

        private void CompareValues(List<DiffItem> diffs, string componentName, string fieldName, IRszNode valA, IRszNode valB)
        {
            if (valA is RszValueNode va && valB is RszValueNode vb)
            {
                if (!va.Equals(vb))
                {
                    var item = new DiffItem { ComponentName = componentName, FieldName = fieldName, ValueA = FormatRszValue(va), ValueB = FormatRszValue(vb) };
                    CheckMatches(item);
                    diffs.Add(item);
                }
            }
            else if (valA is RszObjectNode oa && valB is RszObjectNode ob)
            {
                var subDiffs = CompareNodes(oa, ob);
                foreach (var d in subDiffs)
                {
                    d.FieldName = string.IsNullOrEmpty(fieldName) ? d.FieldName : $"{fieldName}.{d.FieldName}";
                    if (d.ComponentName == "Object" || d.ComponentName == oa.Type.Name) d.ComponentName = componentName;
                    CheckMatches(d);
                    diffs.Add(d);
                }
            }
            else if (valA is RszArrayNode aa && valB is RszArrayNode ab)
            {
                if (aa.Children.Length != ab.Children.Length)
                {
                    var item = new DiffItem { ComponentName = componentName, FieldName = $"{fieldName}.Count", ValueA = aa.Children.Length.ToString(), ValueB = ab.Children.Length.ToString() };
                    CheckMatches(item);
                    diffs.Add(item);
                }
                int count = Math.Min(aa.Children.Length, ab.Children.Length);
                for(int i=0; i<count; i++)
                {
                    CompareValues(diffs, componentName, $"{fieldName}[{i}]", aa.Children[i], ab.Children[i]);
                }
            }
            else
            {
                string sA = valA?.ToString() ?? "null";
                string sB = valB?.ToString() ?? "null";
                if (sA != sB)
                {
                    var item = new DiffItem { ComponentName = componentName, FieldName = fieldName, ValueA = sA, ValueB = sB };
                    CheckMatches(item);
                    diffs.Add(item);
                }
            }
        }

        private string FormatRszValue(RszValueNode node)
        {
            string s = node.ToString() ?? "";
            if (s.StartsWith("System.ReadOnlyMemory") || s.Contains("ReadOnlyMemory<Byte>"))
            {
                if (node.Data.Length == 16) return new Guid(node.Data.Span).ToString();
                return BitConverter.ToString(node.Data.ToArray()).Replace("-", " ");
            }
            return s;
        }

        private void CheckMatches(DiffItem item)
        {
            if (_searchManager == null) return;
            if (_searchManager.Type == SearchManager.SearchType.PropertyName || _searchManager.Type == SearchManager.SearchType.Both)
            {
                if (_searchManager.IsTextMatch(item.FieldName)) item.IsNameMatch = true;
            }
            if (_searchManager.Type == SearchManager.SearchType.PropertyValue || _searchManager.Type == SearchManager.SearchType.Both)
            {
                if (_searchManager.IsTextMatch(item.ValueA)) item.IsValueAMatch = true;
                if (_searchManager.IsTextMatch(item.ValueB)) item.IsValueBMatch = true;
            }
        }

        // --- Event Handlers from Decompiled Code ---

        private void MenuItemTrace_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm && cm.PlacementTarget is TreeView tree && tree.SelectedItem is RszNodeViewModel selected)
            {
                if (selected.GameObject != null && _repo != null)
                {
                    TracerUI.Initialize(_repo, selected.GameObject);
                    MainTabControl.SelectedIndex = 2; // RSZ LINK tab
                }
                else MessageBox.Show("Please select a GameObject to trace.");
            }
        }

        private void CopyValue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                Clipboard.SetText(!string.IsNullOrEmpty(vm.Value) ? vm.Value : vm.Name);
            }
        }

        private void LinkObject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm && vm.GameObject != null)
            {
                foreach(TabItem tab in MainTabControl.Items) if (tab.Header.ToString() == "RSZ LINK") { MainTabControl.SelectedItem = tab; break; }
                if (_repo != null) TracerUI.Initialize(_repo, vm.GameObject);
                else MessageBox.Show("RSZ Repository not loaded.");
            }
        }

        private void BtnGuidGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                vm.Value = Guid.NewGuid().ToString();
                vm.OnPropertyChanged(nameof(RszNodeViewModel.Value));
            }
        }

        private void BtnGuidReset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                vm.Value = Guid.Empty.ToString();
                vm.OnPropertyChanged(nameof(RszNodeViewModel.Value));
            }
        }

        private void BtnGuidSearch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                OpenInlineSearch(vm.Value);
            }
        }

        /// <summary>
        /// Double-click on a tree node: if it's a resource, open it
        /// </summary>
        private void RszNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                // Check if this looks like a file path (resource or userdata)
                if (vm.IsResource || LooksLikeFilePath(vm.Value))
                {
                    OpenResourceFile(vm.Value);
                    e.Handled = true;
                }
            }
        }

        private bool LooksLikeFilePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            // Check for common file patterns
            return value.Contains("\\") || value.Contains("/") ||
                   value.EndsWith(".scn", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith(".pfb", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith(".user", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith(".tex", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("natives", StringComparison.OrdinalIgnoreCase);
        }

        private void BtnResourceOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                OpenResourceFile(vm.Value);
            }
        }

        /// <summary>
        /// Resolves a resource path and opens it in a new tab
        /// </summary>
        private void OpenResourceFile(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return;
            
            // Try to resolve path - it could be relative like "natives\x64\..." or a full path
            string? fullPath = ResolveResourcePath(resourcePath);
            
            if (fullPath != null && File.Exists(fullPath))
            {
                LoadFileForView(fullPath);
            }
            else
            {
                MessageBox.Show($"Could not find file:\n{resourcePath}\n\nTried: {fullPath ?? "(no base path set)"}", 
                    "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Resolves a resource path to an absolute path using the current base folder
        /// </summary>
        private string? ResolveResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            
            // Normalize path separators
            resourcePath = resourcePath.Replace("/", "\\");
            
            // If already absolute, use as-is
            if (Path.IsPathRooted(resourcePath) && File.Exists(resourcePath))
                return resourcePath;
            
            // Get base path from current file or configured folder
            string? basePath = null;
            
            // Try to get from current tab's file path
            if (MainTabControl.SelectedItem is TabItem tab && tab.Tag is string tabPath)
            {
                // Walk up to find "natives" folder
                var dir = Path.GetDirectoryName(tabPath);
                while (!string.IsNullOrEmpty(dir))
                {
                    if (Path.GetFileName(dir)?.Equals("natives", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        basePath = Path.GetDirectoryName(dir); // One level above "natives"
                        break;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            }
            
            // Try from recent folders
            if (basePath == null && RecentFolders.Count > 0)
            {
                foreach (var folder in RecentFolders)
                {
                    var candidate = Path.Combine(folder, resourcePath);
                    if (File.Exists(candidate)) return candidate;
                    
                    // Also try stripping "natives\" prefix if present
                    if (resourcePath.StartsWith("natives\\", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = Path.Combine(folder, resourcePath.Substring(8));
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            
            // Build full path
            if (basePath != null)
            {
                var fullPath = Path.Combine(basePath, resourcePath);
                if (File.Exists(fullPath)) return fullPath;
            }
            
            return null;
        }

        // --- New Button Handlers for Vectors ---
        private void BtnVectorCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm && vm.VectorValues != null)
            {
                Clipboard.SetText(string.Join(" ", vm.VectorValues.Select(v => v.ComponentValue)));
            }
        }

        private void BtnVectorPaste_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm && vm.VectorValues != null)
            {
                var text = Clipboard.GetText();
                if(!string.IsNullOrWhiteSpace(text))
                {
                    var parts = text.Split(new[] { ',', ' ', '\t', ';', '<', '>', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == vm.VectorValues.Count)
                    {
                        for(int i=0; i<parts.Length; i++) vm.VectorValues[i].ComponentValue = parts[i];
                    }
                    else MessageBox.Show($"Clipboard data format mismatch. Expected {vm.VectorValues.Count} values.");
                }
            }
        }

        private void BtnVectorReset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm && vm.VectorValues != null)
            {
                foreach(var v in vm.VectorValues) v.ComponentValue = "0";
            }
        }


        
        // Renamed from BtnSearch_Click to avoid confusion with new search
        // Renamed from BtnSearch_Click to avoid confusion with new search
        private void BtnFindObject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                OpenInlineSearch(vm.Value);
            }
        }
        
        private void OpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string? pathToOpen = null;
            
            if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm)
            {
                // Try to resolve resource path to absolute path
                pathToOpen = ResolveResourcePath(vm.Value);
            }
            else if (sender is FrameworkElement fe2 && fe2.DataContext is FileNodeViewModel fm)
            {
                pathToOpen = fm.FullPath;
            }
            
            if (!string.IsNullOrEmpty(pathToOpen) && (File.Exists(pathToOpen) || Directory.Exists(pathToOpen)))
            {
                Process.Start("explorer.exe", $"/select,\"{pathToOpen}\"");
            }
            else if (!string.IsNullOrEmpty(pathToOpen))
            {
                // Try current tab's folder as fallback
                if (MainTabControl.SelectedItem is TabItem tab && tab.Tag is string tabPath && File.Exists(tabPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{tabPath}\"");
                }
                else
                {
                    MessageBox.Show($"Could not resolve path:\n{pathToOpen}", "Path Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void OpenTerminal_Click(object sender, RoutedEventArgs e)
        {
             string? path = null;
             if (sender is FrameworkElement fe && fe.DataContext is RszNodeViewModel vm) path = vm.Value; // Check if it's a file path
             if (sender is FrameworkElement fe2 && fe2.DataContext is FileNodeViewModel fm) path = fm.FullPath;
             
             if (!string.IsNullOrEmpty(path))
             {
                 string? dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                 if (dir != null && Directory.Exists(dir))
                 {
                     Process.Start(new ProcessStartInfo("cmd.exe", $"/k cd /d \"{dir}\"") { UseShellExecute = true });
                 }
             }
        }

        // RszSheet Methods
        private void UpdateRszSheet()
        {
            if (_config.SpreadsheetPath != null && SheetVM != null)
            {
                _ = Dispatcher.InvokeAsync(() => 
                {
                     if (!string.IsNullOrEmpty(_config.SpreadsheetPath)) 
                        SheetVM.LoadSheet(_config.SpreadsheetPath);
                });
            }
        }

        private void BtnReloadSheet_Click(object sender, RoutedEventArgs e)
        {
            UpdateRszSheet();
        }
    }

    // --- SUPPORTING CLASSES (Restored) ---

    public class RszNodeViewModel : INotifyPropertyChanged
    {
        public IRszNode? Node { get; }
        public RszGameObject? GameObject { get; }
        
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string IconColor { get; set; } = "#64B5F6"; // Default: light blue
        public Brush IconColorBrush 
        { 
            get 
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(IconColor)); }
                catch { return Brushes.LightBlue; }
            } 
        }
        private string _value = "";
        public string Value 
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPropertyChanged(nameof(Value)); UpdateVectors(); } }
        }
        public string? TypeName { get; }
        public ObservableCollection<RszNodeViewModel> Children { get; }
        public RszFieldType? FieldType { get; set; }
        public ObservableCollection<RszVectorComponent>? VectorValues { get; set; }

        public bool IsBool => FieldType == RszFieldType.Bool;
        public bool IsGuid => FieldType == RszFieldType.Guid;
        public bool IsString => FieldType == RszFieldType.String;
        public bool IsResource => FieldType == RszFieldType.Resource || FieldType == RszFieldType.UserData;
        public bool IsNumeric => FieldType == RszFieldType.S8 || FieldType == RszFieldType.U8 ||
                               FieldType == RszFieldType.S16 || FieldType == RszFieldType.U16 ||
                               FieldType == RszFieldType.S32 || FieldType == RszFieldType.U32 ||
                               FieldType == RszFieldType.S64 || FieldType == RszFieldType.U64 ||
                               FieldType == RszFieldType.F32 || FieldType == RszFieldType.F64;
        public bool IsVector => FieldType == RszFieldType.Vec2 || FieldType == RszFieldType.Vec3 || FieldType == RszFieldType.Vec4 ||
                              FieldType == RszFieldType.Float2 || FieldType == RszFieldType.Float3 || FieldType == RszFieldType.Float4 ||
                              FieldType == RszFieldType.Int2 || FieldType == RszFieldType.Int3 || FieldType == RszFieldType.Int4 ||
                              FieldType == RszFieldType.Quaternion;
        
        public bool BoolValue
        {
            get => bool.TryParse(Value, out var b) && b;
            set
            {
                Value = value.ToString();
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(BoolValue));
            }
        }
        
        // Helper for Search
        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        public bool IsSearchMatch { get; set; }
        public bool HasMatchInChildren { get; set; }
        public int MatchCount { get; set; }
        public Visibility NodeVisibility { get; set; } = Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public RszNodeViewModel(RszGameObject go) : this((IRszNode)go, go.Name) { }

        public RszNodeViewModel(IRszNode node, string? name = null, RszFieldType? fieldType = null)
        {
            if (node is RszGameObject go)
            {
                GameObject = go;
                Name = name ?? go.Name;
                Icon = go.Children.Any() ? "\uE8B7" : "\uEA86";
                IconColor = "#66BB6A"; // Green for GameObjects
                TypeName = "GameObject";
                FieldType = fieldType;
                Children = new ObservableCollection<RszNodeViewModel>();
                // Add special children
                Children.Add(new RszNodeViewModel("GUID", go.Guid.ToString(), "System.Guid", go, RszFieldType.Guid) { Icon = "\uE8EC", IconColor = "#AB47BC" }); // Purple tag
                if (go.Settings != null) Children.Add(new RszNodeViewModel(go.Settings, "Settings"));
                foreach(var c in go.Components) Children.Add(new RszNodeViewModel(c, c.Type.Name));
                
                // Add child GameObjects in a subcategory if there are any
                if (go.Children.Any())
                {
                    var childObjectsNode = new RszNodeViewModel("Child Objects", $"{go.Children.Count()} children", "List") { Icon = "\uE8F1", IconColor = "#26A69A" }; // Teal list
                    foreach (var child in go.Children) childObjectsNode.Children.Add(new RszNodeViewModel(child));
                    Children.Add(childObjectsNode);
                }
                return;
            }

            if (node is RszFolder folder)
            {
                Name = name ?? folder.Name;
                Icon = "\uE8B7";
                IconColor = "#FFD54F"; // Yellow for Folders
                TypeName = "Folder";
                Children = new ObservableCollection<RszNodeViewModel>();
                if (folder.Settings != null) Children.Add(new RszNodeViewModel(folder.Settings, "Settings"));
                foreach (var child in folder.Children)
                {
                    Children.Add(new RszNodeViewModel(child));
                }
                return;
            }

            Node = node;
            Name = name ?? node.GetType().Name.Replace("Rsz", "");
            FieldType = fieldType;
            Children = new ObservableCollection<RszNodeViewModel>();

            if (node is RszObjectNode obj)
            {
                Icon = "\uE713";
                IconColor = "#4FC3F7"; // Cyan for components/objects
                TypeName = obj.Type.Name;
                if (obj.Children != null)
                {
                    for(int i=0; i<obj.Type.Fields.Length; i++)
                    {
                        var field = obj.Type.Fields[i];
                        if (i < obj.Children.Length)
                        {
                            var childVal = obj.Children[i];
                            if (childVal is IRszNode childNode)
                            {
                                Children.Add(new RszNodeViewModel(childNode, field.Name, field.Type));
                            }
                            else
                            {
                                // Handle null or non-node values (should be rare in RszObjectNode)
                                Children.Add(new RszNodeViewModel(field.Name, childVal?.ToString() ?? "null", field.Type.ToString(), null, field.Type));
                            }
                        }
                    }
                }
            }
            /*
            else if (node is RszInstance inst)
            {
                 // RszInstance is a struct without 'Type' property directly accessible in this context apparently?
                 // Will rely on fallback to identify it.
            }
            */
            else if (node is RszValueNode val)
            {
                TypeName = fieldType?.ToString() ?? "Value";
                FieldType = fieldType;
                
                // Check if this is a vector type first - vectors should NOT be parsed as GUIDs
                bool isVectorType = fieldType == RszFieldType.Vec2 || fieldType == RszFieldType.Vec3 || fieldType == RszFieldType.Vec4 ||
                                   fieldType == RszFieldType.Float2 || fieldType == RszFieldType.Float3 || fieldType == RszFieldType.Float4 ||
                                   fieldType == RszFieldType.Int2 || fieldType == RszFieldType.Int3 || fieldType == RszFieldType.Int4 ||
                                   fieldType == RszFieldType.Quaternion;
                
                // GUID detection for 16-byte memory - but ONLY if it's not a vector type
                if (val.Data.Length == 16 && !isVectorType)
                {
                    try
                    {
                        var guid = new Guid(val.Data.Span);
                        Value = guid.ToString();
                        FieldType = RszFieldType.Guid;
                        TypeName = fieldType?.ToString() ?? "Guid";
                    }
                    catch
                    {
                        // If GUID parsing fails, fall back to string representation
                        Value = val.ToString() ?? "";
                    }
                }
                else
                {
                    Value = val.ToString() ?? "";
                }
                
                if (IsVector && !string.IsNullOrEmpty(Value))
                {
                     var clean = Value.Replace("<", "").Replace(">", "").Replace("{", "").Replace("}", "")
                                      .Replace("X:", "").Replace("Y:", "").Replace("Z:", "").Replace("W:", "");
                     var parts = clean.Split(new[] { ", ", " " }, StringSplitOptions.RemoveEmptyEntries);
                     VectorValues = new ObservableCollection<RszVectorComponent>();
                     for(int i=0; i<parts.Length; i++) VectorValues.Add(new RszVectorComponent(this, i) { ComponentValue = parts[i] });
                }
            }
            else if (node is IEnumerable<IRszNode> collection)
            {
                Icon = "\uE71D"; // List icon
                IconColor = "#26A69A"; // Teal for arrays
                TypeName = "Array";
                FieldType = fieldType;
                var count = collection.Count();
                Value = $"Array[{count}]";
                Children = new ObservableCollection<RszNodeViewModel>();
                int i = 0;
                foreach (var item in collection)
                {
                    var itemVM = new RszNodeViewModel(item, $"[{i}]", null); 
                    Children.Add(itemVM);
                    i++;
                }
            }
            else
            {
                 TypeName = node.GetType().Name.Replace("Rsz", "");
                 Value = node.ToString() ?? "";
            }
        }

        public RszNodeViewModel(string name, string value, string typeName, RszGameObject? go = null, RszFieldType? fieldType = null)
        {
            Name = name;
            Value = value;
            TypeName = typeName;
            GameObject = go;
            FieldType = fieldType;
            Children = new ObservableCollection<RszNodeViewModel>();

            // Set icon color based on type
            if (typeName == "System.Guid" || fieldType == RszFieldType.Guid)
                IconColor = "#AB47BC"; // Purple
            else if (typeName == "String" || fieldType == RszFieldType.String)
                IconColor = "#FFA726"; // Orange
            else if (typeName == "Float" || fieldType == RszFieldType.F32 || fieldType == RszFieldType.F64)
                IconColor = "#29B6F6"; // Light blue
            else if (typeName == "Component" || typeName == "AimapFile" || typeName == "MsgFile" || typeName == "UvarFile" || typeName == "PakFile")
                IconColor = "#4FC3F7"; // Cyan
            else if (typeName == "Error")
                IconColor = "#EF5350"; // Red
            else if (typeName == "List" || typeName == "Array")
                IconColor = "#26A69A"; // Teal
            else if (typeName == "Message")
                IconColor = "#FFEE58"; // Yellow
            else if (typeName == "PakEntry")
                IconColor = "#8D6E63"; // Brown

            if (IsVector && !string.IsNullOrEmpty((value)))
            {
                var clean = value.Replace("<", "").Replace(">", "").Replace("{", "").Replace("}", "")
                                 .Replace("X:", "").Replace("Y:", "").Replace("Z:", "").Replace("W:", "");
                var parts = clean.Split(new[] { ", ", " " }, StringSplitOptions.RemoveEmptyEntries);
                VectorValues = new ObservableCollection<RszVectorComponent>();
                for(int i=0; i<parts.Length; i++) VectorValues.Add(new RszVectorComponent(this, i) { ComponentValue = parts[i] });
            }
        }

        public void UpdateVectorFromComponents()
        {
            if (VectorValues != null)
            {
                Value = string.Join(" ", VectorValues.Select(v => v.ComponentValue));
                OnPropertyChanged(nameof(Value));
            }
        }

        private void UpdateVectors() 
        {
            if (VectorValues == null || string.IsNullOrEmpty(Value)) return;
            
            var clean = Value.Replace("<", "").Replace(">", "").Replace("{", "").Replace("}", "")
                                .Replace("X:", "").Replace("Y:", "").Replace("Z:", "").Replace("W:", "");
            var parts = clean.Split(new[] { ", ", " " }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == VectorValues.Count)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (VectorValues[i].ComponentValue != parts[i])
                    {
                        VectorValues[i].ComponentValue = parts[i];
                    }
                }
            }
        }

    }


    public class RszVectorComponent : INotifyPropertyChanged
    {
        private RszNodeViewModel _parent;
        private int _index;
        private string _value = "";
        public string ComponentValue { get => _value; set { if (_value != value) { _value = value; OnPropertyChanged(nameof(ComponentValue)); _parent.UpdateVectorFromComponents(); } } }
        public RszVectorComponent(RszNodeViewModel p, int i) { _parent = p; _index = i; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RszPropertyTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DefaultTemplate { get; set; }
        public DataTemplate? BoolTemplate { get; set; }
        public DataTemplate? NumericTemplate { get; set; }
        public DataTemplate? VectorTemplate { get; set; }
        public DataTemplate? GuidTemplate { get; set; }
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? ResourceTemplate { get; set; }
        public DataTemplate? ObjectTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is RszNodeViewModel vm)
            {
                if (vm.Children.Count > 0 && !vm.IsVector && !vm.IsResource && !vm.IsGuid) return ObjectTemplate;
                if (vm.IsBool) return BoolTemplate;
                if (vm.IsVector) return VectorTemplate;
                if (vm.IsGuid) return GuidTemplate;
                if (vm.IsString) return StringTemplate;
                if (vm.IsResource) return ResourceTemplate;
                if (vm.IsNumeric) return NumericTemplate;
            }
            return DefaultTemplate;
        }
    }


    public class DiffItem
    {
        public string ComponentName { get; set; } = "";
        public string FieldName { get; set; } = "";
        public string ValueA { get; set; } = "";
        public string ValueB { get; set; } = "";
        public bool IsNameMatch { get; set; }
        public bool IsValueAMatch { get; set; }
        public bool IsValueBMatch { get; set; }
    }
}
