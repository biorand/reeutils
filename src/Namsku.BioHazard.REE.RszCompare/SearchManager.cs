using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;

namespace ReeCompare
{
    public class SearchManager
    {
        public enum SearchType
        {
            PropertyName,
            PropertyValue,
            Both
        }

        public class SearchResult
        {
            public RszNodeViewModel Node { get; set; }
            public string ComponentName { get; set; } = "";
            public string PropertyName { get; set; } = "";
            public string Value { get; set; } = "";
            public string Path { get; set; } = "";
            public bool IsMatch { get; set; }
        }

        public string Query { get; set; } = "";
        public SearchType Type { get; set; } = SearchType.Both;
        public bool MatchCase { get; set; }
        public bool UseRegex { get; set; }
        public bool ShowDifferencesOnly { get; set; }

        public List<SearchResult> Matches { get; private set; } = new List<SearchResult>();
        private Regex? _cachedRegex;

        public void ExecuteSearch(IEnumerable<RszNodeViewModel> rootNodes)
        {
            Matches.Clear();
            if (string.IsNullOrWhiteSpace(Query)) 
            {
                _cachedRegex = null;
                ClearMatches(rootNodes);
                return;
            }

            _cachedRegex = null;
            if (UseRegex)
            {
                try
                {
                    _cachedRegex = new Regex(Query, MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                }
                catch
                {
                    // Invalid regex, handle gracefully
                    return;
                }
            }

            foreach (var node in rootNodes)
            {
                SearchInNode(node, "");
            }
        }

        private void ClearMatches(IEnumerable<RszNodeViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsSearchMatch = false;
                node.HasMatchInChildren = false;
                node.MatchCount = 0;
                ClearMatches(node.Children);
            }
        }

        private void SearchInNode(RszNodeViewModel node, string path)
        {
            bool nodeMatch = false;
            string currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path} > {node.Name}";

            // Reset node search state
            node.IsSearchMatch = false;
            node.MatchCount = 0;

            // Search in properties if it's an object node
            if (node.Node != null)
            {
                for (int i = 0; i < node.Node.Type.Fields.Length; i++)
                {
                    var field = node.Node.Type.Fields[i];
                    var val = node.Node.Children[i];
                    
                    bool nameMatch = IsMatch(field.Name);
                    bool valueMatch = false;
                    string valString = val?.ToString() ?? "null";

                    if (val is RszValueNode)
                    {
                        valueMatch = IsMatch(valString);
                    }

                    bool finalMatch = false;
                    if (Type == SearchType.PropertyName && nameMatch) finalMatch = true;
                    else if (Type == SearchType.PropertyValue && valueMatch) finalMatch = true;
                    else if (Type == SearchType.Both && (nameMatch || valueMatch)) finalMatch = true;

                    if (finalMatch)
                    {
                        node.MatchCount++;
                        nodeMatch = true;
                    }
                }
            }

            // Also check node name itself as a property name
            if (IsMatch(node.Name))
            {
               nodeMatch = true;
               node.IsSearchMatch = true;
            }

            if (nodeMatch)
            {
                node.IsSearchMatch = true;
                Matches.Add(new SearchResult 
                { 
                    Node = node, 
                    ComponentName = node.Name,
                    Path = currentPath,
                    IsMatch = true
                });
            }

            // Recurse
            foreach (var child in node.Children)
            {
                SearchInNode(child, currentPath);
                if (child.IsSearchMatch || child.HasMatchInChildren)
                {
                    node.HasMatchInChildren = true;
                }
            }
        }

        public bool IsMatch(string text)
        {
            if (string.IsNullOrEmpty(Query)) return false;

            if (_cachedRegex != null)
            {
                return _cachedRegex.IsMatch(text);
            }
            
            if (MatchCase)
            {
                return text.Contains(Query);
            }
            else
            {
                return text.IndexOf(Query, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
