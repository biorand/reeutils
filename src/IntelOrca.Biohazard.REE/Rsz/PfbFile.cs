using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class PfbFile(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x00424650;

        public ReadOnlyMemory<byte> Data => data;

        public int Version => version;
        private PfbHeader Header => new PfbHeader(Version, version < 17 ? data[..HeaderSizeV16] : data[..56]);
        private const int HeaderSizeV16 = 40;
        private ReadOnlySpan<GameObjectInfo> GameObjectInfoList => data.Get<GameObjectInfo>((ulong)Header.Size, Header.GameObjectCount);
        private ReadOnlySpan<GameObjectRefInfo> GameObjectRefInfoList => data.Get<GameObjectRefInfo>(Header.GameObjectRefOffset, Header.GameObjectRefCount);
        private ReadOnlySpan<ResourceInfo> ResourceInfoList => Version < 17 ? default : data.Get<ResourceInfo>(Header.ResourceOffset, Header.ResourceCount);
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        public int InstanceCount => Rsz.InstanceCount;

        public int RszVersion => Rsz.Version;

        public ImmutableArray<string> Resources
        {
            get
            {
                var result = ImmutableArray.CreateBuilder<string>();
                if (Version < 17)
                {
                    // RE2 (v16): resource paths are packed NUL-terminated UTF16 strings
                    // starting at ResourceOffset. There is no offset table.
                    var span = MemoryMarshal.Cast<byte, char>(data.Span.Slice((int)Header.ResourceOffset));
                    for (var i = 0; i < Header.ResourceCount; i++)
                    {
                        var start = 0;
                        while (start < span.Length && span[start] == '\0')
                        {
                            start++;
                        }

                        var end = start;
                        while (end < span.Length && span[end] != '\0')
                        {
                            end++;
                        }

                        result.Add(new string(span.Slice(start, end - start).ToArray()));
                        span = span.Slice(Math.Min(end + 1, span.Length));
                    }
                    return result.ToImmutable();
                }

                var resourceInfoList = ResourceInfoList;
                for (var i = 0; i < resourceInfoList.Length; i++)
                {
                    result.Add(GetString(resourceInfoList[i].PathOffset));
                }
                return result.ToImmutable();
            }
        }

        private string GetString(ulong offset)
        {
            if (offset != 0)
            {
                var span = MemoryMarshal.Cast<byte, char>(Data.Slice((int)offset).Span);
                for (var i = 0; i < span.Length; i++)
                {
                    if (span[i] == '\0')
                    {
                        return new string(span.Slice(0, i).ToArray());
                    }
                }
            }
            return string.Empty;
        }

        public ImmutableArray<RszObjectNode> LooseObjects { get; private set; } = [];

        /// <summary>
        /// Raw game object ref entries preserved from a v16 file. RE2 (v16) property ids are
        /// engine-assigned and not present in the RSZ dumps, so entries are replayed verbatim
        /// on build (with object id remapping) instead of being recomputed.
        /// </summary>
        internal ImmutableArray<GameObjectRefInfo> PreservedGameObjectRefs { get; private set; } = [];

        /// <summary>
        /// Object list as read from the file, captured by <see cref="ReadScene"/> so the builder
        /// can remap preserved v16 game object refs to rebuilt object ids.
        /// </summary>
        internal ImmutableArray<RszObjectNode>? OriginalObjectList { get; private set; }

        public RszScene ReadScene(RszTypeRepository repository)
        {
            var objectList = Rsz.ReadObjectList(repository);
            var gameObjectInfoList = GameObjectInfoList.ToImmutableArray();
            // RE2 (v16) packs arrayIndex/propertyId into one u32:
            // packed = (arrayIndex << 16) | propertyId
            var gameObjectRefs = GameObjectRefInfoList.ToArray();
            if (Version < 17)
            {
                for (var i = 0; i < gameObjectRefs.Length; i++)
                {
                    var packed = gameObjectRefs[i].PropertyIdPacked;
                    gameObjectRefs[i] = new GameObjectRefInfo()
                    {
                        ObjectId = gameObjectRefs[i].ObjectId,
                        PropertyIdPacked = packed & 0xFFFF,
                        ArrayIndex = (packed >> 16) & 0xFFFF,
                        TargetId = gameObjectRefs[i].TargetId
                    };
                }
                PreservedGameObjectRefs = GameObjectRefInfoList.ToImmutableArray();
            }
            OriginalObjectList = objectList;
            LooseObjects = ReadLooseObjects(objectList, gameObjectInfoList);
            return BuildRoot();

            ImmutableArray<RszObjectNode> ReadLooseObjects(ImmutableArray<RszObjectNode> allObjects, ImmutableArray<GameObjectInfo> goInfos)
            {
                // Objects not reachable from any game object (settings + components). RE2 prefabs
                // can append standalone objects that hold GameObjectRef values (e.g. camera targets).
                var consumed = new bool[allObjects.Length];
                foreach (var goInfo in goInfos)
                {
                    if (goInfo.ObjectId < consumed.Length)
                        consumed[goInfo.ObjectId] = true;
                    for (var i = 0; i < goInfo.ComponentCount; i++)
                    {
                        var componentIndex = goInfo.ObjectId + 1 + i;
                        if (componentIndex < consumed.Length)
                            consumed[componentIndex] = true;
                    }
                }

                var loose = ImmutableArray.CreateBuilder<RszObjectNode>();
                for (var i = 0; i < allObjects.Length; i++)
                {
                    if (!consumed[i])
                        loose.Add(allObjects[i]);
                }
                return loose.ToImmutable();
            }

            RszScene BuildRoot()
            {
                var children = ImmutableArray.CreateBuilder<IRszSceneNode>();
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == -1)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }
                return new RszScene(children.ToImmutable());
            }

            RszGameObject BuildGameObject(int id)
            {
                var info = gameObjectInfoList[id];
                var settings = (RszObjectNode)objectList[info.ObjectId];

                var components = ImmutableArray.CreateBuilder<RszObjectNode>();
                for (var i = 0; i < info.ComponentCount; i++)
                {
                    var componentIndex = info.ObjectId + 1 + i;
                    if (componentIndex >= objectList.Length)
                    {
                        break;
                    }
                    components.Add((RszObjectNode)objectList[componentIndex]);
                }

                var children = ImmutableArray.CreateBuilder<RszGameObject>();
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == info.ObjectId)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }

                var gameObjectGuid = default(Guid);
                var gameObjectRefInfoIndex = Array.FindIndex(gameObjectRefs, x => x.TargetId == info.ObjectId);
                if (gameObjectRefInfoIndex != -1)
                {
                    var gameObjectRefInfo = gameObjectRefs[gameObjectRefInfoIndex];
                    var sourceObjectId = gameObjectRefInfo.ObjectId;
                    var sourceObject = (RszObjectNode)objectList[sourceObjectId];
                    var sourceObjectFields = sourceObject.Type.Fields;
                    for (var i = 0; i < sourceObjectFields.Length; i++)
                    {
                        var field = sourceObjectFields[i];
                        if (field.Type != RszFieldType.GameObjectRef)
                            continue;

                        // HACK: Since the RSZ dumps don't contain property IDs, we set the property ID of
                        // the relevant fields based on the order of game object refs in the file.
                        // This is a very rough work around.
                        if (field.Id is int fieldId)
                        {
                            if (fieldId != gameObjectRefInfo.PropertyIdPacked)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            field.Id = gameObjectRefInfo.PropertyIdPacked;
                        }
                        if (field.IsArray)
                        {
                            var arrayNode = (RszArrayNode)sourceObject[i];
                            var arrayIndex = gameObjectRefInfo.ArrayIndex;
                            if (arrayIndex >= 0 && arrayIndex < arrayNode.Children.Length)
                            {
                                gameObjectGuid = RszSerializer.Deserialize<Guid>(arrayNode.Children[arrayIndex]);
                            }
                        }
                        else
                        {
                            gameObjectGuid = RszSerializer.Deserialize<Guid>(sourceObject[i]);
                        }
                    }
                }

                return new RszGameObject(gameObjectGuid, null, settings, components.ToImmutable(), children.ToImmutable());
            }
        }

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public int Version { get; }
            public int RszVersion { get; }
            public List<string> Resources { get; } = [];
            public RszScene Scene { get; set; } = new RszScene();

            /// <summary>
            /// Standalone objects not owned by any game object. RE2 (v16) prefabs use these
            /// to hold GameObjectRef values. Appended after all game object objects.
            /// </summary>
            public List<RszObjectNode> LooseObjects { get; } = [];

            internal ImmutableArray<RszObjectNode>? OriginalObjectList { get; set; }
            internal ImmutableArray<GameObjectRefInfo> PreservedGameObjectRefs { get; set; } = [];

            public Builder(RszTypeRepository repository, int version, int rszVersion)
            {
                Repository = repository;
                Version = version;
                RszVersion = rszVersion;
            }

            public Builder(RszTypeRepository repository, PfbFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                RszVersion = instance.Rsz.Version;
                Resources = instance.Resources.ToList();
                Scene = instance.ReadScene(repository);
                LooseObjects.AddRange(instance.LooseObjects);
                OriginalObjectList = instance.OriginalObjectList;
                PreservedGameObjectRefs = instance.PreservedGameObjectRefs;
            }

            public Builder AddMissingResources()
            {
                var resourceHash = new HashSet<string>(Resources, StringComparer.OrdinalIgnoreCase);
                Scene.Visit(node =>
                {
                    if (node is RszResourceNode resourceNode && !string.IsNullOrEmpty(resourceNode.Value))
                    {
                        var resourceValue = resourceNode.Value;
                        if (resourceHash.Add(resourceValue))
                        {
                            Resources.Add(resourceValue);
                        }
                    }
                });
                return this;
            }

            public Builder RebuildResources()
            {
                Resources.Clear();
                return AddMissingResources();
            }

            public PfbFile Build()
            {
                var gameObjectsGuid = new List<Guid>();
                var gameObjects = new List<GameObjectInfo>();
                var objectList = ImmutableArray.CreateBuilder<RszObjectNode>();
                Traverse(-1, Scene);
                foreach (var looseObject in LooseObjects)
                {
                    objectList.Add(looseObject);
                }

                var rszBuilder = new RszFile.Builder(Repository, RszVersion);
                rszBuilder.Objects = objectList.ToImmutable();
                var rsz = rszBuilder.Build();

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                var stringPool = new StringPoolBuilder(ms);

                // Reserve space for header
                bw.WriteZeros(Version < 17 ? HeaderSizeV16 : 56);

                // Game objects
                foreach (var gameObject in gameObjects)
                {
                    bw.Write(gameObject);
                }

                // Game object refs
                var gameObjectRefOffset = ms.Position;
                var gameObjectRefCount = 0;
                if (Version < 17 && PreservedGameObjectRefs.Length > 0)
                {
                    gameObjectRefCount = WritePreservedGameObjectRefs();
                }
                else
                {
                    WriteGameObjectRefs();
                }

                // Resources
                var resourceOffset = ms.Position;
                if (Version < 17)
                {
                    // RE2 (v16): resources are packed NUL-terminated UTF16 strings,
                    // not a table of offsets into a string pool.
                    foreach (var resource in Resources)
                    {
                        foreach (var ch in resource)
                        {
                            bw.Write((short)ch);
                        }
                        bw.Write((short)0);
                    }
                }
                else
                {
                    bw.Align(16);
                    resourceOffset = ms.Position;
                    foreach (var resource in Resources)
                    {
                        stringPool.WriteStringOffset64(resource);
                    }
                }

                // Userdata
                var userDataOffset = 0L;
                var userDataCount = 0;
                if (Version >= 17)
                {
                    bw.Align(16);
                    userDataOffset = ms.Position;
                    var userDataList = rsz.UserDataInfoList;
                    var userDataListPaths = rsz.UserDataInfoPaths;
                    for (var i = 0; i < userDataList.Length; i++)
                    {
                        bw.Write(userDataList[i].TypeId);
                        bw.Write(0);
                        stringPool.WriteStringOffset64(userDataListPaths[i]);
                    }
                    userDataCount = userDataList.Length;
                }

                // String data
                if (Version >= 17)
                {
                    bw.Align(16);
                    stringPool.WriteStrings();
                }

                // Instance data
                var rszDataOffset = ms.Position;
                rszBuilder.AlignOffset = rszDataOffset;
                rsz = rszBuilder.Build();
                bw.Write(rsz.Data.Span);

                // Header
                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(gameObjects.Count);
                bw.Write(Resources.Count);
                bw.Write(gameObjectRefCount);
                if (Version >= 17)
                {
                    bw.Write(userDataCount);
                    bw.Write(0);
                }
                bw.Write(gameObjectRefOffset);
                bw.Write(resourceOffset);
                if (Version >= 17)
                {
                    bw.Write(userDataOffset);
                }
                bw.Write(rszDataOffset);

                return new PfbFile(Version, ms.ToArray());

                void WriteGameObjectRefs()
                {
                    for (var i = 0; i < objectList.Count; i++)
                    {
                        var sourceObject = (RszObjectNode)objectList[i];
                        for (var j = 0; j < sourceObject.Children.Length; j++)
                        {
                            var rszType = sourceObject.Type;
                            var fieldType = rszType.Fields[j];
                            if (fieldType.Type != RszFieldType.GameObjectRef)
                            {
                                continue;
                            }

                            var fieldValue = sourceObject.Children[j];
                            var fieldArrayValues = new List<Guid>();
                            if (fieldType.IsArray)
                            {
                                var arrayValue = (RszArrayNode)fieldValue;
                                foreach (var arrayElementValue in arrayValue)
                                {
                                    var guid = RszSerializer.Deserialize<Guid>(arrayElementValue);
                                    fieldArrayValues.Add(guid);
                                }
                            }
                            else
                            {
                                var guid = RszSerializer.Deserialize<Guid>(fieldValue);
                                fieldArrayValues.Add(guid);
                            }

                            var arrayIndex = 0;
                            foreach (var guid in fieldArrayValues)
                            {
                                if (guid == default)
                                    continue;

                                var gameObjectIndex = gameObjectsGuid.IndexOf(guid);
                                if (gameObjectIndex == -1)
                                    continue;

                                bw.Write(new GameObjectRefInfo()
                                {
                                    ObjectId = i,
                                    TargetId = gameObjects[gameObjectIndex].ObjectId,
                                    PropertyIdPacked = fieldType.Id ?? throw new Exception($"Id not set on field: {rszType.Name}.{fieldType.Name}."),
                                    ArrayIndex = arrayIndex
                                });
                                arrayIndex++;
                                gameObjectRefCount++;
                            }
                        }
                    }
                }

                int WritePreservedGameObjectRefs()
                {
                    // RE2 (v16): property ids are engine-assigned and unknown to the RSZ dump, so the
                    // original ref table is replayed. src/dst object ids are remapped through object
                    // identity: (type crc, occurrence index among instances of that type).
                    var oldObjects = OriginalObjectList;
                    if (oldObjects == null)
                    {
                        // Template-free build: no preserved identity to remap against.
                        return 0;
                    }
                    var oldList = oldObjects.GetValueOrDefault();
                    if (oldList.Length != objectList.Count)
                    {
                        // Object list changed shape (edited scene); fall back to positional replay where
                        // possible, skipping refs that no longer resolve.
                        return WriteRemappedRefs(i => i);
                    }

                    var oldToNew = BuildIdentityMap(oldList);
                    return WriteRemappedRefs(i => oldToNew.TryGetValue(i, out var newIndex) ? newIndex : -1);

                    int WriteRemappedRefs(Func<int, int> remapObjectId)
                    {
                        var count = 0;
                        foreach (var raw in PreservedGameObjectRefs)
                        {
                            var srcId = remapObjectId(raw.ObjectId);
                            var dstId = remapObjectId(raw.TargetId);
                            if (srcId < 0 || dstId < 0)
                                continue;

                            bw.Write(new GameObjectRefInfo()
                            {
                                ObjectId = srcId,
                                PropertyIdPacked = raw.PropertyIdPacked,
                                ArrayIndex = raw.ArrayIndex,
                                TargetId = dstId
                            });
                            count++;
                        }
                        return count;
                    }
                }

                Dictionary<int, int> BuildIdentityMap(ImmutableArray<RszObjectNode> from)
                {
                    // Map old object index -> new object index by (type crc, k-th occurrence of type).
                    var result = new Dictionary<int, int>();
                    for (var oldId = 0; oldId < from.Length; oldId++)
                    {
                        var node = from[oldId];
                        var occurrence = CountOccurrences(from, oldId);
                        result[oldId] = FindOccurrence(node.Type.Crc, occurrence);
                    }
                    return result;

                    int CountOccurrences(ImmutableArray<RszObjectNode> list, int index)
                    {
                        var key = list[index].Type.Crc;
                        var seen = 0;
                        for (var i = 0; i < index; i++)
                        {
                            if (list[i].Type.Crc == key)
                                seen++;
                        }
                        return seen;
                    }

                    int FindOccurrence(uint key, int occurrence)
                    {
                        var seen = 0;
                        for (var i = 0; i < objectList.Count; i++)
                        {
                            if (((RszObjectNode)objectList[i]).Type.Crc != key)
                                continue;
                            if (seen++ == occurrence)
                                return i;
                        }
                        return -1;
                    }
                }

                int AddObject(RszObjectNode node)
                {
                    var index = objectList.Count;
                    objectList.Add(node);
                    return index;
                }

                void Traverse(int parentId, IRszNode node)
                {
                    var id = parentId;
                    if (node is RszGameObject gameObjectNode)
                    {
                        id = AddObject(gameObjectNode.Settings);
                        gameObjectsGuid.Add(gameObjectNode.Guid);
                        gameObjects.Add(new GameObjectInfo()
                        {
                            ObjectId = id,
                            ParentId = parentId,
                            ComponentCount = (short)gameObjectNode.Components.Length
                        });
                        foreach (var component in gameObjectNode.Components)
                        {
                            AddObject(component);
                        }
                    }

                    if (node is IRszNodeContainer container)
                    {
                        foreach (var child in container.Children)
                        {
                            Traverse(id, child);
                        }
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ResourceInfo
        {
            public ulong PathOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GameObjectInfo
        {
            public int ObjectId;
            public int ParentId;
            public int ComponentCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GameObjectRefInfo
        {
            public int ObjectId;
            public int PropertyIdPacked;
            public int ArrayIndex;
            public int TargetId;

            // v17+: PropertyIdPacked holds the field's property id.
            // v16: packed = (ArrayIndex << 16) | PropertyId, stored split into the two int fields above.
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UserDataInfo
        {
            public uint TypeId;
            public uint Padding;
            public ulong PathOffset;
        }
    }
}
