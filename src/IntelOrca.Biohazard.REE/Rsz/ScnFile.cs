using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class ScnFile(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x004E4353;

        public ReadOnlyMemory<byte> Data => data;

        public int Version => version;
        private ScnHeader Header => new ScnHeader(Version, version <= 18 ? data[..56] : data[..64]);
        private ReadOnlySpan<GameObjectInfo> GameObjectInfoList => data.Get<GameObjectInfo>((ulong)Header.Size, Header.GameObjectCount);
        private ReadOnlySpan<FolderInfo> FolderInfoList => data.Get<FolderInfo>(Header.FolderOffset, Header.FolderCount);
        private ReadOnlySpan<PrefabInfo> PrefabInfoList => data.Get<PrefabInfo>(Header.PrefabOffset, Header.PrefabCount);
        private ReadOnlySpan<ResourceInfo> ResourceInfoList => data.Get<ResourceInfo>(Header.ResourceOffset, Header.ResourceCount);
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        public ImmutableArray<string> Prefabs
        {
            get
            {
                var result = ImmutableArray.CreateBuilder<string>();
                var prefabInfoList = PrefabInfoList;
                for (var i = 0; i < prefabInfoList.Length; i++)
                {
                    result.Add(GetString(prefabInfoList[i].PathOffset));
                }
                return result.ToImmutable();
            }
        }

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
            var objectList = Rsz.ReadObjectList(repository);
            var folderInfoList = FolderInfoList.ToImmutableArray();
            var gameObjectInfoList = GameObjectInfoList.ToImmutableArray();
            var prefabs = Prefabs;
            return BuildRoot();

            RszScene BuildRoot()
            {
                var children = ImmutableArray.CreateBuilder<IRszSceneNode>();
                for (var i = 0; i < folderInfoList.Length; i++)
                {
                    if (folderInfoList[i].ParentId == -1)
                    {
                        children.Add(BuildFolder(i));
                    }
                }
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == -1)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }
                return new RszScene(children.ToImmutable());
            }

            RszFolder BuildFolder(int id)
            {
                var info = folderInfoList[id];
                var settings = (RszStructNode)objectList[info.ObjectId];
                var children = ImmutableArray.CreateBuilder<IRszSceneNode>();
                for (var i = 0; i < folderInfoList.Length; i++)
                {
                    if (folderInfoList[i].ParentId == info.ObjectId)
                    {
                        children.Add(BuildFolder(i));
                    }
                }
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == info.ObjectId)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }
                return new RszFolder(settings, children.ToImmutable());
            }

            RszGameObject BuildGameObject(int id)
            {
                var info = gameObjectInfoList[id];
                var settings = (RszStructNode)objectList[info.ObjectId];

                var components = ImmutableArray.CreateBuilder<IRszNode>();
                for (var i = 0; i < info.ComponentCount; i++)
                {
                    components.Add(objectList[info.ObjectId + 1 + i]);
                }

                var prefab = info.PrefabId >= 0 && info.PrefabId < prefabs.Length
                    ? prefabs[info.PrefabId]
                    : null;
                var children = ImmutableArray.CreateBuilder<RszGameObject>();
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == info.ObjectId)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }

                return new RszGameObject(info.Guid, prefab, settings, components.ToImmutable(), children.ToImmutable());
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

            public Builder(RszTypeRepository repository, int version, int rszVersion)
            {
                Repository = repository;
                Version = version;
                RszVersion = rszVersion;
            }

            public Builder(RszTypeRepository repository, ScnFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                RszVersion = instance.Rsz.Version;
                Resources = instance.Resources.ToList();
                Scene = instance.ReadScene(repository);
            }

            public ScnFile Build()
            {
                var folders = new List<FolderInfo>();
                var gameObjects = new List<GameObjectInfo>();
                var prefabs = new List<string>();
                var prefabToId = new Dictionary<string, int>();
                var objectList = ImmutableArray.CreateBuilder<IRszNode>();
                Traverse(-1, Scene);

                var rszBuilder = new RszFile.Builder(Repository, RszVersion);
                rszBuilder.Objects = objectList.ToImmutable();
                var rsz = rszBuilder.Build();

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                var stringPool = new StringPoolBuilder(ms);

                // Reserve space for header
                bw.Skip(Version >= 19 ? 64 : 56);

                foreach (var gameObject in gameObjects)
                {
                    bw.Write(gameObject);
                }

                bw.Align(16);
                var folderOffset = ms.Position;
                foreach (var folder in folders)
                {
                    bw.Write(folder);
                }

                bw.Align(16);
                var resourceOffset = ms.Position;
                foreach (var resource in Resources)
                {
                    stringPool.WriteStringOffset64(resource);
                }

                bw.Align(16);
                var prefabOffset = ms.Position;
                foreach (var prefab in prefabs)
                {
                    stringPool.WriteStringOffset64(prefab);
                }

                bw.Align(16);
                var userDataOffset = ms.Position;
                var userDataCount = 0;
                if (Version >= 20)
                {
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

                bw.Align(16);
                stringPool.WriteStrings();

                // Instance data
                bw.Align(16);
                var rszDataOffset = ms.Position;
                bw.Write(rsz.Data.Span);

                // Header
                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(gameObjects.Count); // Game object count
                bw.Write(Resources.Count); // Resource count
                bw.Write(folders.Count); // Folder count
                bw.Write(prefabs.Count); // Prefab count
                bw.Write(userDataCount); // User data count
                bw.Write(folderOffset); // Folder offset
                bw.Write(resourceOffset); // Resource offset
                bw.Write(prefabOffset); // Resource offset
                bw.Write(userDataOffset); // User data offset
                bw.Write(rszDataOffset); // RSZ data offset

                return new ScnFile(Version, ms.ToArray());

                int AddObject(IRszNode node)
                {
                    var index = objectList.Count;
                    objectList.Add(node);
                    return index;
                }

                int AddPrefab(string? prefab)
                {
                    if (prefab == null)
                        return -1;

                    if (prefabToId.TryGetValue(prefab, out var id))
                        return id;

                    var index = prefabs.Count;
                    prefabs.Add(prefab);
                    prefabToId.Add(prefab, index);
                    return index;
                }

                void Traverse(int parentId, IRszNode node)
                {
                    var id = parentId;
                    if (node is RszFolder folderNode)
                    {
                        id = AddObject(folderNode.Settings);
                        folders.Add(new FolderInfo()
                        {
                            ObjectId = id,
                            ParentId = parentId
                        });
                    }
                    else if (node is RszGameObject gameObjectNode)
                    {
                        id = AddObject(gameObjectNode.Settings);
                        gameObjects.Add(new GameObjectInfo()
                        {
                            Guid = gameObjectNode.Guid,
                            ObjectId = id,
                            ParentId = parentId,
                            ComponentCount = (short)gameObjectNode.Components.Length,
                            PrefabId = AddPrefab(gameObjectNode.Prefab)
                        });
                        foreach (var component in gameObjectNode.Components)
                        {
                            AddObject(component);
                        }
                    }

                    foreach (var child in node.Children)
                    {
                        Traverse(id, child);
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FolderInfo
        {
            public int ObjectId;
            public int ParentId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GameObjectInfo
        {
            public Guid Guid;
            public int ObjectId;
            public int ParentId;
            public short ComponentCount;
            public short Padding;
            public int PrefabId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ResourceInfo
        {
            public ulong PathOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PrefabInfo
        {
            public ulong PathOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UserDataInfo
        {
            public uint TypeId;
            public uint Padding;
            public ulong PathOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EmbeddedUserDataInfo
        {
            public int InstanceId;
            public uint TypeId;
            public uint JsonPathHash;
            public uint DataSize;
            public ulong RszOffset;
        }
    }
}
