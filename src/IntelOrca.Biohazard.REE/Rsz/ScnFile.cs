using System;
using System.Collections.Immutable;
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
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        public RszScene ReadHierarchy(RszTypeRepository repository)
        {
            var objectList = Rsz.ReadObjectList(repository);
            var folderInfoList = FolderInfoList.ToImmutableArray();
            var gameObjectInfoList = GameObjectInfoList.ToImmutableArray();
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
                var settings = (RszStructNode)objectList[folderInfoList[id].ObjectId].Value!;
                var children = ImmutableArray.CreateBuilder<IRszSceneNode>();
                for (var i = 0; i < folderInfoList.Length; i++)
                {
                    if (folderInfoList[i].ParentId == id)
                    {
                        children.Add(BuildFolder(i));
                    }
                }
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == id)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }
                return new RszFolder(settings, children.ToImmutable());
            }

            RszGameObject BuildGameObject(int id)
            {
                var info = gameObjectInfoList[id];
                var settings = (RszStructNode)objectList[info.ObjectId].Value!;

                var components = ImmutableArray.CreateBuilder<IRszNode>();
                for (var i = 0; i < info.ComponentCount; i++)
                {
                    components.Add(objectList[info.ObjectId + 1 + i].Value!);
                }

                var children = ImmutableArray.CreateBuilder<RszGameObject>();
                for (var i = 0; i < gameObjectInfoList.Length; i++)
                {
                    if (gameObjectInfoList[i].ParentId == info.ObjectId)
                    {
                        children.Add(BuildGameObject(i));
                    }
                }

                return new RszGameObject(info.Guid, settings, components.ToImmutable(), children.ToImmutable());
            }
        }

        public Builder ToBuilder(RszTypeRepository repository)
        {
            var root = ReadHierarchy(repository);
            return new Builder(this);
        }

        public class Builder
        {
            public int Version { get; }

            public Builder(ScnFile instance)
            {
                Version = instance.Version;
            }

            public ScnFile Build()
            {
                return new ScnFile(Version, new byte[0]);
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
    }
}
