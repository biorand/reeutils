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
        private PfbHeader Header => new PfbHeader(Version, version < 17 ? data[..48] : data[..56]);
        private ReadOnlySpan<GameObjectInfo> GameObjectInfoList => data.Get<GameObjectInfo>((ulong)Header.Size, Header.GameObjectCount);
        private ReadOnlySpan<GameObjectRefInfo> GameObjectRefInfoList => data.Get<GameObjectRefInfo>(Header.GameObjectRefOffset, Header.GameObjectRefCount);
        private ReadOnlySpan<ResourceInfo> ResourceInfoList => data.Get<ResourceInfo>(Header.ResourceOffset, Header.ResourceCount);
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        public RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        public int InstanceCount => Rsz.InstanceCount;

        public int RszVersion => Rsz.Version;

        public ImmutableArray<string> Resources
        {
            get
            {
                var result = ImmutableArray.CreateBuilder<string>();
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

        public RszScene ReadScene(RszTypeRepository repository)
        {
            return ReadScene(repository, Rsz.ReadObjectList(repository));
        }

        private RszScene ReadScene(RszTypeRepository repository, ImmutableArray<RszObjectNode> objectList)
        {
            var gameObjectInfoList = GameObjectInfoList.ToImmutableArray();
            var gameObjectRefs = GameObjectRefInfoList.ToArray();
            return BuildRoot();

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
                    components.Add((RszObjectNode)objectList[info.ObjectId + 1 + i]);
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
                            if (fieldId != gameObjectRefInfo.PropertyId)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            field.Id = gameObjectRefInfo.PropertyId;
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
                // Read the object list once so that the scene and the orphan objects share node
                // references, keeping shared instances (e.g. prefab trigger objects) intact.
                var objectList = instance.Rsz.ReadObjectList(repository);
                Scene = instance.ReadScene(repository, objectList);
                OrphanObjects = instance.ReadOrphans(repository, objectList);

                // The RSZ dump doesn't contain property IDs for every field, and the read pass only
                // assigns them to the first source object of each ref. Assign the remaining property
                // IDs (from the original ref table) to the orphan fields so their refs can be
                // regenerated, since orphan objects are not part of the game object tree.
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
                        if (field.Id == refInfo.PropertyId)
                            break;
                        if (field.Id == null)
                        {
                            field.Id = refInfo.PropertyId;
                            break;
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
                foreach (var orphan in OrphanObjects)
                {
                    objectList.Add(orphan);
                }

                var rszBuilder = new RszFile.Builder(Repository, RszVersion);
                rszBuilder.Objects = objectList.ToImmutable();
                var rsz = rszBuilder.Build();

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                var stringPool = new StringPoolBuilder(ms);

                // Reserve space for header
                bw.WriteZeros(Version < 17 ? 48 : 56);

                // Game objects
                foreach (var gameObject in gameObjects)
                {
                    bw.Write(gameObject);
                }

                // Game object refs
                var gameObjectRefOffset = ms.Position;
                var gameObjectRefCount = 0;

                // Refs for objects can be regenerated from the scene. Orphan fields have their
                // property IDs assigned during construction, so they are regenerated too.
                for (var i = 0; i < objectList.Count; i++)
                {
                    var sourceObject = (RszObjectNode)objectList[i];
                    for (var j = 0; j < sourceObject.Children.Length; j++)
                    {
                        var rszType = sourceObject.Type;
                        var fieldType = rszType.Fields[j];
                        if (fieldType.Type == RszFieldType.GameObjectRef)
                        {
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
                                    PropertyId = fieldType.Id ?? throw new Exception($"Id not set on field: {rszType.Name}.{fieldType.Name}."),
                                    ArrayIndex = arrayIndex
                                });
                                arrayIndex++;
                                gameObjectRefCount++;
                            }
                        }
                    }
                }

                // Resources
                bw.Align(16);
                var resourceOffset = ms.Position;
                foreach (var resource in Resources)
                {
                    stringPool.WriteStringOffset64(resource);
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
                bw.Align(16);
                stringPool.WriteStrings();

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
        private struct GameObjectRefInfo
        {
            public int ObjectId;
            public int PropertyId;
            public int ArrayIndex;
            public int TargetId;
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
