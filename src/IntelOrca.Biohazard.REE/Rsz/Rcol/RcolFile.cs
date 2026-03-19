using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

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

        private ReadOnlySpan<GroupInfo> GroupInfoList => data.Get<GroupInfo>(Header.GroupsPtrOffset, (uint)Header.NumGroups);
        private ReadOnlySpan<RequestSetInfo> RequestSetInfoList => data.Get<RequestSetInfo>(Header.RequestSetOffset, (uint)Header.NumRequestSets);
        private ReadOnlySpan<IgnoreTag> IgnoreTagList => data.Get<IgnoreTag>(Header.IgnoreTagOffset, (uint)Header.NumIgnoreTags);
        public List<string>? AutoGenerateJointDescs { get; private set; } = [];

        public RszFile Rsz => new(data.Slice((int)Header.DataOffset));

        public int InstanceCount => Rsz.InstanceCount;

        public ImmutableArray<RcolGroup> Groups
        {
            get
            {
                return []; // TODO
            }
        }

        public ImmutableArray<RequestSet> RequestSets
        {
            get
            {
                return []; // TODO
            }
        }

        public ImmutableArray<IgnoreTag> IgnoreTags
        {
            get
            {
                return []; // TODO
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GroupInfo
        {
            public Guid Guid;
            public string Name;
            public uint NameHash;
            public uint Padding;
            public int NumShapes;
            public int NumMaskGuids;
            public long ShapesOffset;
            public int LayerIndex;
            public uint MaskBits;
            public long MaskGuidsOffset;
            public Guid LayerGuid;

            public List<Guid> MaskGuids { get; set; }

            internal long ShapesOffsetStart;

            public void Read(BinaryReader reader)
            {
                Guid = new Guid(reader.ReadBytes(16));
                Name = reader.ReadString();
                NameHash = reader.ReadUInt32();
                reader.ReadBytes(4); // Padding
                NumShapes = reader.ReadInt32();
                NumMaskGuids = reader.ReadInt32();

                ShapesOffset = reader.ReadInt64();
                LayerIndex = reader.ReadInt32();
                MaskBits = reader.ReadUInt32();
                MaskGuidsOffset = reader.ReadInt32();
                LayerGuid = new Guid(reader.ReadBytes(16));

                reader.BaseStream.Seek(MaskGuidsOffset, SeekOrigin.Begin);
                for (int i = 0; i < NumMaskGuids; i++)
                {
                    MaskGuids.Add(new Guid(reader.ReadBytes(16)));
                }
            }

            public void Write(BinaryWriter writer, int numShapesOnWrite)
            {
                NameHash = (uint)MurMur3.HashData(Name);
                NumMaskGuids = MaskGuids.Count;
                writer.Write(Guid.ToByteArray());
                writer.WriteOffsetString(Name);
                writer.Write(NameHash);
                writer.WriteZeros(4); // Padding
                writer.Write(numShapesOnWrite);
                writer.Write(NumMaskGuids);

                ShapesOffsetStart = writer.BaseStream.Position;
                writer.Write(ShapesOffset);
                writer.Write(LayerIndex);
                writer.Write(MaskBits);

                foreach (var guid in MaskGuids)
                {
                    writer.Write(guid.ToByteArray());
                }
                writer.Write(MaskGuidsOffset);
                writer.Write(LayerGuid.ToByteArray());
            }

            public override readonly string ToString() => Name;
        }

        public class RcolGroup
        {
            public GroupInfo Info { get; } = new();

            public List<RcolShape> Shapes { get; } = new();

            public void ReadInfo(BinaryReader reader)
            {
                Info.Read(reader);
            }

            public void Read(BinaryReader reader)
            {
                Shapes.Clear();
                if (Info.NumShapes > 0)
                {
                    reader.BaseStream.Position = Info.ShapesOffset;
                    for (int i = 0; i < Info.NumShapes; i++)
                    {
                        var shape = new RcolShape();
                        shape.Read(reader);
                        Shapes.Add(shape);
                    }
                }
            }

            public void Write(BinaryWriter writer)
            {
                if (Info.NumShapes > 0)
                {
                    long shapesOffset = writer.BaseStream.Position;

                    if (Info.ShapesOffsetStart > 0)
                    {
                        long returnPos = writer.BaseStream.Position;
                        writer.BaseStream.Seek(shapesOffset, SeekOrigin.Begin);
                        writer.Write(shapesOffset);
                        writer.BaseStream.Seek(returnPos, SeekOrigin.Begin);
                    }
                    else
                    {
                        throw new InvalidOperationException("Should WriteInfo first");
                    }

                    foreach (var shape in Shapes)
                    {
                        shape.Write(writer);
                    }
                }
            }

            public override string ToString() => Info.Name;
        }

        public enum ShapeType
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

        [StructLayout(LayoutKind.Sequential)]
        public struct RcolShapeInfo
        {
            public Guid Guid;
            public string Name;
            public uint NameHash;
            public int UserDataIndex;
            public int LayerIndex;
            public int Attribute;
            public uint SkipIdBits;
            public uint IgnoreTagBits;
            public string primaryJointNameStr;
            public string secondaryJointNameStr;
            public uint PrimaryJointNameHash;
            public uint SecondaryJointNameHash;
            public ShapeType shapeType;

            public void Read(BinaryReader reader)
            {
                Guid = new Guid(reader.ReadBytes(16));
                Name = reader.ReadString();
                NameHash = reader.ReadUInt32();
                UserDataIndex = reader.ReadInt32();
                LayerIndex = reader.ReadInt32();
                Attribute = reader.ReadInt32();

                SkipIdBits = reader.ReadUInt32();
                IgnoreTagBits = reader.ReadUInt32();
                primaryJointNameStr = reader.ReadString();
                secondaryJointNameStr = reader.ReadString();
                PrimaryJointNameHash = reader.ReadUInt32();
                SecondaryJointNameHash = reader.ReadUInt32();
                shapeType = (ShapeType)reader.ReadUInt32();
                reader.ReadBytes(4);
            }

            public readonly void Write(BinaryWriter writer)
            {
                writer.Write(Guid);
                writer.WriteOffsetString(Name);
                writer.Write(MurMur3.HashData(Name));
                writer.Write(UserDataIndex);
                writer.Write(LayerIndex);
                writer.Write(Attribute);
                writer.Write(SkipIdBits);
                writer.Write(IgnoreTagBits);
                writer.WriteOffsetString(primaryJointNameStr);
                writer.WriteOffsetString(secondaryJointNameStr);
                writer.Write(MurMur3.HashData(primaryJointNameStr));
                writer.Write(MurMur3.HashData(secondaryJointNameStr));
                writer.Write((uint)shapeType);
            }
        }

        public class RcolShape
        {
            public RcolShapeInfo Info { get; } = new();
            public object? shape = new AABB(Vector3.Zero, Vector3.One);

            public RszInstance? DefaultInstance { get; set; }

            public void UpdateShapeType()
            {
                switch (Info.shapeType)
                {
                    case ShapeType.Aabb: if (shape is not AABB) shape = new AABB(new Vector3(-0.5f), new Vector3(0.5f)); break;
                    case ShapeType.Sphere or ShapeType.ContinuousSphere: if (shape is not Sphere) shape = new Sphere(); break;
                    case ShapeType.Capsule or ShapeType.ContinuousCapsule: if (shape is not Capsule) shape = new Capsule(); break;
                    case ShapeType.Box: if (shape is not OBB) shape = new OBB(); break;
                    case ShapeType.Area: if (shape is not Area) shape = new Area(); break;
                    case ShapeType.Triangle: if (shape is not Triangle) shape = new Triangle(); break;
                    case ShapeType.Cylinder: if (shape is not Cylinder) shape = new Cylinder(); break;
                    default:
                        throw new Exception($"Illegal ShapeType '{Info.shapeType}' for shape '{Info.Name}'");
                }
            }

            public void Read(BinaryReader reader)
            {
                Info.Read(reader);

                shape = Info.shapeType switch
                {
                    ShapeType.Aabb => reader.ByteToType<AABB>(),
                    ShapeType.Sphere => reader.ByteToType<Sphere>(),
                    ShapeType.Capsule => reader.ByteToType<Capsule>(),
                    ShapeType.Box => reader.ByteToType<OBB>(),
                    ShapeType.Area => reader.ByteToType<Area>(),
                    ShapeType.Triangle => reader.ByteToType<Triangle>(),
                    ShapeType.Cylinder => reader.ByteToType<Cylinder>(),
                    ShapeType.ContinuousSphere => reader.ByteToType<Sphere>(),
                    ShapeType.ContinuousCapsule => reader.ByteToType<Capsule>(),
                    _ => throw new Exception("Unsupported RCOL shape type " + Info.shapeType),
                };

                reader.BaseStream.Position += sizeof(float) * 4 * 5;
            }

            public void Write(BinaryWriter writer)
            {
                Info.Write(writer);

                switch (Info.shapeType)
                {
                    case ShapeType.Aabb: writer.Write((AABB)shape!); break;
                    case ShapeType.Sphere: writer.Write((Sphere)shape!); break;
                    case ShapeType.Capsule: writer.Write((Capsule)shape!); break;
                    case ShapeType.Box: writer.Write((OBB)shape!); break;
                    case ShapeType.Area: writer.Write((Area)shape!); break;
                    case ShapeType.Triangle: writer.Write((Triangle)shape!); break;
                    case ShapeType.Cylinder: writer.Write((Cylinder)shape!); break;
                    case ShapeType.ContinuousSphere: writer.Write((Sphere)shape!); break;
                    case ShapeType.ContinuousCapsule: writer.Write((Capsule)shape!); break;
                    default: throw new Exception("Unsupported RCOL shape type " + Info.shapeType);
                }
                writer.BaseStream.Position += sizeof(float) * 4 * 5;
            }

            public override string ToString() => (shape == null ? Info.Name : $"{Info.Name} <{shape}>");
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

        [StructLayout(LayoutKind.Sequential)]
        public struct IgnoreTag
        {
            public string Tag;
            public uint Hash;

            public void Read(BinaryReader reader)
            {
                Tag = reader.ReadString();
                Hash = reader.ReadUInt32();

                if (Hash != MurMur3.HashData(Tag))
                {
                    throw new Exception($"IgnoreTag hash mismatch for tag '{Tag}' (expected {Hash:X8}, got {MurMur3.HashData(Tag):X8})");
                }

                reader.ReadBytes(4);
            }

            public void Write(BinaryWriter writer)
            {
                writer.WriteOffsetString(Tag);
                writer.Write(Hash);
                writer.WriteZeros(4);
            }

            public override readonly string ToString() => Tag;
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
            public RszFile Rsz { get; }
            public List<RcolGroup> Groups { get; } = new();
            public List<RequestSet> RequestSets { get; } = new();
            public List<IgnoreTag> IgnoreTags { get; } = new();
            public List<string>? AutoGenerateJointDescs { get; } = new();

            public Builder(RszTypeRepository repository, RcolFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                Rsz = instance.Rsz;
                RszVersion = instance.Rsz.Version;
                Groups = instance.Groups.ToList();
                RequestSets = instance.RequestSets.ToList();
                IgnoreTags = instance.IgnoreTags.ToList();
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

            public Builder AddIgnoreTag(IgnoreTag tag)
            {
                IgnoreTags.Add(tag);
                return this;
            }

            public RcolFile Build()
            {
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // Reserve space for header
                bw.WriteZeros(RcolHeader.GetSize(Version));

                foreach (var requestSet in RequestSets)
                {
                    if(requestSet.Group == null)
                    {
                        throw new InvalidDataException($"Request Set {requestSet.Info.Name} is missing shape group assignment");
                    }

                    requestSet.Instance ??= CreateDefaultUserdata();

                    while(requestSet.ShapeUserdata.Count < requestSet.Group.Shapes.Count)
                    {
                        requestSet.ShapeUserdata.Add(CreateDefaultUserdata());
                    }
                    
                    if(requestSet.ShapeUserdata.Count > requestSet.Group.Shapes.Count)
                    {
                        requestSet.ShapeUserdata.RemoveRange(requestSet.Group.Shapes.Count, requestSet.ShapeUserdata.Count);
                    }
                }

                var setGroupDict = new Dictionary<RcolGroup, List<RequestSet>>();
                foreach (var requestSet in RequestSets)
                {
                    if (!setGroupDict.TryGetValue(requestSet.Group!, out var setlist))
                    {
                        setGroupDict[requestSet.Group!] = setlist = new();
                    }
                    setlist.Add(requestSet);
                    if (requestSet.Group != null && !Groups.Contains(requestSet.Group))
                    {
                        Groups.Add(requestSet.Group);
                    }
                }

                int shapeCount = 0;
                //foreach (var group in Groups)
                //{
                //    if (!setGroupDict.TryGetValue(group, out var setlist))
                //    {
                //        foreach (var shape in group.Shapes)
                //        {
                //            shape.Info.UserDataIndex = RSZ.ObjectList.Count;
                //            Rsz.AddToObjectTable(shape.DefaultInstance ??= CreateDefaultUserdata());
                //        }
                //        continue;
                //    }

                    
                //    foreach (var set in setlist)
                //    {
                //        set.Info.ShapeOffset = shapeCount;
                //        foreach (var ud in set.ShapeUserdata)
                //        {
                //            if (set == setlist[0])
                //            {
                //                group.Shapes[shapeCount].Info.UserDataIndex = RSZ.ObjectList.Count;
                //            }

                //            Rsz.AddToObjectTable(ud);
                //            shapeCount++;
                //        }
                //    }
                //}

                int groupCount = Groups.Count;
                int requestSetCount = RequestSets.Count;
                int ignoreTagsCount = IgnoreTags.Count;
                int autoGenerateJointCount = AutoGenerateJointDescs?.Count ?? 0;
                uint maxRequestSetId = requestSetCount == 0 ? uint.MaxValue : (uint)RequestSets.Max(s => s.Info.ID);
                int numUserData = RequestSets.Sum(s => s.ShapeUserdata.Count);

                foreach (var group in Groups)
                {
                    group.Info.Write(bw, group.Shapes.Count);
                }

                foreach (var group in Groups)
                {
                    group.Write(bw);
                    shapeCount += group.Shapes.Count;
                }

                // Header
                ms.Position = 0;
                bw.Write(MAGIC);

                bw.Write(groupCount);
                bw.Write(shapeCount);
                bw.Write(groupCount); // Unknown count
                bw.Write(requestSetCount);
                bw.Write(maxRequestSetId);
                bw.Write(ignoreTagsCount);
                bw.Write(autoGenerateJointCount);
                bw.Write(Rsz.InstanceCount);
                bw.Write(0); // Status
                bw.Write(0); // ukn1
                bw.Write(0); // ukn2

                //bw.Write(groupsPtrOffset);
                //bw.Write(dataOffset);
                //bw.Write(requestSetOffset);
                //    bw.Write(ignoreTagOffset);
                //    bw.Write(autoGenerateJointDescOffset);
                //    bw.Write(unknPtr0);
                //    bw.Write(unknPtr1);

                return new RcolFile(Version, ms.ToArray());
            }
        }
    }
}
