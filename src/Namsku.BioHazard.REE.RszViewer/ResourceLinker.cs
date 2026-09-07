using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Rsz;

namespace RszViewer
{
    public class ResourceLinker
    {
        private readonly RszTypeRepository _repo;
        private string _targetFolder = "";
        
        // Search State
        private HashSet<Guid> _processedObjectGuids = new HashSet<Guid>();

        public event Action<string>? OnFileProcessed;
        public event Action<string>? OnStatusUpdate;

        public ResourceLinker(RszTypeRepository repo)
        {
            _repo = repo;
        }

        public List<LinkResult> TraceDependencies(string folderPath, RszGameObject sourceObject)
        {
            _targetFolder = folderPath;
            _processedObjectGuids.Clear();
            _processedObjectGuids.Add(sourceObject.Guid);

            var guidIndex = new Dictionary<Guid, List<MatchInfo>>();
            var stringIndex = new Dictionary<string, List<MatchInfo>>();

            // Phase 1: Index all files in the folder
            var files = Directory.GetFiles(_targetFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => (f.Contains(".user.") || f.Contains(".scn.") || f.Contains(".pfb.")))
                .ToList();

            OnStatusUpdate?.Invoke($"Indexing {files.Count} files...");
            int fileCount = 0;
            foreach (var file in files)
            {
                fileCount++;
                if (fileCount % 10 == 0) OnStatusUpdate?.Invoke($"Indexing files {fileCount}/{files.Count}...");
                OnFileProcessed?.Invoke(file);
                IndexFile(file, guidIndex, stringIndex);
            }

            // Phase 2: Hierarchical Trace
            OnStatusUpdate?.Invoke("Building dependency tree...");
            var rootResults = new List<LinkResult>();
            
            BuildHierarchyRecursively(rootResults, sourceObject, guidIndex, stringIndex);

            OnStatusUpdate?.Invoke("Trace Complete.");
            return rootResults;
        }

        private void BuildHierarchyRecursively(List<LinkResult> container, RszGameObject parent, 
            Dictionary<Guid, List<MatchInfo>> guidIndex, Dictionary<string, List<MatchInfo>> stringIndex)
        {
            var foundMatches = new List<LinkResult>();

            // 1. OUTGOING: Who does this object reference? (Dependencies)
            var outgoingGuids = ExtractGuidsFromNode(parent);
            foreach (var g in outgoingGuids)
            {
                // Prevent self-reference if the object contains its own GUID in its data
                if (g == parent.Guid) continue;

                if (guidIndex.TryGetValue(g, out var matches))
                {
                    foreach (var m in matches)
                    {
                        if (m.IsIdentity && m.MatchedObject != null && !_processedObjectGuids.Contains(m.MatchedObject.Guid))
                        {
                            _processedObjectGuids.Add(m.MatchedObject.Guid);
                            foundMatches.Add(new LinkResult(m.FilePath, m.MatchedObject, $"[Outgoing] Found via GUID {g:N}", true));
                        }
                    }
                }
            }

            var outgoingStrings = ExtractStringsFromNode(parent);
            foreach (var s in outgoingStrings)
            {
                // Prevent self-reference if the object contains its own name in its data
                if (s == parent.Name) continue;

                if (stringIndex.TryGetValue(s, out var matches))
                {
                    foreach (var m in matches)
                    {
                        if (m.IsIdentity && m.MatchedObject != null && !_processedObjectGuids.Contains(m.MatchedObject.Guid))
                        {
                            _processedObjectGuids.Add(m.MatchedObject.Guid);
                            foundMatches.Add(new LinkResult(m.FilePath, m.MatchedObject, $"[Outgoing] Found via String '{s}'", true));
                        }
                    }
                }
            }

            // 2. INCOMING: Who references this object? (Usages)
            // 2a. Direct GUID match
            if (guidIndex.TryGetValue(parent.Guid, out var incomingGuidMatches))
            {
                foreach (var m in incomingGuidMatches)
                {
                    if (!m.IsIdentity && m.MatchedObject != null && !_processedObjectGuids.Contains(m.MatchedObject.Guid))
                    {
                        _processedObjectGuids.Add(m.MatchedObject.Guid);
                        foundMatches.Add(new LinkResult(m.FilePath, m.MatchedObject, $"[Incoming] Object references parent's GUID", true));
                    }
                }
            }

            // 2b. GUID-as-String match (e.g., path containing GUID or string field with GUID)
            string guidString = parent.Guid.ToString();
            if (stringIndex.TryGetValue(guidString, out var incomingGuidStringMatches))
            {
                foreach (var m in incomingGuidStringMatches)
                {
                    if (!m.IsIdentity && m.MatchedObject != null && !_processedObjectGuids.Contains(m.MatchedObject.Guid))
                    {
                        _processedObjectGuids.Add(m.MatchedObject.Guid);
                        foundMatches.Add(new LinkResult(m.FilePath, m.MatchedObject, $"[Incoming] Object references parent's GUID (as string)", true));
                    }
                }
            }

            // 2c. Name match
            if (IsInterestingString(parent.Name))
            {
                if (stringIndex.TryGetValue(parent.Name!, out var incomingStringMatches))
                {
                    foreach (var m in incomingStringMatches)
                    {
                        if (!m.IsIdentity && m.MatchedObject != null && !_processedObjectGuids.Contains(m.MatchedObject.Guid))
                        {
                            _processedObjectGuids.Add(m.MatchedObject.Guid);
                            foundMatches.Add(new LinkResult(m.FilePath, m.MatchedObject, $"[Incoming] Object references parent by name", true));
                        }
                    }
                }

                // If name is a GUID, check the guid index too
                if (Guid.TryParse(parent.Name, out var nameAsGuid))
                {
                    if (guidIndex.TryGetValue(nameAsGuid, out var incomingNameGuidMatches))
                    {
                        foreach (var m in incomingNameGuidMatches)
                        {
                            if (!m.IsIdentity && m.MatchedObject != null && !_processedObjectGuids.Contains(m.MatchedObject.Guid))
                            {
                                _processedObjectGuids.Add(m.MatchedObject.Guid);
                                foundMatches.Add(new LinkResult(m.FilePath, m.MatchedObject, $"[Incoming] Object references parent's name (which is a GUID)", true));
                            }
                        }
                    }
                }
            }

            // 3. Recurse for all found links
            foreach (var res in foundMatches)
            {
                container.Add(res);
                if (res.MatchedObject != null)
                {
                    BuildHierarchyRecursively(res.Children, res.MatchedObject, guidIndex, stringIndex);
                }
            }
        }

        private void IndexFile(string filePath, Dictionary<Guid, List<MatchInfo>> guidIndex, Dictionary<string, List<MatchInfo>> stringIndex)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                string fileName = Path.GetFileName(filePath).ToLower();
                
                if (fileName.Contains(".scn."))
                {
                    var scnFile = new ScnFile(GetVersion(fileName, ".scn."), data);
                    var scene = scnFile.ReadScene(_repo);
                    IndexNode(scene, filePath, guidIndex, stringIndex);
                }
                else if (fileName.Contains(".pfb."))
                {
                    var pfbFile = new PfbFile(GetVersion(fileName, ".pfb."), data);
                    var scene = pfbFile.ReadScene(_repo);
                    IndexNode(scene, filePath, guidIndex, stringIndex);
                }
                else if (fileName.Contains(".user."))
                {
                    var userFile = new UserFile(data);
                    foreach (var obj in userFile.GetObjects(_repo))
                    {
                        var guids = ExtractGuidsFromNode(obj);
                        foreach(var g in guids) AddToIndex(guidIndex, g, new MatchInfo { FilePath = filePath, MatchedObject = null, IsIdentity = false });
                        
                        var strings = ExtractStringsFromNode(obj);
                        foreach(var s in strings) AddToIndex(stringIndex, s, new MatchInfo { FilePath = filePath, MatchedObject = null, IsIdentity = false });
                    }
                }
            }
            catch { }
        }

        private void IndexNode(IRszSceneNode node, string filePath, Dictionary<Guid, List<MatchInfo>> guidIndex, Dictionary<string, List<MatchInfo>> stringIndex)
        {
            if (node is RszGameObject go)
            {
                // Identity Search (Who is this object?)
                AddToIndex(guidIndex, go.Guid, new MatchInfo { FilePath = filePath, MatchedObject = go, IsIdentity = true });
                if (IsInterestingString(go.Name))
                    AddToIndex(stringIndex, go.Name!, new MatchInfo { FilePath = filePath, MatchedObject = go, IsIdentity = true });

                // Content Search (What does this object reference?)
                var guids = ExtractGuidsFromNode(go);
                foreach(var g in guids) AddToIndex(guidIndex, g, new MatchInfo { FilePath = filePath, MatchedObject = go, IsIdentity = false });
                
                var strings = ExtractStringsFromNode(go);
                foreach(var s in strings) AddToIndex(stringIndex, s, new MatchInfo { FilePath = filePath, MatchedObject = go, IsIdentity = false });
            }

            foreach (var child in node.Children)
            {
                if (child is IRszSceneNode sceneChild)
                    IndexNode(sceneChild, filePath, guidIndex, stringIndex);
            }
        }

        private void AddToIndex<T>(Dictionary<T, List<MatchInfo>> index, T key, MatchInfo match) where T : notnull
        {
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<MatchInfo>();
                index[key] = list;
            }
            list.Add(match);
        }

        private class MatchInfo
        {
            public string FilePath { get; set; } = "";
            public RszGameObject? MatchedObject { get; set; }
            public bool IsIdentity { get; set; } = false;
        }

        private bool IsInterestingString(string? s)
        {
            return !string.IsNullOrWhiteSpace(s);
        }

        private HashSet<string> ExtractStringsFromNode(IRszNode? node)
        {
            var set = new HashSet<string>();
            if (node == null) return set;

            if (node is RszStringNode sn)
            {
                if (IsInterestingString(sn.Value)) set.Add(sn.Value);
            }
            else if (node is RszResourceNode rn)
            {
                if (IsInterestingString(rn.Value)) set.Add(rn.Value!);
            }
            else if (node is RszUserDataNode udn)
            {
                if (IsInterestingString(udn.Path)) set.Add(udn.Path);
            }
            else if (node is RszGameObject go)
            {
                 set.UnionWith(ExtractStringsFromNode(go.Settings));
                 foreach(var c in go.Components) set.UnionWith(ExtractStringsFromNode(c));
            }
            else if (node is RszObjectNode obj)
            {
                foreach(var child in obj.Children)
                {
                    set.UnionWith(ExtractStringsFromNode(child));
                }
            }
            else if (node is RszArrayNode arr)
            {
                foreach(var item in arr.Children)
                    set.UnionWith(ExtractStringsFromNode(item));
            }
            else if (node is IRszNodeContainer container)
            {
                 foreach(var child in container.Children)
                    set.UnionWith(ExtractStringsFromNode(child));
            }
            return set;
        }

        private HashSet<Guid> ExtractGuidsFromNode(IRszNode? node)
        {
            var set = new HashSet<Guid>();
            if (node == null) return set;

            if (node is RszValueNode vn)
            {
                if (vn.Type == RszFieldType.Guid || vn.Type == RszFieldType.Uri || vn.Type == RszFieldType.GameObjectRef)
                {
                    // 1. Try raw bytes (Fast path for standard GUIDs)
                    if (vn.Data.Length == 16)
                    {
                        var guid = new Guid(vn.Data.Span);
                        if (guid != Guid.Empty) set.Add(guid);
                    }
                    else
                    {
                        // 2. Try string parsing (Robust path for Uri/String-based GUIDs)
                        string val = vn.ToString() ?? "";
                        if (Guid.TryParse(val, out var g) && g != Guid.Empty) set.Add(g);
                    }
                }
            }
            else if (node is RszStringNode sn)
            {
                if (Guid.TryParse(sn.Value, out var g) && g != Guid.Empty) set.Add(g);
            }
            else if (node is RszResourceNode rn)
            {
                if (rn.Value != null && Guid.TryParse(rn.Value, out var g) && g != Guid.Empty) set.Add(g);
            }
            else if (node is RszUserDataNode udn)
            {
                if (Guid.TryParse(udn.Path, out var g) && g != Guid.Empty) set.Add(g);
            }
            else if (node is RszGameObject go)
            {
                set.Add(go.Guid); // Identity is also a signal
                set.UnionWith(ExtractGuidsFromNode(go.Settings));
                foreach(var c in go.Components) set.UnionWith(ExtractGuidsFromNode(c));
            }
            else if (node is RszObjectNode obj)
            {
                foreach(var child in obj.Children)
                {
                    set.UnionWith(ExtractGuidsFromNode(child));
                }
            }
            else if (node is RszArrayNode arr)
            {
                foreach (var item in arr.Children)
                    set.UnionWith(ExtractGuidsFromNode(item));
            }
            else if (node is IRszNodeContainer container)
            {
                foreach (var child in container.Children)
                    set.UnionWith(ExtractGuidsFromNode(child));
            }
            return set;
        }

        private int GetVersion(string fileName, string ext)
        {
            try
            {
                int index = fileName.LastIndexOf(ext);
                if (index != -1)
                {
                    string suffix = fileName.Substring(index + ext.Length);
                    if (int.TryParse(suffix, out int version)) return version;
                }
            }
            catch { }
            return 0;
        }
    }

    public class LinkResult
    {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public RszGameObject? MatchedObject { get; }
        public string Reason { get; }
        public bool IsConfirmedLink { get; }
        public List<LinkResult> Children { get; } = new List<LinkResult>();

        // Properties for XAML binding
        public string LinkType => IsConfirmedLink ? "Linked" : "Potentially Linked";
        public string ObjectName => MatchedObject?.Name ?? "[Generic Match]";

        public LinkResult(string filePath, RszGameObject? matchedObject, string reason, bool isConfirmed)
        {
            FilePath = filePath;
            MatchedObject = matchedObject;
            Reason = reason;
            IsConfirmedLink = isConfirmed;
        }
    }
}
