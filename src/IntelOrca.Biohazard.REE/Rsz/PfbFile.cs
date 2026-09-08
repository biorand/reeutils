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
        public RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

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

        /// <summary>
        /// Original RSZ data-section offset (v16). A handful of vanilla RE2 prefabs (the
        /// <c>enemydead/emXXXX_dead.pfb.16</c> template family) carry ~12 bytes of gratuitous zero
        /// padding before the RSZ block that no layout rule predicts; the engine seeks via this
        /// header offset and ignores it. Captured so <see cref="Build"/> can replay the exact gap
        /// and stay byte-identical.
        /// </summary>
        internal ulong? OriginalDataOffset { get; private set; }

        public int GameObjectRefCount => (int)Header.GameObjectRefCount;

        /// <summary>Absolute offset of the RSZ data section as stored in the header.</summary>
        public long DataOffset => (long)Header.DataOffset;

        /// <summary>
        /// Copies the raw v16 game object ref table (16 bytes per entry) into
        /// <paramref name="destination"/>. Length must equal
        /// <see cref="GameObjectRefCount"/> * 16.
        /// </summary>
        public void ReadGameObjectRefData(byte[] destination)
        {
            var span = data.Get<GameObjectRefInfo>(Header.GameObjectRefOffset, Header.GameObjectRefCount);
            MemoryMarshal.AsBytes(span).CopyTo(destination);
        }

        /// <summary>
        /// The engine-assigned property id for each <see cref="RszFieldType.GameObjectRef"/> field
        /// that the file's ref table links, keyed by (owning type crc, field index). RSZ dumps do
        /// not carry these ids; <see cref="ReadScene"/> back-fills the ones it can reach, but the
        /// JSON export/import path has no original file to source them from. Callers persist this
        /// map alongside the exported JSON and feed it back through
        /// <see cref="ApplyGameObjectRefPropertyIds"/> before rebuilding.
        /// </summary>
        public IReadOnlyList<(uint TypeCrc, int FieldIndex, int PropertyId)> GetGameObjectRefPropertyIds(RszTypeRepository repository)
        {
            var result = new List<(uint, int, int)>();
            if (Version < 17 || Header.GameObjectRefCount == 0)
                return result;

            var objectList = Rsz.ReadObjectList(repository);
            var seen = new HashSet<(uint, int)>();
            // Multiple entries can share a source object; they appear in GameObjectRef-field
            // order, matching the writer in Build. Advance a per-object cursor so the n-th entry
            // maps to the n-th GameObjectRef field.
            var cursor = new Dictionary<int, int>();
            foreach (var r in GameObjectRefInfoList)
            {
                if (r.ObjectId < 0 || r.ObjectId >= objectList.Length)
                    continue;
                if (objectList[r.ObjectId] is not RszObjectNode source)
                    continue;

                var fields = source.Type.Fields;
                var start = cursor.GetValueOrDefault(r.ObjectId, 0);
                for (var fi = start; fi < fields.Length; fi++)
                {
                    if (fields[fi].Type != RszFieldType.GameObjectRef)
                        continue;
                    cursor[r.ObjectId] = fi + 1;
                    if (seen.Add((source.Type.Id, fi)))
                        result.Add((source.Type.Id, fi, r.PropertyIdPacked));
                    break;
                }
            }
            // Stable order (independent of ref-table order) so the serialized map round-trips
            // identically after a rebuild reorders the ref entries.
            result.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
            return result;
        }

        /// <summary>
        /// Re-applies a map produced by <see cref="GetGameObjectRefPropertyIds"/> to the shared
        /// repository field metadata so <see cref="Builder.Build"/> can regenerate the ref table
        /// on the template-based JSON import path.
        /// </summary>
        public static void ApplyGameObjectRefPropertyIds(
            RszTypeRepository repository,
            IEnumerable<(uint TypeCrc, int FieldIndex, int PropertyId)> ids)
        {
            foreach (var (crc, fieldIndex, propertyId) in ids)
            {
                var type = repository.FromId(crc);
                if (type != null && fieldIndex >= 0 && fieldIndex < type.Fields.Length)
                    type.Fields[fieldIndex].Id = propertyId;
            }
        }

        public RszScene ReadScene(RszTypeRepository repository)
        {
            return ReadScene(repository, Rsz.ReadObjectList(repository));
        }

        private RszScene ReadScene(RszTypeRepository repository, ImmutableArray<RszObjectNode> objectList)
        {
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
                OriginalDataOffset = Header.DataOffset;
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
                        break;
                    }
                }

                return new RszGameObject(gameObjectGuid, null, settings, components.ToImmutable(), children.ToImmutable());
            }
        }

        /// <summary>
        /// Returns the RSZ objects that are not part of the game object tree. The engine can still
        /// reference such objects via <c>GameObjectRef</c> fields, so they must be preserved when
        /// rebuilding or the file will be corrupted.
        /// </summary>
        private List<RszObjectNode> ReadOrphans(RszTypeRepository repository, ImmutableArray<RszObjectNode> objectList)
        {
            var claimedObjectIds = new HashSet<int>();
            var gameObjectInfoList = GameObjectInfoList;
            for (var i = 0; i < gameObjectInfoList.Length; i++)
            {
                var info = gameObjectInfoList[i];
                claimedObjectIds.Add(info.ObjectId);
                for (var componentIndex = 1; componentIndex <= info.ComponentCount; componentIndex++)
                {
                    claimedObjectIds.Add(info.ObjectId + componentIndex);
                }
            }

            var orphans = new List<RszObjectNode>();
            for (var i = 0; i < objectList.Length; i++)
            {
                if (!claimedObjectIds.Contains(i))
                {
                    orphans.Add(objectList[i]);
                }
            }
            return orphans;
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
            public List<RszObjectNode> OrphanObjects { get; } = [];

            /// <summary>
            /// Standalone objects not owned by any game object. RE2 (v16) prefabs use these
            /// to hold GameObjectRef values. Appended after all game object objects.
            /// </summary>
            public List<RszObjectNode> LooseObjects { get; } = [];

            internal ImmutableArray<RszObjectNode>? OriginalObjectList { get; set; }
            internal ImmutableArray<GameObjectRefInfo> PreservedGameObjectRefs { get; set; } = [];

            /// <summary>Original v16 RSZ data-section offset; replayed verbatim to reproduce the
            /// stray zero padding a few vanilla template prefabs carry. See
            /// <see cref="PfbFile.OriginalDataOffset"/>.</summary>
            public ulong? PreservedDataOffset { get; set; }

            /// <summary>Leading padding before the RSZ instance-info table in the source file
            /// (0 for all but a few RE2 template prefabs). See <see cref="RszFile.InstanceListPad"/>.</summary>
            public int PreservedRszInstanceListPad { get; set; }

            /// <summary>
            /// Raw v16 game object ref table as read from the original file (16 bytes per entry).
            /// Set this on a template-free builder so <see cref="Build"/> can replay engine-assigned
            /// property ids that are not present in RSZ type dumps.
            /// </summary>
            public ReadOnlyMemory<byte>? PreservedGameObjectRefData { get; set; }

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
                // Read the object list once so the scene, the v16 loose objects, and the orphan
                // objects all share node references, keeping shared instances (e.g. prefab trigger
                // objects) intact.
                var objectList = instance.Rsz.ReadObjectList(repository);
                Scene = instance.ReadScene(repository, objectList);

                // RE2 non-RT (v16): raw game-object-ref replay data captured by ReadScene.
                LooseObjects.AddRange(instance.LooseObjects);
                OriginalObjectList = instance.OriginalObjectList;
                PreservedGameObjectRefs = instance.PreservedGameObjectRefs;
                PreservedDataOffset = instance.OriginalDataOffset;
                PreservedRszInstanceListPad = instance.Rsz.InstanceListPad;

                OrphanObjects = instance.ReadOrphans(repository, objectList);

                if (Version >= 17)
                {
                    // The RSZ dump doesn't contain property IDs for every field, and the read pass
                    // only assigns them to the first source object of each ref. Assign the remaining
                    // property IDs (from the original ref table) to the orphan fields so their refs
                    // can be regenerated, since orphan objects are not part of the game object tree.
                    // v16 replays its ref table verbatim and never consults field.Id, so skip it.
                    var orphanSet = OrphanObjects.ToHashSet();
                    foreach (var refInfo in instance.GameObjectRefInfoList)
                    {
                        if (refInfo.ObjectId < 0 || refInfo.ObjectId >= objectList.Length)
                            continue;
                        if (objectList[refInfo.ObjectId] is not RszObjectNode sourceObject ||
                            !orphanSet.Contains(sourceObject))
                        {
                            continue;
                        }
                        foreach (var field in sourceObject.Type.Fields)
                        {
                            if (field.Type != RszFieldType.GameObjectRef)
                                continue;
                            if (field.Id == refInfo.PropertyIdPacked)
                                break;
                            if (field.Id == null)
                            {
                                field.Id = refInfo.PropertyIdPacked;
                                break;
                            }
                        }
                    }
                }
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
                // RE2 (v16) resource blobs can contain paths not referenced by any RSZ node
                // (e.g. sound prefabs); they cannot be rediscovered from the scene graph, so
                // keep the seeded list and only append newly seen resources.
                if (Version < 17)
                    return AddMissingResources();

                Resources.Clear();
                return AddMissingResources();
            }

            public PfbFile Build()
            {
                var gameObjectsGuid = new List<Guid>();
                var gameObjects = new List<GameObjectInfo>();
                var objectList = ImmutableArray.CreateBuilder<RszObjectNode>();
                Traverse(-1, Scene);

                // Preserve RSZ objects that are not part of the game object tree, as the engine can
                // still reference them through GameObjectRef fields (e.g. via app.InteractTrigger*).
                // v16 tracks these as LooseObjects (raw-ref replay); v17+ as OrphanObjects (Ted's
                // orphan-preservation path). They describe the same set, so append exactly one.
                if (Version < 17)
                {
                    foreach (var looseObject in LooseObjects)
                    {
                        objectList.Add(looseObject);
                    }
                }
                else
                {
                    // Binary ToBuilder populates OrphanObjects; the template-based JSON import
                    // path carries the same set in as LooseObjects (from the "@loose" document
                    // section) with OrphanObjects left empty. Fall back to it so those objects
                    // are not dropped on rebuild.
                    var extras = OrphanObjects.Count > 0 || LooseObjects.Count == 0
                        ? (IReadOnlyList<RszObjectNode>)OrphanObjects
                        : LooseObjects;
                    foreach (var orphan in extras)
                    {
                        objectList.Add(orphan);
                    }
                }

                var rszBuilder = new RszFile.Builder(Repository, RszVersion);
                rszBuilder.Objects = objectList.ToImmutable();
                rszBuilder.PreservedInstanceListPad = PreservedRszInstanceListPad;
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
                // v16 with a preserved raw ref table: replay it (property ids are engine-assigned
                // and absent from RSZ dumps). Otherwise regenerate refs from the scene - orphan
                // fields had their property ids assigned during construction, so they regenerate
                // too (Ted's b2f61dd/6051d93 orphan-preservation path for v17+).
                if (Version < 17 && PreservedGameObjectRefs.Length == 0 && PreservedGameObjectRefData is { Length: > 0 } rawRefs)
                {
                    // Template-free build: replay the raw ref table verbatim (object ids are
                    // positional, so they stay valid as long as the scene shape is unchanged).
                    gameObjectRefCount = rawRefs.Length / 16; // sizeof(GameObjectRefInfo)
                    bw.Write(rawRefs.Span);
                }
                else if (Version < 17 && PreservedGameObjectRefs.Length > 0)
                {
                    gameObjectRefCount = WritePreservedGameObjectRefs();
                }
                else
                {
                    WriteGameObjectRefs();
                }

                // A few vanilla v16 template prefabs (enemydead/emXXXX_dead) carry a small run of
                // stray zero padding between the game-object-ref section and the RSZ block that no
                // layout rule predicts; the engine seeks via the header offset and ignores it.
                // These files always have an empty resource section, so replaying the gap here
                // lands the resource offset, data offset and RSZ block byte-identically. Bounded
                // to a single 16-byte step so a bogus preserved value can never balloon the file.
                if (Version < 17 && Resources.Count == 0 && PreservedDataOffset is { } preservedDataOffset)
                {
                    var pad = (long)preservedDataOffset - ms.Position;
                    if (pad > 0 && pad <= 16)
                        bw.WriteZeros((int)pad);
                }

                // Resources
                var resourceOffset = ms.Position;
                if (Version < 17)
                {
                    bw.Align(4);
                    resourceOffset = ms.Position;

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
