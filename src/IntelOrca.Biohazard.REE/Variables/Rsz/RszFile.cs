using IntelOrca.Biohazard.REE.Utils;
using IntelOrca.Biohazard.REE.Variables.Pfb;
using IntelOrca.Biohazard.REE.Variables.Rsz.Pfb;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        private int _currentOffset = 0;
        public byte[] FullData { get; set; } = Array.Empty<byte>();
        public object Header { get; set; }
        public List<RszGameObject> GameObjects { get; } = new List<RszGameObject>();
        public List<RszFolderInfo> FolderInfos { get; } = new List<RszFolderInfo>();
        public List<RszResourceInfo> ResourceInfos { get; } = new List<RszResourceInfo>();
        public List<RszPrefabInfo> PrefabInfos { get; } = new List<RszPrefabInfo>();
        public List<UserDataInfo> UserDataInfos { get; } = new List<UserDataInfo>(); // from Main header
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

        private Dictionary<object, string> _resourceStrMap = new Dictionary<object, string>();
        private Dictionary<RszPrefabInfo, string> _prefabStrMap = new Dictionary<RszPrefabInfo, string>();
        private Dictionary<UserDataInfo, string> _userDataStrMap = new Dictionary<UserDataInfo, string>();
        private Dictionary<RszUserDataInfo, string> _rszUserDataStrMap = new Dictionary<RszUserDataInfo, string>();
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
                    case 0x00525355: // "USR\x00"
                        headerType = HeaderType.Usr;
                        Header = new UsrHeader();
                        break;
                    case 0x00424650: // "PFB\x00"
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

        private uint GetResourceCount()
        {
            switch (Header)
            {
                case UsrHeader usrHeader:
                    return usrHeader.ResourceCount;
                case PfbHeader pfbHeader:
                    return pfbHeader.ResourceCount;
                case Pfb16Header pfb16Header:
                    return pfb16Header.ResourceCount;
                case ScnHeaderBase scnHeader:
                    return scnHeader.ResourceCount;
                default:
                    return 0;
            }
        }

        private uint GetUserDataCount()
        {
            switch (Header)
            {
                case UsrHeader usrHeader:
                    return usrHeader.UserdataCount;
                case PfbHeader pfbHeader:
                    return pfbHeader.UserdataCount;
                case ScnHeaderBase scnHeader:
                    return scnHeader.UserdataCount;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Check if the current file is a SCN file.
        /// </summary>

        private bool isScnFile()
        {
            return Header is ScnHeaderBase || Header is Scn18Header || Header is Scn19Header;
        }

        /// <summary>
        /// Check if the current file is a PFB file.
        /// </summary>
        /// 
        private bool isPfbFile()
        {
            return Header is PfbHeader;
        }

        /// <summary>
        /// Check if the current file is a PFB 16 file.
        /// </summary>
        /// 
        private bool isPfb16File()
        {
            return Header is Pfb16Header;
        }

        /// <summary>
        /// Check if the current file is a USR file.
        /// </summary>
        /// 
        private bool isUsrFile()
        {
            return Header is UsrHeader;
        }

        /// <summary>
        /// Parses the USR file format from the provided byte array.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="skipData"></param>

        private void ParseUsrFile(byte[] data, bool skipData = false) 
        {
            
        }
        private void ParsePfbFile(byte[] data) { /* Implement parsing logic */ }
        private void ParseScnFile(byte[] data) { /* Implement parsing logic */ }

        /// <summary>
        /// Parses the resource information from the provided byte array.
        /// </summary>
        /// <param name="data"></param>
        private void ParseResourceInfos(byte[] data)
        {
            ResourceInfos.Clear();
            var resourceCount = GetResourceCount();
            int structSize = Marshal.SizeOf<RszResourceInfo>();
            int offset = _currentOffset;

            for (int i = 0; i < resourceCount; i++)
            {
                var span = new ReadOnlySpan<byte>(data, offset, structSize);
                var resourceInfo = MemoryMarshal.Read<RszResourceInfo>(span);
                ResourceInfos.Add(resourceInfo);
                offset += structSize;
            }
            _currentOffset = Align(offset, 16); 
        }

        /// <summary>
        /// Parses the user data information from the provided byte array.
        /// </summary>
        /// <param name="data"></param>
        private void ParseUserDataInfos(byte[] data)
        {
            UserDataInfos.Clear();
            var userDataCount = GetUserDataCount();
            int structSize = Marshal.SizeOf<UserDataInfo>();
            int offset = _currentOffset;

            for (int i = 0; i < userDataCount; i++)
            {
                var span = new ReadOnlySpan<byte>(data, offset, structSize);
                var userDataInfo = MemoryMarshal.Read<UserDataInfo>(span);
                UserDataInfos.Add(userDataInfo);
                offset += structSize;
            }
            _currentOffset = Align(offset, 16);
        }

        private void ParsePrefabInfo(byte[] data)
        {
            PrefabInfos.Clear();
            var scnHeader = Header as ScnHeaderBase;
            if (scnHeader == null || scnHeader.PrefabCount == 0)
                return;

            int structSize = Marshal.SizeOf<RszPrefabInfo>();
            int offset = _currentOffset;

            for (int i = 0; i < scnHeader.PrefabCount; i++)
            {
                var span = new ReadOnlySpan<byte>(data, offset, structSize);
                var prefabInfo = MemoryMarshal.Read<RszPrefabInfo>(span);
                PrefabInfos.Add(prefabInfo);
                offset += structSize;
            }
            _currentOffset = Align(offset, 16);
        }

        private void ParseBlocks(byte[] data)
        {
            _prefabStrMap.Clear();
            _userDataStrMap.Clear();
            _rszUserDataStrMap.Clear();

            if (FilePath?.ToLowerInvariant().EndsWith(".18") ?? false && isScnFile())
            {
                _resourceStrMap.Clear();
                foreach(var resourceInfo in ResourceInfos)
                {
                    if (resourceInfo.StringOffset != 0)
                    {
                        var tuple = Hex.ReadWString(FullData, resourceInfo.StringOffset, 1000);
                        SetResourceString(resourceInfo, tuple.Item1);
                    }
                }
            }

            foreach(var prefabInfo in PrefabInfos)
            {
                if (prefabInfo.StringOffset != 0)
                {
                    var tuple = Hex.ReadWString(FullData, prefabInfo.StringOffset, 1000);
                    _prefabStrMap[prefabInfo] = tuple.Item1;
                }
            }

            foreach (var userDataInfo in UserDataInfos)
            {
                if (userDataInfo.StringOffset != 0)
                {
                    var tuple = Hex.ReadWString(FullData, (uint) userDataInfo.StringOffset, 1000);
                    _userDataStrMap[userDataInfo] = tuple.Item1;
                }
            }
        }

        private ulong GetHeaderDataOffset()
        {
            if (Header is UsrHeader usrHeader)
                return usrHeader.DataOffset;
            if (Header is PfbHeader pfbHeader)
                return pfbHeader.DataOffset;
            if (Header is Pfb16Header pfb16Header)
                return pfb16Header.DataOffset;
            if (Header is Scn18Header scn18Header)
                return scn18Header.DataOffset;
            if (Header is Scn19Header scn19Header)
                return scn19Header.DataOffset;
            if (Header is ScnHeaderBase scnBaseHeader)
                return scnBaseHeader.DataOffset;
            return 0; // Default case
        }

        private void ParseRszSection(byte[] data, bool skipData = false)
        {
            _currentOffset = (int) GetHeaderDataOffset(); 
            RszHeader = new RszHeader(data);
            if (RszHeader == null)
            {
                Console.WriteLine("Failed to parse RSZ header.");
                return;
            }

            _currentOffset += RszHeader.Size;

            // Parse Object Table First
            int objectCount = (int)RszHeader.ObjectCount;
            ObjectTable.Clear();
            if (objectCount > 0)
            {
                // Each object table entry is a 4-byte int
                int entrySize = sizeof(int);
                for (int i = 0; i < objectCount; i++)
                {
                    int objId = BitConverter.ToInt32(data, _currentOffset + i * entrySize);
                    ObjectTable.Add(objId);
                }
                _currentOffset += objectCount * entrySize;
            }

            // Now we can create the gameobject and folder ID Sets
            _gameObjectInstanceIds.Clear();
            _folderInstanceIds.Clear();

            // Populate _gameObjectInstanceIds and _folderInstanceIds based on ObjectTable, GameObjects, and FolderInfos
            _gameObjectInstanceIds.Clear();
            foreach (var go in GameObjects)
            {
                if (go.Id < ObjectTable.Count)
                {
                    _gameObjectInstanceIds.Add(ObjectTable[go.Id]);
                }
            }
            _folderInstanceIds.Clear();
            foreach (var fi in FolderInfos)
            {
                if (fi.Id < ObjectTable.Count)
                {
                    _folderInstanceIds.Add(ObjectTable[fi.Id]);
                }
            }

            // Continue with rest of Rsz Section parsing
            _currentOffset = (int) GetHeaderDataOffset() + (int) RszHeader.InstanceOffset;
            int structSize = Marshal.SizeOf<RszInstanceInfo>();

            for (int i = 0; i < RszHeader.InstanceCount; i++)
            {
                var span = new ReadOnlySpan<byte>(data, _currentOffset, structSize);
                var instanceInfo = MemoryMarshal.Read<RszInstanceInfo>(span); 
                if (RszHeader.Version < 4) _currentOffset += 8;
                InstanceInfos.Add(instanceInfo);
                _currentOffset += structSize;
            }

            if (RszHeader.Version > 3)
            {
                var filepath = FilePath?.ToLowerInvariant() ?? string.Empty;
                _currentOffset = (int) GetHeaderDataOffset() + (int) RszHeader.UserdataOffset;
            }
        }
        private void SetResourceString(object resourceInfo, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                if (isPfb16File() && resourceInfo is Pfb16ResourceInfo pfb16ResourceInfo)
                {
                    pfb16ResourceInfo.StringValue = value;
                }

                _resourceStrMap[resourceInfo] = value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting resource string: {ex.Message}");
            }
        }

        /// <summary>
        /// Aligns a position relative to a base modifier.
        /// </summary>
        /// <param name="pos">The position to align.</param>
        /// <param name="align">The alignment boundary.</param>
        /// <param name="baseMod">The base modifier for alignment.</param>
        /// <returns>The aligned position.</returns>
        private static int AlignRel(int pos, int align, int baseMod)
        {
            int rem = (pos + baseMod) % align;
            return rem == 0 ? pos : pos + (align - rem);
        }

        /// <summary>
        /// Aligns a position using the instance base modifier.
        /// </summary>
        /// <param name="pos">The position to align.</param>
        /// <param name="align">The alignment boundary.</param>
        /// <returns>The aligned position.</returns>
        private int Align(int pos, int align)
        {
            int baseMod = GetInstanceBaseMod();
            return AlignRel(pos, align, baseMod);
        }

        /// <summary>
        /// Computes the instance base modifier for alignment.
        /// </summary>
        /// <returns>The base modifier value.</returns>
        private int GetInstanceBaseMod()
        {
            return (int)(GetType().GetProperty("_instance_base_mod")?.GetValue(this) ?? 0);
        }
    }
}
