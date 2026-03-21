using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz.Rcol
{
    // Adapted from https://github.com/kagenocookie/RE-Engine-Lib/blob/master/REE-Lib/RszFile/RcolFile.cs.

    /// <summary>
    /// RequestSet Collider (RCOL) file.
    /// Contains hitboxes, damage values, stun, impact, damage type, special attack flags and other hitbox-related data.
    /// TODO: Fully support all versions. Currently only fully supports version 20.
    /// </summary>
    /// <param name="version">File version. Valid values: 2, 11, 20, >= 25</param>
    /// <param name="data">RCOL file data</param>
    public sealed class RcolFile(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x4C4F4352;

        public ReadOnlyMemory<byte> Data => data;

        public int Version => version;

        private readonly RcolHeader Header = new(version, data[..RcolHeader.GetSize(version)]);

        private ReadOnlySpan<GroupInfo> GroupInfoList
        {
            get
            {
                var size = GroupInfo.GetSize(Version);
                var count = Header.NumGroups;
                var result = new GroupInfo[count];
                var offset = Header.GroupsPtrOffset;
                for (var i = 0; i < count; i++)
                {
                    result[i] = new GroupInfo(version, data.Slice((int)offset, size));
                    offset += size;
                }
                return result;
            }
        }

        private ReadOnlySpan<IgnoreTagInfo> IgnoreTags => Data.Get<IgnoreTagInfo>(Header.IgnoreTagOffset, Header.NumIgnoreTags);

        private ReadOnlySpan<RequestSetInfo> RequestSetInfoList => data.Get<RequestSetInfo>(Header.RequestSetOffset, (uint)Header.NumRequestSets);
        public List<string>? AutoGenerateJointDescs { get; private set; } = [];

        public RszFile Rsz => new(data[(int)Header.DataOffset..]);

        public int InstanceCount => Rsz.InstanceCount;

        public ImmutableArray<RequestSet> RequestSets
        {
            get
            {
                return []; // TODO
            }
        }

        public readonly struct GroupInfo(int version, ReadOnlyMemory<byte> data)
        {
            public Guid Guid => new(data.Span.Slice(0, 16));
            public long NameOffset => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(16, 8));

            public int NumShapes
            {
                get
                {
                    if (version >= 25)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(28, 4));
                    else if (version >= 3)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(32, 4));
                    else
                        return BinaryPrimitives.ReadInt16LittleEndian(data.Span.Slice(30, 2));
                }
            }

            public int NumExtraShapes
            {
                get
                {
                    if (version >= 25)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(32, 4));
                    return 0;
                }
            }

            public int NumMaskGuids
            {
                get
                {
                    if (version >= 25)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(36, 4));
                    else if (version >= 3)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(36, 4));
                    else
                        return 0;
                }
            }

            public long ShapesOffset
            {
                get
                {
                    if (version >= 3)
                        return BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(40, 8));
                    return BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(32, 8));
                }
            }

            public int LayerIndex
            {
                get
                {
                    if (version >= 3)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(48, 4));
                    return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(40, 4));
                }
            }

            public int MaskBits
            {
                get
                {
                    if (version >= 3)
                        return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(52, 4));
                    return BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(44, 4));
                }
            }

            public long MaskGuidOffset
            {
                get
                {
                    if (version >= 3)
                        return BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(56, 8));
                    return 0;
                }
            }

            public Guid LayerGuid
            {
                get
                {
                    if (version >= 3)
                        return new(data.Span.Slice(64, 16));
                    return default;
                }
            }

            public static int GetSize(int version)
            {
                if (version >= 3)
                    return 40 + (8 + 4 + 4) + (8 + 16);
                return 32 + (8 + 4 + 4);
            }
        }

        public readonly struct RcolShapeInfo(int version, ReadOnlyMemory<byte> data)
        {
            public Guid Guid => new(data.Span.Slice(0, 16));
            public long NameOffset => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(16, 8));
            public int NameHash => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(24, 4));
            public int UserDataIndex => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(28, 4));
            public int LayerIndex => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(32, 4));
            public int Attribute => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(36, 4));

            public int SkipIdBits => version >= 3 ? BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(40, 4)) : 0;
            public RcolShapeType Type => version switch
            {
                _ when version >= 27 => (RcolShapeType)BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(44, 8)),
                _ when version >= 3 => (RcolShapeType)BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(72, 8)),
                _ => (RcolShapeType)BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(64, 8)),
            };
            public int IgnoreTagBits => version switch
            {
                _ when version >= 27 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(48, 8)),
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(44, 8)),
                _ => 0
            };
            public long PrimaryJointNameOffset => version switch
            {
                _ when version >= 27 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(52, 8)),
                _ when version >= 3 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(48, 8)),
                _ => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(40, 8)),
            };
            public long SecondaryJointNameOffset => version switch
            {
                _ when version >= 27 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(60, 8)),
                _ when version >= 3 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(56, 8)),
                _ => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(48, 8)),
            };
            public int PrimaryJointNameHash => version switch
            {
                _ when version >= 27 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(68, 4)),
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(64, 4)),
                _ => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(56, 4)),
            };
            public int SecondaryJointNameHash => version switch
            {
                _ when version >= 27 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(72, 4)),
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(68, 4)),
                _ => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(60, 4)),
            };

            public ReadOnlySpan<byte> Data => version switch
            {
                _ when version >= 27 => data.Span.Slice(0x60, 0x50),
                _ => data.Span.Slice(0x50, 0x50),
            };

            public static int GetSize(int version) => version switch
            {
                _ when version >= 28 => 0x60 + 0x50,
                _ when version >= 27 => 0x50 + 0x50,
                _ when version >= 3 => 0x50 + 0x50,
                _ => 0x50 + 0x50,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RequestSetInfo
        {
            public uint ID;
            public int GroupIndex;
            public int ShapeOffset;
            public int status;
            public int requestSetUserdataIndex;
            public int groupUserdataIndexStart;
            public int requestSetIndex;
            public string Name;
            public uint NameHash;
            public string KeyName;
            public uint KeyHash;

            public void Read(BinaryReader reader)
            {
                ID = reader.ReadUInt32();
                GroupIndex = reader.ReadInt32();
                ShapeOffset = reader.ReadInt32();
                status = reader.ReadInt32();
                Name = reader.ReadString();
                NameHash = reader.ReadUInt32();
                reader.ReadBytes(4);
                KeyName = reader.ReadString();
                KeyHash = reader.ReadUInt32();
                reader.ReadBytes(4);
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(ID);
                writer.Write(GroupIndex);
                writer.Write(ShapeOffset);
                writer.Write(status);
                writer.WriteOffsetString(Name);
                writer.Write(MurMur3.HashData(Name));
                writer.WriteZeros(4);
                writer.WriteOffsetString(KeyName);
                writer.Write(MurMur3.HashData(KeyName));
                writer.WriteZeros(4);
            }

            public override string ToString() => Name;
        }

        public class RequestSet(RequestSetInfo? info = null)
        {
            public RequestSetInfo Info { get; set; } = info ?? new();
            public RcolGroup? Group { get; set; }
            public RszObjectNode? Instance { get; set; }
            public List<RszObjectNode> ShapeUserdata { get; set; } = new();

            public override string ToString() => $"[{Info.ID:00000000}] {Info.Name}";
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        public struct IgnoreTagInfo
        {
            public long NameOffset;
            public int NameHash;
        }

        private ReadOnlySpan<RcolShapeInfo> GetShapes(long offset, int count)
        {
            var size = RcolShapeInfo.GetSize(Version);
            var result = new RcolShapeInfo[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = new RcolShapeInfo(version, data.Slice((int)offset, size));
                offset += size;
            }
            return result;
        }

        private string GetString(long offset) => offset != 0 ? Data.ReadWString((int)offset) : string.Empty;

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public int Version { get; }
            public int RszVersion { get; }
            public List<RcolGroup> Groups { get; } = new();
            public List<RequestSet> RequestSets { get; } = new();
            public List<string> IgnoreTags { get; } = new();
            public List<string>? AutoGenerateJointDescs { get; } = new();

            public Builder(RszTypeRepository repository, RcolFile rcol)
            {
                Repository = repository;
                Version = rcol.Version;
                RszVersion = rcol.Rsz.Version;

                var rsz = rcol.Rsz.ToBuilder(repository);
                foreach (var groupInfo in rcol.GroupInfoList)
                {
                    var group = new RcolGroup()
                    {
                        Guid = groupInfo.Guid,
                        Name = rcol.GetString(groupInfo.NameOffset),
                        LayerIndex = groupInfo.LayerIndex,
                        MaskBits = groupInfo.MaskBits,
                        LayerGuid = groupInfo.LayerGuid,
                        MaskGuids = rcol.Data.Get<Guid>(groupInfo.MaskGuidOffset, groupInfo.NumMaskGuids).ToList()
                    };
                    foreach (var shapeInfo in rcol.GetShapes(groupInfo.ShapesOffset, groupInfo.NumShapes))
                    {
                        var shape = new RcolShape()
                        {
                            Guid = shapeInfo.Guid,
                            Type = shapeInfo.Type,
                            Name = rcol.GetString(shapeInfo.NameOffset),
                            Attribute = shapeInfo.Attribute,
                            SkipIdBits = shapeInfo.SkipIdBits,
                            IgnoreTagBits = shapeInfo.IgnoreTagBits,
                            LayerIndex = shapeInfo.LayerIndex,
                            PrimaryJointName = rcol.GetString(shapeInfo.PrimaryJointNameOffset),
                            SecondaryJointName = rcol.GetString(shapeInfo.SecondaryJointNameOffset),
                            UserData = rsz.Objects[shapeInfo.UserDataIndex],
                            Data = shapeInfo.Data.ToImmutableArray()
                        };
                        group.Shapes.Add(shape);
                    }
                    Groups.Add(group);
                }

                RequestSets = rcol.RequestSets.ToList();

                foreach (var ignoreTag in rcol.IgnoreTags)
                {
                    IgnoreTags.Add(rcol.GetString(ignoreTag.NameOffset));
                }
            }

            public RszObjectNode CreateDefaultUserdata() =>
                Repository.Create("via.physics.RequestSetColliderUserData");

            public Builder AddGroup(RcolGroup group)
            {
                Groups.Add(group);
                return this;
            }

            public Builder AddRequestSet(RequestSet requestSet)
            {
                RequestSets.Add(requestSet);
                return this;
            }

            public RcolFile Build()
            {
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                var stringTable = new StringPoolBuilder(ms);
                var userDataObjects = new List<RszObjectNode>();

                bw.WriteZeros(RcolHeader.GetSize(Version));
                bw.Align(16);

                var groupsOffset = ms.Position;
                var shapesOffset = groupsOffset + GroupInfo.GetSize(Version) * Groups.Count;
                var rszOffset = (long)0x07F0;
                var maskGuidsOffset = (long)0x1F90;
                var ignoreTagsOffset = (long)0x2040;

                var currentGroupOffset = groupsOffset;
                var currentShapesOffset = shapesOffset;
                var currentMaskGuidsOffset = maskGuidsOffset;

                var shapeCount = 0;
                var maskGuidCount = 0;

                // Groups
                foreach (var group in Groups)
                {
                    ms.Position = currentGroupOffset;

                    bw.Write(group.Guid);
                    stringTable.WriteStringOffset64(group.Name);
                    bw.Write(MurMur3.HashData(group.Name));
                    if (Version >= 25)
                    {
                        bw.Write(group.Shapes.Count);
                        bw.Write(group.ExtraShapes.Count);
                        bw.Write(group.MaskGuids.Count);
                    }
                    else if (Version >= 3)
                    {
                        bw.Write(0);
                        bw.Write(group.Shapes.Count);
                        bw.Write(group.MaskGuids.Count);
                    }
                    else
                    {
                        bw.Write((short)0);
                        bw.Write((short)group.Shapes.Count);
                    }
                    bw.Write(currentShapesOffset);
                    bw.Write(group.LayerIndex);
                    bw.Write(group.MaskBits);
                    if (Version >= 3)
                    {
                        bw.Write(currentMaskGuidsOffset);
                        bw.Write(group.LayerGuid);
                    }

                    currentGroupOffset = ms.Position;

                    foreach (var shape in group.Shapes)
                    {
                        ms.Position = currentShapesOffset;

                        userDataObjects.Add(shape.UserData!);

                        bw.Write(shape.Guid);
                        stringTable.WriteStringOffset64(shape.Name);
                        bw.Write(MurMur3.HashData(shape.Name));
                        bw.Write(userDataObjects.Count);
                        bw.Write(shape.LayerIndex);
                        bw.Write(shape.Attribute);
                        bw.Write(shape.SkipIdBits);
                        bw.Write(shape.IgnoreTagBits);
                        stringTable.WriteStringOffset64(shape.PrimaryJointName);
                        stringTable.WriteStringOffset64(shape.SecondaryJointName);
                        bw.Write(MurMur3.HashData(shape.PrimaryJointName));
                        bw.Write(MurMur3.HashData(shape.SecondaryJointName));
                        bw.Write(shape.Type);
                        bw.Write(0);
                        bw.Write(shape.Data.AsSpan());

                        currentShapesOffset = ms.Position;
                    }

                    foreach (var maskGuid in group.MaskGuids)
                    {
                        ms.Position = currentMaskGuidsOffset;
                        bw.Write(maskGuid);
                        currentMaskGuidsOffset = ms.Position;
                    }

                    shapeCount += group.Shapes.Count;
                    maskGuidCount += group.MaskGuids.Count;
                }

                // RSZ
                ms.Position = rszOffset;
                var rszBuilder = new RszFile.Builder(Repository, RszVersion)
                {
                    Objects = userDataObjects.ToImmutableArray()
                };
                bw.Write(rszBuilder.Build().Data.Span);

                // Ignore tags
                ms.Position = ignoreTagsOffset;
                foreach (var ignoreTag in IgnoreTags)
                {
                    stringTable.WriteStringOffset64(ignoreTag);
                    bw.Write(MurMur3.HashData(ignoreTag));
                    bw.Write(0);
                }

                // String table
                bw.Align(16);
                ms.Position = 0x2040; // TEMP
                stringTable.WriteStrings();

                // Header
                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(Groups.Count);
                bw.Write(0);
                bw.Write(shapeCount);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(groupsOffset);
                bw.Write(rszOffset);
                bw.Write(0L); // request set
                bw.Write(ignoreTagsOffset);
                bw.Write(0L);
                bw.Write(0L);
                bw.Write(0L);
                bw.Write(0L);

                return new RcolFile(Version, ms.ToArray());
            }
        }
    }

    public class RcolGroup
    {
        public Guid Guid { get; set; }
        public string Name { get; set; } = "";
        public int LayerIndex { get; set; }
        public int MaskBits { get; set; }
        public Guid LayerGuid { get; set; }
        public List<RcolShape> Shapes { get; set; } = [];
        public List<RcolShape> ExtraShapes { get; set; } = [];
        public List<Guid> MaskGuids { get; set; } = [];
    }


    public class RcolShape
    {
        public Guid Guid { get; set; }
        public RcolShapeType Type { get; set; }
        public string Name { get; set; } = "";
        public string PrimaryJointName { get; set; } = "";
        public string SecondaryJointName { get; set; } = "";
        public RszObjectNode? UserData { get; set; }
        public int LayerIndex { get; set; }
        public int Attribute { get; set; }
        public int SkipIdBits { get; set; }
        public int IgnoreTagBits { get; set; }
        public ImmutableArray<byte> Data { get; set; } = [];
    }

    public enum RcolShapeType
    {
        Aabb = 0x0,
        Sphere = 0x1,
        ContinuousSphere = 0x2,
        Capsule = 0x3,
        ContinuousCapsule = 0x4,
        Box = 0x5,
        Mesh = 0x6,
        HeightField = 0x7,
        StaticCompound = 0x8,
        Area = 0x9,
        Triangle = 0xA,
        SkinningMesh = 0xB,
        Cylinder = 0xC,
        DeformableMesh = 0xD,
        Invalid = 0xE,
        Max = 0xF,
    }
}
