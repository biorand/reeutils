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
        private ReadOnlySpan<ResourceInfo> ResourceInfoList => data.Get<ResourceInfo>(Header.ResourceOffset, Header.ResourceCount);
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

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
            var gameObjectInfoList = GameObjectInfoList.ToImmutableArray();
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

                return new RszGameObject(default, null, settings, components.ToImmutable(), children.ToImmutable());
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

            public Builder(RszTypeRepository repository, PfbFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                RszVersion = instance.Rsz.Version;
                Resources = instance.Resources.ToList();
                Scene = instance.ReadScene(repository);
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
                var gameObjects = new List<GameObjectInfo>();
                var objectList = ImmutableArray.CreateBuilder<IRszNode>();
                Traverse(-1, Scene);

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

                bw.Align(16);
                var gameObjectRefOffset = ms.Position;

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
                bw.Write(0);
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

                int AddObject(IRszNode node)
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
