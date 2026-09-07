using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IntelOrca.Biohazard.REE.Rsz;
using Microsoft.Win32;

namespace ReeCompare
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }
        private RszTypeRepository? _repo;
        private SearchManager _searchManager = new SearchManager();
        private AppConfig _config = AppConfig.Load();
        private bool _suppressGameChange;

        public string SearchQuery { get; set; } = "";
        private int _matchIndex = -1;
        private List<RszNodeViewModel> _currentMatches = new List<RszNodeViewModel>();

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitGamePicker();
            TryLoadGameRepo(showErrors: false);
            UpdateStatus();

            if (_config.LeftFilePath != null && File.Exists(_config.LeftFilePath))
                LoadFile(true, _config.LeftFilePath);

            if (_config.RightFilePath != null && File.Exists(_config.RightFilePath))
                LoadFile(false, _config.RightFilePath);

            ListHistory.ItemsSource = _config.SearchHistory;
            ListRecent.ItemsSource = _config.RecentFiles;
        }

        private void InitGamePicker()
        {
            _suppressGameChange = true;
            try
            {
                var items = GameCatalog.Games
                    .Select(g => new GameEntry(g.Id, g.DisplayName))
                    .ToList();
                items.Add(new GameEntry(GameCatalog.CustomId, "Custom..."));
                CmbGame.ItemsSource = items;
                CmbGame.DisplayMemberPath = "DisplayName";
                CmbGame.SelectedValuePath = "Id";

                var gameId = NormalizeGameId(_config.GameId);
                CmbGame.SelectedValue = gameId;
                // If the id was legacy/unknown, fall back to re4 visually but keep custom file if set.
                if (CmbGame.SelectedItem == null)
                {
                    CmbGame.SelectedValue = string.Equals(_config.GameId, GameCatalog.CustomId, StringComparison.OrdinalIgnoreCase)
                        ? GameCatalog.CustomId
                        : "re4";
                }
            }
            finally
            {
                _suppressGameChange = false;
            }
        }

        private static string NormalizeGameId(string? gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
                return "re4";
            gameId = gameId.Trim().ToLowerInvariant();
            if (gameId == GameCatalog.CustomId)
                return gameId;
            return GameCatalog.IsKnownGame(gameId) ? gameId : "re4";
        }

        private void CmbGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGameChange || !IsLoaded)
                return;
            if (CmbGame.SelectedItem is not GameEntry entry)
                return;

            if (string.Equals(entry.Id, GameCatalog.CustomId, StringComparison.OrdinalIgnoreCase))
            {
                // Picking Custom prompts for a file; on cancel revert to current game.
                var previous = NormalizeGameId(_config.GameId == GameCatalog.CustomId ? "re4" : _config.GameId);
                LoadRsz_Click(this, new RoutedEventArgs());
                _suppressGameChange = true;
                try
                {
                    CmbGame.SelectedValue = _config.GameId == GameCatalog.CustomId
                        ? GameCatalog.CustomId
                        : previous;
                }
                finally
                {
                    _suppressGameChange = false;
                }
                return;
            }

            _config.GameId = entry.Id;
            if (TryLoadGameRepo(showErrors: true))
            {
                _config.Save();
                UpdateStatus();
                ReparseLoadedFiles();
            }
            else
            {
                UpdateStatus();
            }
        }

        private bool TryLoadGameRepo(bool showErrors)
        {
            try
            {
                if (string.Equals(_config.GameId, GameCatalog.CustomId, StringComparison.OrdinalIgnoreCase))
                {
                    if (_config.RszRepoPath != null && File.Exists(_config.RszRepoPath))
                        _repo = RszRepositorySerializer.Default.FromJsonFile(_config.RszRepoPath);
                    else
                        _repo = null;
                }
                else
                {
                    var gameId = NormalizeGameId(_config.GameId);
                    _config.GameId = gameId;
                    _repo = GameCatalog.LoadEmbedded(gameId);
                }
                return _repo != null;
            }
            catch (Exception ex)
            {
                _repo = null;
                if (showErrors)
                    MessageBox.Show("Error loading RSZ definitions: " + ex.Message);
                return false;
            }
        }

        private void UpdateStatus()
        {
            if (TxtStatus == null)
                return;
            if (_repo == null)
            {
                TxtStatus.Text = "No RSZ — pick a game above";
                return;
            }
            if (string.Equals(_config.GameId, GameCatalog.CustomId, StringComparison.OrdinalIgnoreCase))
                TxtStatus.Text = $"Custom RSZ | {Path.GetFileName(_config.RszRepoPath)}";
            else
                TxtStatus.Text = $"{GameCatalog.DisplayNameFor(_config.GameId)} | embedded RSZ";
        }

        private void ReparseLoadedFiles()
        {
            // LoadFile only overwrites the tree on success, so a failed re-parse keeps the old view.
            if (_config.LeftFilePath != null && File.Exists(_config.LeftFilePath))
                LoadFile(true, _config.LeftFilePath);
            if (_config.RightFilePath != null && File.Exists(_config.RightFilePath))
                LoadFile(false, _config.RightFilePath);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _config.Save();
            Close();
        }

        private async void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SearchQuery = TxtSearch.Text;
            await Task.Delay(300); // Simple debounce
            if (SearchQuery != TxtSearch.Text) return;
            PerformSearch();
        }

        private void SearchOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            PerformSearch();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMatches.Count == 0) return;
            _matchIndex--;
            if (_matchIndex < 0) _matchIndex = _currentMatches.Count - 1;
            NavigateToMatch();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMatches.Count == 0) return;
            _matchIndex++;
            if (_matchIndex >= _currentMatches.Count) _matchIndex = 0;
            NavigateToMatch();
        }

        private void NavigateToMatch()
        {
            if (_matchIndex < 0 || _matchIndex >= _currentMatches.Count) return;
            
            var match = _currentMatches[_matchIndex];
            TxtMatchStatus.Text = $"{_matchIndex + 1}/{_currentMatches.Count}";

            // Find which tree contains the node
            if (IsInTree(TreeA.ItemsSource as IEnumerable<RszNodeViewModel>, match))
                SelectAndScroll(TreeA, match);
            else if (IsInTree(TreeB.ItemsSource as IEnumerable<RszNodeViewModel>, match))
                SelectAndScroll(TreeB, match);
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

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            PopHistory.IsOpen = !PopHistory.IsOpen;
        }

        private void PopHistory_Closed(object sender, EventArgs e)
        {
            BtnHistory.IsChecked = false;
        }

        private void BtnRecent_Click(object sender, RoutedEventArgs e)
        {
            PopRecent.IsOpen = !PopRecent.IsOpen;
        }

        private void ListRecent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListRecent.SelectedItem is FileHistoryItem item)
            {
                 PopRecent.IsOpen = false;
                 BtnRecent.IsChecked = false;
                 
                 // Prevent re-triggering logic if just setting null
                 if(item == null) return;

                 if (File.Exists(item.LeftPath)) LoadFile(true, item.LeftPath);
                 if (File.Exists(item.RightPath)) LoadFile(false, item.RightPath);
                 
                 // Clear selection so we can re-select same item if needed
                 ListRecent.SelectedItem = null;
            }
        }

        private void BtnSaveSearch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;
            if (!_config.SearchHistory.Contains(SearchQuery))
            {
                _config.SearchHistory.Insert(0, SearchQuery);
                if (_config.SearchHistory.Count > 20) _config.SearchHistory.RemoveAt(20);
                _config.Save();
                ListHistory.ItemsSource = null;
                ListHistory.ItemsSource = _config.SearchHistory;
            }
        }

        private void ListHistory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListHistory.SelectedItem is string query)
            {
                TxtSearch.Text = query;
                PopHistory.IsOpen = false;
                BtnHistory.IsChecked = false;
            }
        }

        private void PerformSearch()
        {
            if (_repo == null) return;

            _searchManager.Query = SearchQuery;
            _searchManager.MatchCase = (SearchChkMatchCase as System.Windows.Controls.CheckBox)?.IsChecked ?? false;
            _searchManager.UseRegex = (SearchChkRegex as System.Windows.Controls.CheckBox)?.IsChecked ?? false;
            _searchManager.ShowDifferencesOnly = (SearchChkDiffOnly as System.Windows.Controls.CheckBox)?.IsChecked ?? false;
            
            if ((SearchRadPropName as System.Windows.Controls.RadioButton)?.IsChecked == true) _searchManager.Type = SearchManager.SearchType.PropertyName;
            else if ((SearchRadPropValue as System.Windows.Controls.RadioButton)?.IsChecked == true) _searchManager.Type = SearchManager.SearchType.PropertyValue;
            else _searchManager.Type = SearchManager.SearchType.Both;

            if (TreeA.ItemsSource is IEnumerable<RszNodeViewModel> nodesA)
                _searchManager.ExecuteSearch(nodesA);
            
            if (TreeB.ItemsSource is IEnumerable<RszNodeViewModel> nodesB)
                _searchManager.ExecuteSearch(nodesB);

            _currentMatches = _searchManager.Matches.Select(m => m.Node).ToList();
            _matchIndex = _currentMatches.Count > 0 ? 0 : -1;
            TxtMatchStatus.Text = _currentMatches.Count > 0 ? $"1/{_currentMatches.Count}" : "0/0";

            RefreshTreeVisibility(TreeA.ItemsSource as IEnumerable<RszNodeViewModel>);
            RefreshTreeVisibility(TreeB.ItemsSource as IEnumerable<RszNodeViewModel>);
            
            if (_currentMatches.Count > 0) NavigateToMatch();
        }

        private void RefreshTreeVisibility(IEnumerable<RszNodeViewModel>? nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                node.OnPropertyChanged(nameof(RszNodeViewModel.NodeVisibility));
                
                if (node.HasMatchInChildren || node.IsSearchMatch)
                {
                    node.IsExpanded = true;
                }

                RefreshTreeVisibility(node.Children);
            }
        }

        private void LoadRsz_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "RSZ JSON|*.json;*.json.gz|All files|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var path = dlg.FileName;
                    _repo = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                        ? RszRepositorySerializer.Default.FromJsonGz(File.ReadAllBytes(path))
                        : RszRepositorySerializer.Default.FromJsonFile(path);
                    _config.GameId = GameCatalog.CustomId;
                    _config.RszRepoPath = path;
                    _config.Save();
                    _suppressGameChange = true;
                    try { CmbGame.SelectedValue = GameCatalog.CustomId; }
                    finally { _suppressGameChange = false; }
                    UpdateStatus();
                    MessageBox.Show("Custom RSZ Definitions loaded.");
                    ReparseLoadedFiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading RSZ: " + ex.Message);
                }
            }
        }

        private void BtnLoadA_Click(object sender, RoutedEventArgs e)
        {
            LoadFileWithDialog(true);
        }

        private void BtnLoadB_Click(object sender, RoutedEventArgs e)
        {
            LoadFileWithDialog(false);
        }

        private void FileA_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadFile(true, files[0]);
                }
            }
        }

        private void FileB_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadFile(false, files[0]);
                }
            }
        }

        private void LoadFileWithDialog(bool isA)
        {
            if (!EnsureRszLoaded()) return;

            var dlg = new OpenFileDialog { Filter = "RE Engine files|*.user.*;*.scn.*;*.pfb.*|User files|*.user.*|Scene files|*.scn.*|Prefab files|*.pfb.*|All files|*.*" };
            if (dlg.ShowDialog() == true)
            {
                LoadFile(isA, dlg.FileName);
            }
        }

        private bool EnsureRszLoaded()
        {
            if (_repo == null)
            {
                // Auto-retry the selected game once (e.g. first run before Loaded finished).
                TryLoadGameRepo(showErrors: false);
                UpdateStatus();
            }
            if (_repo == null)
            {
                 var mbRes = MessageBox.Show("RSZ Definitions not loaded. Pick a game in the toolbar (uses embedded RSZ), or load a custom JSON now?", "Missing RSZ", MessageBoxButton.YesNo);
                if (mbRes == MessageBoxResult.Yes)
                {
                    LoadRsz_Click(this, new RoutedEventArgs());
                    return _repo != null;
                }
                return false;
            }
            return true;
        }

        private static bool TryGetScnOrPfbVersion(string filePath, out int version)
        {
            // Matches ".scn.20", ".scn.21", ".pfb.18", etc. (trailing number after last dot).
            version = 0;
            var name = Path.GetFileName(filePath);
            var lastDot = name.LastIndexOf('.');
            if (lastDot < 0 || !int.TryParse(name.Substring(lastDot + 1), out version))
                return false;
            var lower = name.ToLowerInvariant();
            return lower.Contains(".scn.") || lower.Contains(".pfb.");
        }

        private void LoadFile(bool isA, string filePath)
        {
            if (!EnsureRszLoaded()) return;

            try
            {
                IList<RszNodeViewModel> viewModels;
                var lower = filePath.ToLowerInvariant();

                if (lower.Contains(".scn."))
                {
                    if (!TryGetScnOrPfbVersion(filePath, out var version))
                        throw new InvalidDataException($"Could not parse scn version from '{Path.GetFileName(filePath)}'.");
                    var data = File.ReadAllBytes(filePath);
                    var scnFile = new ScnFile(version, data);
                    var scene = scnFile.ReadScene(_repo!);

                    viewModels = scene.Children.Select(n => new RszNodeViewModel(n)).ToList();
                }
                else if (lower.Contains(".pfb."))
                {
                    if (!TryGetScnOrPfbVersion(filePath, out var version))
                        throw new InvalidDataException($"Could not parse pfb version from '{Path.GetFileName(filePath)}'.");
                    var data = File.ReadAllBytes(filePath);
                    var pfbFile = new PfbFile(version, data);
                    var scene = pfbFile.ReadScene(_repo!);

                    viewModels = scene.Children.Select(n => new RszNodeViewModel(n)).ToList();
                }
                else
                {
                    var data = File.ReadAllBytes(filePath);
                    var userFile = new UserFile(data);

                    var builder = userFile.ToBuilder(_repo!);
                    var rootNodes = builder.Objects;

                    viewModels = rootNodes.Select((n, i) => new RszNodeViewModel(n, $"Object {i}")).ToList();
                }

                if (isA)
                {
                    TreeA.ItemsSource = viewModels;
                    TxtFileA.Text = filePath;
                    _config.LeftFilePath = filePath;
                }
                else
                {
                    TreeB.ItemsSource = viewModels;
                    TxtFileB.Text = filePath;
                    _config.RightFilePath = filePath;
                }
                
                AddToHistory();
                _config.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading file: " + ex.ToString());
            }
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
                _config.RecentFiles.Insert(0, new FileHistoryItem 
                { 
                    LeftPath = _config.LeftFilePath, 
                    RightPath = _config.RightFilePath,
                    LastAccessed = DateTime.Now 
                });
            }

            if (_config.RecentFiles.Count > 10) _config.RecentFiles.RemoveAt(_config.RecentFiles.Count - 1);
            
            // Refresh list
            ListRecent.ItemsSource = null;
            ListRecent.ItemsSource = _config.RecentFiles;
        }

        private void TreeA_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TryCompare();
        }

        private void TreeB_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TryCompare();
        }

        private void TryCompare()
        {
            try
            {
                var itemA = TreeA.SelectedItem as RszNodeViewModel;
                var itemB = TreeB.SelectedItem as RszNodeViewModel;

                if (itemA != null && itemB != null)
                {
                    var diffs = new List<DiffItem>();
                    
                    if (itemA.GameObject != null && itemB.GameObject != null)
                    {
                        diffs = CompareGameObjects(itemA.GameObject, itemB.GameObject);
                    }
                    else if (itemA.Node != null && itemB.Node != null)
                    {
                        diffs = CompareNodes(itemA.Node, itemB.Node);
                    }
                    
                    GridDiff.ItemsSource = diffs;
                }
                else
                {
                    GridDiff.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error comparing objects: " + ex.ToString());
            }
        }

        private List<DiffItem> CompareGameObjects(RszGameObject goA, RszGameObject goB)
        {
             var diffs = new List<DiffItem>();
             var compsA = goA.Components;
             var compsB = goB.Components;

             foreach(var compA in compsA)
             {
                 var compB = compsB.FirstOrDefault(c => c.Type.Name == compA.Type.Name);
                 if (compB != null)
                 {
                     CompareValues(diffs, compA.Type.Name, "", compA, compB);
                 }
                 else
                 {
                     diffs.Add(new DiffItem { ComponentName = "GameObject", FieldName = "Simple Component", ValueA = compA.Type.Name, ValueB = "<Missing>" });
                 }
             }

             foreach(var compB in compsB)
             {
                 if(compsA.All(c => c.Type.Name != compB.Type.Name))
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
                var field = nodeA.Type.Fields[i];
                var valA = nodeA.Children[i];
                var valB = nodeB.Children[i];

                CompareValues(diffs, nodeA.Type.Name, field.Name, valA, valB);
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
                foreach(var d in subDiffs)
                {
                    d.FieldName = string.IsNullOrEmpty(fieldName) ? d.FieldName : $"{fieldName}.{d.FieldName}";
                    if(d.ComponentName == "Object" || d.ComponentName == oa.Type.Name) d.ComponentName = componentName;
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
                if(sA != sB)
                {
                     var item = new DiffItem { ComponentName = componentName, FieldName = fieldName, ValueA = sA, ValueB = sB };
                     CheckMatches(item);
                     diffs.Add(item);
                }
            }
        }

        private string FormatRszValue(RszValueNode node)
        {
            var s = node.ToString() ?? "";
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
                if (_searchManager.IsMatch(item.FieldName)) item.IsNameMatch = true;
            }

            if (_searchManager.Type == SearchManager.SearchType.PropertyValue || _searchManager.Type == SearchManager.SearchType.Both)
            {
                if (_searchManager.IsMatch(item.ValueA)) item.IsValueAMatch = true;
                if (_searchManager.IsMatch(item.ValueB)) item.IsValueBMatch = true;
            }
        }
    }

    public class RszNodeViewModel : INotifyPropertyChanged
    {
         public string Name { get; }
         public RszObjectNode? Node { get; }
         public RszGameObject? GameObject { get; }
         public ObservableCollection<RszNodeViewModel> Children { get; }

         private bool _isSearchMatch;
         public bool IsSearchMatch
         {
             get => _isSearchMatch;
             set { _isSearchMatch = value; OnPropertyChanged(nameof(IsSearchMatch)); OnPropertyChanged(nameof(NodeVisibility)); }
         }

         private bool _hasMatchInChildren;
         public bool HasMatchInChildren
         {
             get => _hasMatchInChildren;
             set { _hasMatchInChildren = value; OnPropertyChanged(nameof(HasMatchInChildren)); OnPropertyChanged(nameof(NodeVisibility)); }
         }

         private int _matchCount;
         public int MatchCount
         {
             get => _matchCount;
             set { _matchCount = value; OnPropertyChanged(nameof(MatchCount)); }
         }

         private bool _isExpanded;
         public bool IsExpanded
         {
             get => _isExpanded;
             set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
         }

         private bool _isSelected;
         public bool IsSelected
         {
             get => _isSelected;
             set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
         }

         public Visibility NodeVisibility => (IsSearchMatch || HasMatchInChildren || MatchCount > 0 || string.IsNullOrEmpty(MainWindow.Instance?.SearchQuery)) ? Visibility.Visible : Visibility.Collapsed;

         public event PropertyChangedEventHandler? PropertyChanged;
         public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

         public RszNodeViewModel(RszObjectNode? node, string name)
         {
             Node = node;
             Children = new ObservableCollection<RszNodeViewModel>();

             if (node == null)
             {
                 Name = name;
                 return;
             }

             Name = $"{name} : {node.Type.Name}";
             
             for(int i=0; i<node.Type.Fields.Length; i++)
             {
                if (i >= node.Children.Length)
                {
                    Children.Add(new RszNodeViewModel(null, $"<Missing Field Data for {node.Type.Name}>"));
                    break;
                }

                var field = node.Type.Fields[i];
                var val = node.Children[i];

                if(val is RszObjectNode childObj)
                {
                    Children.Add(new RszNodeViewModel(childObj, field.Name));
                }
                else if (val is RszArrayNode arr)
                {
                    for(int j=0; j<arr.Children.Length; j++)
                    {
                        var childVal = arr.Children[j];
                        
                        if(childVal is RszObjectNode arrChild)
                        {
                            Children.Add(new RszNodeViewModel(arrChild, $"{field.Name}[{j}]"));
                        }
                    }
                }
             }
         }

         public RszNodeViewModel(RszGameObject gameObject)
         {
             GameObject = gameObject;
             Name = gameObject.Name;
             Children = new ObservableCollection<RszNodeViewModel>();

             foreach(var component in gameObject.Components)
             {
                 Children.Add(new RszNodeViewModel(component, component.Type.Name));
             }

             foreach(var child in gameObject.Children)
             {
                 Children.Add(new RszNodeViewModel(child));
             }
         }

          public RszNodeViewModel(IRszSceneNode sceneNode)
         {
             Children = new ObservableCollection<RszNodeViewModel>();
             if (sceneNode is RszGameObject go)
             {
                 GameObject = go;
                 Name = go.Name;
                 foreach(var component in go.Components)
                     Children.Add(new RszNodeViewModel(component, component.Type.Name));
                 foreach(var child in go.Children)
                     Children.Add(new RszNodeViewModel(child));
             }
             else if (sceneNode is RszFolder folder)
             {
                 Name = folder.Name;
                 foreach(var child in folder.Children)
                     Children.Add(new RszNodeViewModel(child));
             }
             else
             {
                 Name = "Unknown Node";
             }
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
