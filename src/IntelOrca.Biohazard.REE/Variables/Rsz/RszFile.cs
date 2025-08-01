using IntelOrca.Biohazard.REE.Utils;
using IntelOrca.Biohazard.REE.Variables.Pfb;
using IntelOrca.Biohazard.REE.Variables.Rsz.Pfb;
using IntelOrca.Biohazard.REE.Variables.Rsz.Scn;
using System;
using System.Collections.Generic;
using System.Linq;
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

    // Helper class for instance hierarchy
    public class InstanceHierarchy
    {
        public List<object> Children { get; set; } = new List<object>();
        public object Parent { get; set; }
    }

    internal class RszFile
    {
        private int _currentOffset = 0;
        private int _instance_base_mod = 0;
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

        private HeaderType GetHeaderType()
        {
            if (Header is UsrHeader)
                return HeaderType.Usr;
            if (Header is PfbHeader pfbHeader)
            {
                return pfbHeader.Version == 16 ? HeaderType.Pfb16 : HeaderType.Pfb;
            }
            if (Header is ScnHeader scnHeader)
            {
                return scnHeader.Version switch
                {
                    19 => HeaderType.Scn19,
                    18 => HeaderType.Scn18,
                    _ => HeaderType.ScnBase
                };
            }

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
                            var pfbHeader = new PfbHeader();
                            pfbHeader.Parse(data, 16);
                            Header = pfbHeader;
                        }
                        else
                        {
                            headerType = HeaderType.Pfb;
                            var pfbHeader = new PfbHeader();
                            pfbHeader.Parse(data, 0);
                            Header = pfbHeader;
                        }
                        break;
                    default:
                        var fileExt = FilePath?.ToLowerInvariant() ?? string.Empty;
                        if (fileExt.EndsWith(".19"))
                        {
                            headerType = HeaderType.Scn19;
                            var scnHeader = new ScnHeader();
                            scnHeader.Parse(data, 19);
                            Header = scnHeader;
                        }
                        else if (fileExt.EndsWith(".18"))
                        {
                            headerType = HeaderType.Scn18;
                            var scnHeader = new ScnHeader();
                            scnHeader.Parse(data, 18);
                            Header = scnHeader;
                        }
                        else
                        {
                            headerType = HeaderType.ScnBase;
                            var scnHeader = new ScnHeader();
                            scnHeader.Parse(data, 0);
                            Header = scnHeader;
                        }
                        break;
                }
            }
            else
            {
                Header = new ScnHeader();
            }

            // Parse based on detected header type
            switch (headerType)
            {
                case HeaderType.Usr:
                    ParseUsrFile(data);
                    break;
                case HeaderType.Pfb:
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
                case ScnHeader scnHeader:
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
                    return pfbHeader.UserDataCount ?? 0;
                case ScnHeader scnHeader:
                    return scnHeader.UserdataCount;
                default:
                    return 0;
            }
        }

        private bool isScnFile()
        {
            return Header is ScnHeader;
        }
        
        private bool isPfbFile()
        {
            return Header is PfbHeader;
        }
        
        private bool isUsrFile()
        {
            return Header is UsrHeader;
        }
        
        private void ParseUsrFile(byte[] data, bool skipData = false) 
        {
            
        }
        private void ParsePfbFile(byte[] data) { /* Implement parsing logic */ }
        private void ParseScnFile(byte[] data) { /* Implement parsing logic */ }

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
            var scnHeader = Header as ScnHeader;
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
            if (Header is ScnHeader scnHeader)
                return scnHeader.DataOffset;
            return 0; // Default case
        }

        private void ParseRszSection(byte[] data, bool skipData = false)
        {
            var offset = (int)(GetHeaderDataOffset() + RszHeader.InstanceOffset);
            var structSize = Marshal.SizeOf<RszInstanceInfo>();

            for (int i = 0; i < RszHeader.InstanceCount; i++)
            {
                var span = new ReadOnlySpan<byte>(data, offset, structSize);
                var prefabInfo = MemoryMarshal.Read<RszPrefabInfo>(span);
                PrefabInfos.Add(prefabInfo);
                offset += structSize;

                if (RszHeader.Version < 4)
                {
                    _currentOffset += 8;
                }
                
                InstanceInfos.Add(instanceInfo);
            }

            // Only parse userdata if version > 3
            if (RszHeader.Version > 3)
            {
                _currentOffset = (int)(GetHeaderDataOffset() + RszHeader.UserdataOffset);

                if (FilePath.ToLowerInvariant().EndsWith(".19") || 
                    (FilePath.ToLowerInvariant().EndsWith(".18") && isScnFile()))
                {
                    ParseScn19RszUserData(data, skipData);
                }
                else if (FilePath.ToLowerInvariant().EndsWith(".16"))
                {
                    _currentOffset = ParseRszUserData(data, skipData);
                }
                else
                {
                    ParseStandardRszUserData(data);
                }
            }

            Data = data.Skip(_currentOffset).ToArray();

            _rszUserDataDict = RszUserDataInfos.ToDictionary(rui => rui.InstanceId, rui => (object)rui);
            _rszUserDataSet = new HashSet<object>(_rszUserDataDict.Keys);

            int fileOffsetOfData = _currentOffset;
            _instance_base_mod = fileOffsetOfData % 16;
        }

        private int ParseRszUserData(byte[] data)
        {
            this.RszUserDataInfos.Clear();
            var rszBaseOffset = GetHeaderDataOffset();
            var currentOffset = this._currentOffset;

            for (int i = 0; i < RszHeader.UserdataCount; i++)
            {
                var rszUserDataInfo = new RszUserDataInfo();
                currentOffset = rszUserDataInfo.Parse(data, currentOffset, true);
                this.RszUserDataInfos.Add(rszUserDataInfo);
            }

            return 0;
        }

        private int ParseScn19RszUserData(byte[] data, bool skipData)
        {
            this.RszUserDataInfos.Clear();
            var rszBaseOffset = GetHeaderDataOffset();
            var currentOffset = this._currentOffset;

            for (int i = 0; i < RszHeader.UserdataCount; i++)
            {
                var rszUserDataInfo = new RszUserDataInfo();
                currentOffset = rszUserDataInfo.Parse(data, currentOffset, true);
                this.RszUserDataInfos.Add(rszUserDataInfo);
            }

            foreach (var rszUserDataInfo in this.RszUserDataInfos)
            {
                if (rszUserDataInfo.StringOffset <= 0 || rszUserDataInfo.DataSize <= 0)
                {
                    rszUserDataInfo.Data = Array.Empty<byte>();
                    _rszUserDataStrMap[rszUserDataInfo] = "Emtpy UserData";
                }

                var magic = 0;
                var version = 0;
                var absoluteDataOffset = rszBaseOffset + rszUserDataInfo.RszOffset;

                if ((absoluteDataOffset < rszBaseOffset) || (absoluteDataOffset >= (ulong)Data.Length))
                {
                    rszUserDataInfo.Data = Array.Empty<byte>();
                    _rszUserDataStrMap[rszUserDataInfo] = "Invalid UserDat offset";
                }

                if ((absoluteDataOffset + (ulong) rszUserDataInfo.DataSize) <= (ulong) Data.Length)
                {
                    // read data from absoluteDataOffset to (absoluteDataOffset + rszUserDataInfo.DataSize)
                    rszUserDataInfo.Data = data.Skip((int)absoluteDataOffset).Take(rszUserDataInfo.DataSize).ToArray();

                    if (rszUserDataInfo.Data.Length >= 8)
                    {
                        magic = BitConverter.ToInt32(rszUserDataInfo.Data, 0);
                        version = BitConverter.ToInt32(rszUserDataInfo.Data, 4);
                    }

                    if (rszUserDataInfo.Data.Length >= 48)
                    {
                        ParseEmbeddedRsz(rszUserDataInfo, TypeRegistry, skipData);
                        if (rszUserDataInfo.EmbeddedObjectTable.Count > 0)
                        {
                            var description = $"Embedded RSZ: {rszUserDataInfo.EmbeddedObjectTable.Count}, "
                                + $"{rszUserDataInfo.EmbeddedInstanceInfos.Count} instances, "
                                + $"{rszUserDataInfo.EmbeddedInstances.Count} parsed";
                            _rszUserDataStrMap[rszUserDataInfo] = description;
                        }
                        else
                        {
                            _rszUserDataStrMap[rszUserDataInfo] = $"Rsz Parse Error (magic: 0x{magic:08X}, ver: {version})";
                        }
                    } 
                    else
                    {
                        _rszUserDataStrMap[rszUserDataInfo] = $"Not RSZ Data - Too Small {rszUserDataInfo.DataSize} bytes";
                    }
                }
                else
                {
                    rszUserDataInfo.Data = Array.Empty<byte>();
                    _rszUserDataStrMap[rszUserDataInfo] = "Invalid UserData (out of bounds)";
                }
            }

            if (RszUserDataInfos.Count > 0)
            {
                // C# equivalent of the provided Python logic
                int maxEndOffset = 0;
                try
                {
                    var validUserDataInfos = RszUserDataInfos
                        .Where(rui => rui.RszOffset > 0 && rui.DataSize > 0)
                        .Select(rui => (int)((int)rszBaseOffset + (int)rui.RszOffset + rui.DataSize));

                    if (validUserDataInfos.Any())
                    {
                        maxEndOffset = validUserDataInfos.Max();
                        currentOffset = Align(maxEndOffset, 16);
                    }
                    else
                    {
                        currentOffset = Align(currentOffset, 16);
                    }
                }
                catch (Exception)
                {
                    currentOffset = Align(currentOffset, 16);
                }
            }

            return currentOffset;
        }

        private void SetResourceString(object resourceInfo, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                if (isPfbFile() && resourceInfo is Pfb16ResourceInfo pfb16ResourceInfo)
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

        private static int AlignRel(int pos, int align, int baseMod)
        {
            int rem = (pos + baseMod) % align;
            return rem == 0 ? pos : pos + (align - rem);
        }

        private int Align(int pos, int align)
        {
            int baseMod = GetInstanceBaseMod();
            return AlignRel(pos, align, baseMod);
        }

        private int GetInstanceBaseMod()
        {
            return _instance_base_mod;
        }
    }
}

