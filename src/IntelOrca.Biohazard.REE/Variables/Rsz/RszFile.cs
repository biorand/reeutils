using IntelOrca.Biohazard.REE.Variables.Pfb;
using System;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    public enum HeaderType
    {
        Usr,
        Pfb,
        Pfb16,
        Scn18,
        Scn19,
        ScnBase
    }

    internal class RszFile
    {
        public byte[] FullData { get; set; } = Array.Empty<byte>();
        public object Header { get; set; }
        public List<RszGameObject> GameObjects { get; } = new List<RszGameObject>();
        public List<RszFolderInfo> FolderInfos { get; } = new List<RszFolderInfo>();
        public List<RszResourceInfo> ResourceInfos { get; } = new List<RszResourceInfo>();
        public List<RszPrefabInfo> PrefabInfos { get; } = new List<RszPrefabInfo>();
        public List<RszUserDataInfo> UserDataInfos { get; } = new List<RszUserDataInfo>(); // from main header
        public List<GameObjectRefInfo> GameObjectRefInfos { get; } = new List<GameObjectRefInfo>(); // for PFB files
        public List<RszUserDataInfo> RszUserDataInfos { get; } = new List<RszUserDataInfo>(); // from RSZ section

        public byte[] ResourceBlock { get; set; } = Array.Empty<byte>();
        public byte[] PrefabBlock { get; set; } = Array.Empty<byte>();
        public int PrefabBlockStart { get; set; }
        public RszHeader RszHeader { get; set; }
        public List<object> ObjectTable { get; } = new List<object>();
        public List<RszInstanceInfo> InstanceInfos { get; } = new List<RszInstanceInfo>();
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public object TypeRegistry { get; set; }
        public List<object> ParsedInstances { get; } = new List<object>();
        public int CurrentOffset { get; set; }
        public string GameVersion { get; set; } = "RE4";
        public string FilePath { get; set; } = string.Empty;
        public bool AutoResourceManagement { get; set; }

        private Dictionary<object, object> _rszUserDataDict = new Dictionary<object, object>();
        private HashSet<object> _rszUserDataSet = new HashSet<object>();

        private Dictionary<RszResourceInfo, string> _resourceStrMap = new Dictionary<RszResourceInfo, string>();
        private Dictionary<RszPrefabInfo, string> _prefabStrMap = new Dictionary<RszPrefabInfo, string>();
        private Dictionary<UserDataInfo, string> _userdataStrMap = new Dictionary<UserDataInfo, string>();
        private Dictionary<RszUserDataInfo, string> _rszUserdataStrMap = new Dictionary<RszUserDataInfo, string>();
        public Dictionary<object, object> ParsedElements { get; } = new Dictionary<object, object>();
        // public Dictionary<object, InstanceHierarchy> InstanceHierarchy { get; } = new Dictionary<object, InstanceHierarchy>();
        private HashSet<object> _gameObjectInstanceIds = new HashSet<object>();
        private HashSet<object> _folderInstanceIds = new HashSet<object>();

        // Helper class for instance hierarchy
        public class InstanceHierarchy
        {
            public List<object> Children { get; set; } = new List<object>();
            public object Parent { get; set; }
        }

        public HeaderType GetHeaderType()
        {
            if (Header is UsrHeader)
                return HeaderType.Usr;
            if (Header is PfbHeader)
                return HeaderType.Pfb;
            if (Header is Pfb16Header)
                return HeaderType.Pfb16;
            if (Header is Scn18Header)
                return HeaderType.Scn18;
            if (Header is Scn19Header)
                return HeaderType.Scn19;
            return HeaderType.ScnBase;
        }

        public void Read(byte[] data)
        {
            FullData = data;
            CurrentOffset = 0;

            // Fast header type detection
            HeaderType headerType = HeaderType.ScnBase;
            if (data.Length >= 4)
            {
                uint magic = BitConverter.ToUInt32(data, 0);
                switch (magic)
                {
                    case 0x00525355: // "USR\0"
                        headerType = HeaderType.Usr;
                        Header = new UsrHeader();
                        break;
                    case 0x00424650: // "PFB\0"
                        if ((FilePath?.ToLowerInvariant() ?? string.Empty).EndsWith(".16"))
                        {
                            headerType = HeaderType.Pfb16;
                            Header = new Pfb16Header(); // Placeholder
                        }
                        else
                        {
                            headerType = HeaderType.Pfb;
                            Header = new PfbHeader(); // Placeholder
                        }
                        break;
                    default:
                        var fileExt = FilePath?.ToLowerInvariant() ?? string.Empty;
                        if (fileExt.EndsWith(".19"))
                        {
                            headerType = HeaderType.Scn19;
                            Header = new Scn19Header();
                        }
                        else if (fileExt.EndsWith(".18"))
                        {
                            headerType = HeaderType.Scn18;
                            Header = new Scn18Header();
                        }
                        else
                        {
                            headerType = HeaderType.ScnBase;
                            Header = new ScnHeaderBase();
                        }
                        break;
                }
            }
            else
            {
                Header = new ScnHeaderBase();
            }

            // Parse based on detected header type
            switch (headerType)
            {
                case HeaderType.Usr:
                    ParseUsrFile(data);
                    break;
                case HeaderType.Pfb:
                case HeaderType.Pfb16:
                    ParsePfbFile(data);
                    break;
                default:
                    ParseScnFile(data);
                    break;
            }
        }

        // Placeholder methods for parsing
        private void ParseUsrFile(byte[] data) { /* Implement parsing logic */ }
        private void ParsePfbFile(byte[] data) { /* Implement parsing logic */ }
        private void ParseScnFile(byte[] data) { /* Implement parsing logic */ }
    }

}
