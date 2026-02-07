using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;

namespace RszViewer
{
    public class SearchManager
    {
        public enum SearchType
        {
            PropertyName,
            PropertyValue,
            Both
        }

        public enum SearchDataType
        {
            Text,
            Integer,
            Float,
            Hex,
            Guid
        }

        public class SearchResult
        {
            public RszNodeViewModel? Node { get; set; }
            public string ComponentName { get; set; } = "";
            public string PropertyName { get; set; } = "";
            public string Value { get; set; } = "";
            public string Path { get; set; } = "";
            public bool IsMatch { get; set; }
        }

        public string Query { get; set; } = "";
        public SearchType Type { get; set; } = SearchType.Both;
        public SearchDataType DataType { get; set; } = SearchDataType.Text;
        public bool MatchCase { get; set; }
        public bool UseRegex { get; set; }
        public bool ShowDifferencesOnly { get; set; }
        
        // Advanced Search Params
        public long? MinInt { get; set; }
        public long? MaxInt { get; set; }
        public double? MinFloat { get; set; }
        public double? MaxFloat { get; set; }
        public byte[]? HexPattern { get; set; }
        public Guid? GuidTarget { get; set; }

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
            if (node.Node is RszObjectNode objNode)
            {
                for (int i = 0; i < objNode.Type.Fields.Length; i++)
                {
                    var field = objNode.Type.Fields[i];
                    var val = objNode.Children[i];
                    
                    bool match = false;

                    // 1. Check Property Name (Text only)
                    if (DataType == SearchDataType.Text && (Type == SearchType.PropertyName || Type == SearchType.Both))
                    {
                        if (IsTextMatch(field.Name)) match = true;
                    }

                    // 2. Check Property Value
                    if (!match && (Type == SearchType.PropertyValue || Type == SearchType.Both))
                    {
                        if (val is RszValueNode vn)
                        {
                            match = IsValueMatch(vn);
                        }
                        else if (val != null && DataType == SearchDataType.Text)
                        {
                            // Fallback for non-value nodes in text search
                            match = IsTextMatch(val.ToString() ?? "");
                        }
                    }

                    if (match)
                    {
                        node.MatchCount++;
                        nodeMatch = true;
                    }
                }
            }

            // Also check node name itself as a property name (Text only)
            if (DataType == SearchDataType.Text && IsTextMatch(node.Name))
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

        private bool IsValueMatch(RszValueNode node)
        {
            switch (DataType)
            {
                case SearchDataType.Integer:
                    {
                        // Try to interpret bytes as integer
                        // RszValueNode holds primitive data. 
                        // We need to check the field type if possible, but here we just have the node.
                        // We can try to read it as int64.
                        if (node.Data.Length <= 8)
                        {
                            try
                            {
                                long val = 0;
                                // Simple extraction - assumed Little Endian
                                var span = node.Data.Span;
                                if (span.Length == 1) val = span[0];
                                else if (span.Length == 2) val = BitConverter.ToInt16(span);
                                else if (span.Length == 4) val = BitConverter.ToInt32(span);
                                else if (span.Length == 8) val = BitConverter.ToInt64(span);

                                bool minPass = MinInt == null || val >= MinInt;
                                bool maxPass = MaxInt == null || val <= MaxInt;
                                return minPass && maxPass;
                            }
                            catch { }
                        }
                        return false;
                    }

                case SearchDataType.Float:
                    {
                         if (node.Data.Length == 4)
                         {
                             float val = BitConverter.ToSingle(node.Data.Span);
                             bool minPass = MinFloat == null || val >= MinFloat;
                             bool maxPass = MaxFloat == null || val <= MaxFloat;
                             return minPass && maxPass;
                         }
                         if (node.Data.Length == 8)
                         {
                             double val = BitConverter.ToDouble(node.Data.Span);
                             bool minPass = MinFloat == null || val >= MinFloat;
                             bool maxPass = MaxFloat == null || val <= MaxFloat;
                             return minPass && maxPass;
                         }
                         return false;
                    }

                case SearchDataType.Hex:
                    {
                        if (HexPattern == null || HexPattern.Length == 0) return false;
                        var data = node.Data.Span;
                        return data.IndexOf(HexPattern) >= 0;
                    }

                case SearchDataType.Guid:
                    {
                        if (GuidTarget == null) return false;
                        if (node.Data.Length == 16)
                        {
                            return new Guid(node.Data.Span) == GuidTarget.Value;
                        }
                        return false;
                    }

                case SearchDataType.Text:
                default:
                    return IsTextMatch(node.ToString() ?? "");
            }
        }

        public bool IsTextMatch(string text)
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
