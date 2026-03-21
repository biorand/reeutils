using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Extensions;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// RequestSet Collider (RCOL) file.
    /// Contains hitboxes, damage values, stun, impact, damage type, special attack flags and other hitbox-related data.
    /// </summary>
    /// <param name="version">File version. Valid values: 2, 11, 20, >= 25</param>
    /// <param name="data">RCOL file data</param>
    public sealed class RcolFile(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x4C4F4352;

        public int Version => version;
        public ReadOnlyMemory<byte> Data => data;

        private readonly RcolHeader Header = new(version, data[..RcolHeader.GetSize(version)]);

        private ReadOnlySpan<RcolGroupInfo> Groups
        {
            get
            {
                var size = RcolGroupInfo.GetSize(Version);
                var count = Header.NumGroups;
                var result = new RcolGroupInfo[count];
                var offset = Header.GroupsPtrOffset;
                for (var i = 0; i < count; i++)
                {
                    result[i] = new RcolGroupInfo(version, data.Slice((int)offset, size));
                    offset += size;
                }
                return result;
            }
        }

        public RszFile Rsz => new(data.Slice((int)Header.DataOffset, Header.UserDataSize));

        private ReadOnlySpan<RequestSetInfo> RequestSets
        {
            get
            {
                var size = RequestSetInfo.GetSize(Version);
                var count = Header.NumRequestSets;
                var result = new RequestSetInfo[count];
                var offset = Header.RequestSetOffset;
                for (var i = 0; i < count; i++)
                {
                    result[i] = new RequestSetInfo(version, data.Slice((int)offset, size));
                    offset += size;
                }
                return result;
            }
        }

        private ReadOnlySpan<IgnoreTagInfo> IgnoreTags => Data.Get<IgnoreTagInfo>(Header.IgnoreTagOffset, Header.NumIgnoreTags);

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
            public List<RcolGroup> Groups { get; } = [];
            public List<RequestSet> RequestSets { get; } = [];
            public List<string> IgnoreTags { get; } = [];
            public List<string>? AutoGenerateJointDescs { get; } = [];

            public Builder(RszTypeRepository repository, RcolFile rcol)
            {
                Repository = repository;
                Version = rcol.Version;
                RszVersion = rcol.Rsz.Version;

                var rsz = rcol.Rsz.ToBuilder(repository);
                var groupInfos = rcol.Groups;
                for (var i = 0; i < groupInfos.Length; i++)
                {
                    var groupInfo = groupInfos[i];
                    var reuseOffsetForMaskGuids = false;
                    for (var j = 0; j < i; j++)
                    {
                        if (groupInfo.MaskGuidOffset == groupInfos[j].MaskGuidOffset)
                        {
                            reuseOffsetForMaskGuids = true;
                            break;
                        }
                    }

                    var group = new RcolGroup()
                    {
                        Guid = groupInfo.Guid,
                        Name = rcol.GetString(groupInfo.NameOffset),
                        LayerIndex = groupInfo.LayerIndex,
                        MaskBits = groupInfo.MaskBits,
                        LayerGuid = groupInfo.LayerGuid,
                        MaskGuids = rcol.Data.Get<Guid>(groupInfo.MaskGuidOffset, groupInfo.NumMaskGuids).ToList(),
                        ReuseOffsetForMaskGuids = reuseOffsetForMaskGuids
                    };
                    foreach (var shapeInfo in rcol.GetShapes(groupInfo.ShapesOffset, groupInfo.NumShapes))
                    {
                        group.Shapes.Add(GetShape(shapeInfo));
                    }
                    Groups.Add(group);
                }

                var requestSetInfos = rcol.RequestSets;
                for (var i = 0; i < requestSetInfos.Length; i++)
                {
                    var requestSetInfo = requestSetInfos[i];
                    var groupInfo = groupInfos[requestSetInfo.GroupIndex];
                    var group = Groups[requestSetInfo.GroupIndex];
                    var requestShape = new RequestSet()
                    {
                        Id = requestSetInfo.Id,
                        Group = group,
                        Shape = null!,
                        Name = rcol.GetString(requestSetInfo.NameOffset),
                        Key = rcol.GetString(requestSetInfo.KeyOffset),
                        Status = requestSetInfo.Status,
                        UserData = rcol.Version >= 25
                            ? rsz.Objects[requestSetInfo.UserDataIndex]
                            : rsz.Objects[i]
                    };

                    var shapeInfos = rcol.GetShapes(groupInfo.ShapesOffset, groupInfo.NumShapes);
                    foreach (var shapeInfo in shapeInfos)
                    {
                        var objectIndex = shapeInfo.UserDataIndex + requestSetInfo.ShapeOffset;
                        var userData = rsz.Objects[objectIndex];
                        requestShape.ShapeUserData = requestShape.ShapeUserData.Add(userData);
                    }
                    RequestSets.Add(requestShape);
                }

                foreach (var ignoreTag in rcol.IgnoreTags)
                {
                    IgnoreTags.Add(rcol.GetString(ignoreTag.NameOffset));
                }

                RcolShape GetShape(RcolShapeInfo shapeInfo)
                {
                    return new RcolShape()
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
                var stringTable = new StringPoolBuilder(ms, reuseOffsets: true);

                // Reserve space for header
                bw.WriteZeros(RcolHeader.GetSize(Version));
                bw.Align(16);

                // Reserve space for group table
                var groupsOffset = ms.Position;
                bw.WriteZeros(RcolGroupInfo.GetSize(Version) * Groups.Count);

                // Reserve space for shape table
                var shapesOffset = ms.Position;
                var totalShapes = Groups.Sum(x => x.Shapes.Count);
                bw.WriteZeros(RcolShapeInfo.GetSize(Version) * totalShapes);

                // Write the rsz
                var rszOffset = ms.Position;
                var userDataObjects = new List<RszObjectNode>();
                var shapeToUserDataOffset = new Dictionary<RcolShape, int>();
                var requestSetShapeOffset = new Dictionary<RequestSet, int>();
                foreach (var requestSet in RequestSets)
                {
                    userDataObjects.Add(requestSet.UserData!);
                }
                foreach (var group in Groups)
                {
                    for (var shapeIndex = 0; shapeIndex < group.Shapes.Count; shapeIndex++)
                    {
                        var shape = group.Shapes[shapeIndex];
                        var shapeUserDataIndex = userDataObjects.Count;

                        // Default via.physics.RequestSetColliderUserData
                        shapeToUserDataOffset[shape] = userDataObjects.Count;
                        userDataObjects.Add(shape.UserData!);

                        // All additional via.physics.RequestSetColliderUserData
                        foreach (var requestSet in RequestSets)
                        {
                            if (requestSet.Group == group)
                            {
                                var requestShapeUserData = requestSet.ShapeUserData[shapeIndex];

                                // Add request shape data if different from default shape data
                                if (shape.UserData != requestShapeUserData)
                                {
                                    userDataObjects.Add(requestShapeUserData);
                                }
                                requestSetShapeOffset[requestSet] = userDataObjects.Count - 1 - shapeUserDataIndex;
                            }
                        }
                    }
                }
                var rszBuilder = new RszFile.Builder(Repository, RszVersion)
                {
                    AlignOffset = ms.Position,
                    Objects = userDataObjects.ToImmutableArray()
                };
                var rsz = rszBuilder.Build();
                bw.Write(rsz.Data.Span);

                // Reserve space for request set table
                bw.Align(16);
                var requestSetOffset = ms.Position;
                bw.WriteZeros(RequestSets.Count * RequestSetInfo.GetSize(Version));

                // Mask guids
                bw.Align(16);
                var maskGuidsOffset = ms.Position;
                foreach (var group in Groups)
                {
                    foreach (var maskGuid in group.MaskGuids)
                    {
                        bw.Write(maskGuid);
                    }
                }

                // Reserve space for ignore tags
                bw.Align(16);
                var ignoreTagsOffset = ms.Position;
                bw.WriteZeros(IgnoreTags.Count * Marshal.SizeOf<IgnoreTagInfo>());

                var autoGenerateJointDescOffset = ms.Position;
                var unkPtr0 = ms.Position;
                var unkPtr1 = ms.Position;
                var stringTableOffset = ms.Position;

                // Go back and write reserved areas
                // Header
                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(Groups.Count);
                bw.Write(totalShapes);
                bw.Write(Math.Max(0, RequestSets.Count - 1));
                bw.Write(RequestSets.Count);
                bw.Write(RequestSets.Count > 0 ? RequestSets.Max(x => x.Id) : 0);
                bw.Write(IgnoreTags.Count);
                bw.Write(0); // autogeneratejoints
                bw.Write(rsz.Data.Length);
                bw.Write(0); // status
                bw.Write(0);
                bw.Write(0);
                bw.Write(groupsOffset);
                bw.Write(rszOffset);
                bw.Write(requestSetOffset);
                bw.Write(ignoreTagsOffset);
                bw.Write(autoGenerateJointDescOffset);
                bw.Write(unkPtr0);
                bw.Write(unkPtr1);

                // Groups
                ms.Position = groupsOffset;

                var maskGuidCount = 0;
                var groupShapeOffset = shapesOffset;
                for (var i = 0; i < Groups.Count; i++)
                {
                    var group = Groups[i];
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
                    bw.Write(groupShapeOffset);
                    bw.Write(group.LayerIndex);
                    bw.Write(group.MaskBits);
                    if (Version >= 3)
                    {
                        var maskGuidListOffset = maskGuidsOffset + (maskGuidCount * Marshal.SizeOf<Guid>());
                        if (group.ReuseOffsetForMaskGuids)
                        {
                            var offset = maskGuidsOffset;
                            for (var j = 0; j < i; j++)
                            {
                                var previousList = Groups[j].MaskGuids;
                                if (group.MaskGuids.SequenceEqual(previousList))
                                {
                                    maskGuidListOffset = offset;
                                    break;
                                }
                                offset += previousList.Count * Marshal.SizeOf<Guid>();
                            }
                        }

                        bw.Write(maskGuidListOffset);
                        bw.Write(group.LayerGuid);
                        maskGuidCount += group.MaskGuids.Count;
                    }
                    var nextGroupPosition = ms.Position;

                    // Shapes
                    ms.Position = groupShapeOffset;
                    foreach (var shape in group.Shapes)
                    {
                        bw.Write(shape.Guid);
                        stringTable.WriteStringOffset64(shape.Name);
                        bw.Write(MurMur3.HashData(shape.Name));
                        bw.Write(shapeToUserDataOffset[shape]);
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
                    }
                    groupShapeOffset = ms.Position;
                    ms.Position = nextGroupPosition;
                }

                ms.Position = requestSetOffset;
                foreach (var requestSet in RequestSets)
                {
                    if (Version >= 25)
                    {
                        bw.Write(requestSet.Id);
                        bw.Write(Groups.IndexOf(requestSet.Group));
                        // bw.Write(requestSet.UserData);
                        // bw.Write(requestSet.Group.UserData);
                        bw.Write(requestSet.Status);
                        // bw.Write(requestSet.RequestSetIndex);
                        stringTable.WriteStringOffset64(requestSet.Name);
                        stringTable.WriteStringOffset64(requestSet.Key);
                        bw.Write(MurMur3.HashData(requestSet.Name));
                        bw.Write(MurMur3.HashData(requestSet.Key));
                    }
                    else if (Version >= 3)
                    {
                        bw.Write(requestSet.Id);
                        bw.Write(Groups.IndexOf(requestSet.Group));
                        bw.Write(requestSetShapeOffset[requestSet]);
                        bw.Write(requestSet.Status);
                        stringTable.WriteStringOffset64(requestSet.Name);
                        bw.Write(MurMur3.HashData(requestSet.Name));

                        // Potentially bug with capcom's writer, it writes ID out again instead of 0 for padding
                        bw.Write(requestSet.Id);

                        stringTable.WriteStringOffset64(requestSet.Key);
                        bw.Write(MurMur3.HashData(requestSet.Key));
                        bw.Write(0);
                    }
                    else
                    {
                        bw.Write(Groups.IndexOf(requestSet.Group));
                        bw.Write(0);
                        bw.Write(requestSet.Status);
                        bw.Write(0);
                    }
                }

                // Ignore tags
                ms.Position = ignoreTagsOffset;
                foreach (var ignoreTag in IgnoreTags)
                {
                    stringTable.WriteStringOffset64(ignoreTag);
                    bw.Write(MurMur3.HashData(ignoreTag));
                    bw.Write(0);
                }

                // String table (0x2040)
                ms.Position = stringTableOffset;
                stringTable.WriteStrings();

                return new RcolFile(Version, ms.ToArray());
            }
        }

        private readonly struct RcolHeader(int version, ReadOnlyMemory<byte> data)
        {
            private readonly ReadOnlyMemory<byte> _data = data;

            public int Version { get; } = version;

            private ReadOnlySpan<byte> Span => _data.Span;

            public uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(0, 4));

            public int NumGroups =>
                Version == 2
                    ? Span[4]
                    : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(4, 4));

            public int NumShapes =>
                Version == 2
                    ? Span[5]
                    : Version >= 25
                        ? 0
                        : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(8, 4));

            public int NumUserData =>
                Version >= 25
                    ? BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(8, 4))
                    : 0;

            public int UknCount =>
                Version == 2
                    ? BinaryPrimitives.ReadInt16LittleEndian(Span.Slice(6, 2))
                    : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(Version >= 25 ? 12 : 12, 4));

            public int NumRequestSets =>
                Version == 2
                    ? BinaryPrimitives.ReadInt16LittleEndian(Span.Slice(8, 2))
                    : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(Version >= 25 ? 16 : 16, 4));

            public uint MaxRequestSetId =>
                Version == 2
                    ? BinaryPrimitives.ReadUInt16LittleEndian(Span.Slice(10, 2))
                    : BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(Version >= 25 ? 20 : 20, 4));

            private int OffsetAfterHeaderCounts => Version == 2 ? 12 : 24;

            private int OffsetAfterOptionalCounts =>
                Version > 11
                    ? OffsetAfterHeaderCounts + 8
                    : OffsetAfterHeaderCounts;

            public int NumIgnoreTags =>
                Version > 11
                    ? BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterHeaderCounts, 4))
                    : 0;

            public int NumAutoGenerateJoints =>
                Version > 11
                    ? BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterHeaderCounts + 4, 4))
                    : 0;

            public int UserDataSize =>
                BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterOptionalCounts, 4));

            public int Status =>
                BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterOptionalCounts + 4, 4));

            private int OffsetAfterStatus =>
                OffsetAfterOptionalCounts + 8 + (Version == 2 ? 4 : 0);

            public ulong UknRe3_A =>
                Version == 11
                    ? BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(OffsetAfterStatus, 8))
                    : 0;

            public ulong UknRe3_B =>
                Version == 11
                    ? BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(OffsetAfterStatus + 8, 8))
                    : 0;

            private int OffsetAfterRe3 =>
                Version == 11
                    ? OffsetAfterStatus + 16
                    : OffsetAfterStatus;

            public uint Ukn1 =>
                Version >= 20
                    ? BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(OffsetAfterRe3, 4))
                    : 0;

            public uint Ukn2 =>
                Version >= 20
                    ? BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(OffsetAfterRe3 + 4, 4))
                    : 0;

            private int OffsetAfterUkn12 =>
                Version >= 20
                    ? OffsetAfterRe3 + 8
                    : OffsetAfterRe3;

            public long GroupsPtrOffset =>
                BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12, 8));

            public long DataOffset =>
                BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 8, 8));

            public long RequestSetOffset =>
                BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 16, 8));

            public long IgnoreTagOffset =>
                Version > 11
                    ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 24, 8))
                    : 0;

            public long AutoGenerateJointDescOffset =>
                Version > 11
                    ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 32, 8))
                    : 0;

            public long RequestSetIDLookupsOffset =>
                Version == 2
                    ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 24, 8))
                    : 0;

            public ulong UknRe3 =>
                Version == 11
                    ? BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 24, 8))
                    : 0;

            public long UnknPtr0 =>
                Version >= 20
                    ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + (Version > 11 ? 40 : 24), 8))
                    : 0;

            public long UnknPtr1 =>
                Version >= 20
                    ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + (Version > 11 ? 48 : 32), 8))
                    : 0;

            public static int GetSize(int version) => version switch
            {
                2 => 56, // RE7
                11 => -1, // TODO: RE3R
                20 => 0x70, // RE7RT
                >= 25 => 90, // RE4R (and later?)
                _ => throw new ArgumentException($"Invalid version {version}!")
            };
        }

        private readonly struct RcolGroupInfo(int version, ReadOnlyMemory<byte> data)
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

        private readonly struct RcolShapeInfo(int version, ReadOnlyMemory<byte> data)
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

        private readonly struct RequestSetInfo(int version, ReadOnlyMemory<byte> data)
        {
            public int Id => version switch
            {
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(0, 4)),
                _ => 0
            };

            public int GroupIndex => version switch
            {
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(4, 4)),
                _ => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(0, 4))
            };

            public int ShapeOffset => version switch
            {
                _ when version >= 25 => 0,
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(8, 4)),
                _ => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(4, 4))
            };

            public int Status => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(24, 4)),
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(12, 4)),
                _ => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(8, 4))
            };

            public int UserDataIndex => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(8, 4)),
                _ => 0
            };

            public int GroupUserDataIndex => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(12, 4)),
                _ => 0
            };

            public long NameOffset => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(32, 8)),
                _ when version >= 3 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(16, 8)),
                _ => 0
            };

            public long KeyOffset => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(40, 8)),
                _ when version >= 3 => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(32, 8)),
                _ => 0
            };

            public int NameHash => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(48, 4)),
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(24, 4)),
                _ => 0
            };

            public int KeyHash => version switch
            {
                _ when version >= 25 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(52, 4)),
                _ when version >= 3 => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(36, 4)),
                _ => 0
            };

            public static int GetSize(int version) => version switch
            {
                _ when version >= 25 => 0x38,
                _ when version >= 3 => 0x30,
                _ => 0x10,
            };
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct IgnoreTagInfo
        {
            public long NameOffset;
            public int NameHash;
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

        /// <summary>
        /// Compatability flag so that rebuilds can be 100% identical. Some groups reuse previous mask guid list offsets
        /// even though it still writes its own list. It is a bit inconsistent.
        /// </summary>
        public bool ReuseOffsetForMaskGuids { get; set; }
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

    public class RequestSet
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Key { get; set; } = "";
        public required RcolGroup Group { get; init; }
        public required RcolShape Shape { get; init; }
        public int Status { get; init; }
        public RszObjectNode? UserData { get; set; }
        public ImmutableArray<RszObjectNode> ShapeUserData { get; set; } = [];
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
