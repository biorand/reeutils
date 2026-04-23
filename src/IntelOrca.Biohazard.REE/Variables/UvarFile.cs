using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Variables
{
    [DebuggerDisplay("{Name}")]
    public unsafe class UvarFile
    {
        private const uint MAGIC = 0x72617675;
        private readonly int _version;
        private readonly ReadOnlyMemory<byte> _data;

        public UvarFile(ReadOnlyMemory<byte> data)
            : this((int)MemoryMarshal.Read<uint>(data.Span), data)
        {
        }

        public UvarFile(int version, ReadOnlyMemory<byte> data)
        {
            _version = version;
            _data = data;
        }

        public ReadOnlyMemory<byte> Data => _data;

        private ReadOnlySpan<T> GetSpan<T>(ulong offset, int count) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(_data.Slice((int)offset, count * sizeof(T)).Span);
        }

        private UvarHeader Header => _version switch
        {
            2 => ToHeader(MemoryMarshal.Read<UvarHeaderV2>(_data.Span)),
            _ => ToHeader(MemoryMarshal.Read<UvarHeaderV3>(_data.Span))
        };
        private ReadOnlySpan<UvarVariable> Variables => GetSpan<UvarVariable>(Header.DataOffset, Header.VariableCount);
        private ReadOnlySpan<ulong> EmbeddedOffsets => GetSpan<ulong>(Header.EmbedsInfoOffset, Header.EmbedCount);
        private ReadOnlySpan<ulong> HashDataOffsets => GetSpan<ulong>(Header.HashInfoOffset, 4);
        private ReadOnlySpan<Guid> HashDataGuids => GetSpan<Guid>(HashDataOffsets[0], Header.VariableCount);
        private ReadOnlySpan<int> HashDataGuidMap => GetSpan<int>(HashDataOffsets[1], Header.VariableCount);
        private ReadOnlySpan<uint> HashDataNameHashes => GetSpan<uint>(HashDataOffsets[2], Header.VariableCount);
        private ReadOnlySpan<int> HashDataNameHashMap => GetSpan<int>(HashDataOffsets[3], Header.VariableCount);

        private static UvarHeader ToHeader(in UvarHeaderV2 header)
        {
            return new UvarHeader
            {
                Version = header.Version,
                Magic = header.Magic,
                StringsOffset = header.StringsOffset,
                DataOffset = header.DataOffset,
                EmbedsInfoOffset = header.EmbedsInfoOffset,
                HashInfoOffset = header.HashInfoOffset,
                UnknownHeaderValue = header.UnknownHeaderValue,
                UvarHash = header.UvarHash,
                VariableCount = header.VariableCount,
                EmbedCount = header.EmbedCount
            };
        }

        private static UvarHeader ToHeader(in UvarHeaderV3 header)
        {
            return new UvarHeader
            {
                Version = header.Version,
                Magic = header.Magic,
                StringsOffset = header.StringsOffset,
                DataOffset = header.DataOffset,
                EmbedsInfoOffset = header.EmbedsInfoOffset,
                HashInfoOffset = header.HashInfoOffset,
                UvarHash = header.UvarHash,
                VariableCount = header.VariableCount,
                EmbedCount = header.EmbedCount
            };
        }

        private static int CompareOffsetEntries((int Index, ulong Offset) a, (int Index, ulong Offset) b)
        {
            var offsetComparison = a.Offset.CompareTo(b.Offset);
            return offsetComparison != 0 ? offsetComparison : a.Index.CompareTo(b.Index);
        }

        private ulong GetSectionStartOffset(Func<UvarVariable, ulong> getOffset, ulong fallback)
        {
            var variables = Variables;
            var minOffset = fallback;
            for (var i = 0; i < variables.Length; i++)
            {
                var offset = getOffset(variables[i]);
                if (offset != 0 && offset < minOffset)
                {
                    minOffset = offset;
                }
            }
            return minOffset;
        }

        private byte[][] GetVariableData(Func<UvarVariable, ulong> getOffset, ulong endOffset)
        {
            var variables = Variables;
            var result = new byte[variables.Length][];
            var offsets = new List<(int Index, ulong Offset)>();
            for (var i = 0; i < variables.Length; i++)
            {
                var offset = getOffset(variables[i]);
                if (offset != 0)
                {
                    offsets.Add((i, offset));
                }
            }

            offsets.Sort(CompareOffsetEntries);
            for (var i = 0; i < offsets.Count; i++)
            {
                var start = offsets[i].Offset;
                var end = i + 1 < offsets.Count ? offsets[i + 1].Offset : Header.StringsOffset;
                if (end > endOffset)
                {
                    end = endOffset;
                }
                result[offsets[i].Index] = Data.Slice((int)start, (int)(end - start)).ToArray();
            }

            for (var i = 0; i < result.Length; i++)
            {
                result[i] ??= [];
            }
            return result;
        }

        private byte[][] GetVariableValueData()
        {
            var valueSectionEnd = GetSectionStartOffset(variable => variable.UknOffset, Header.StringsOffset);
            return GetVariableData(variable => variable.FloatOffset, valueSectionEnd);
        }

        private byte[][] GetVariableExpressionData()
        {
            return GetVariableData(variable => variable.UknOffset, Header.StringsOffset);
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

        public UvarFile GetEmbedded(int index)
        {
            if (index < 0 || index >= EmbeddedCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            var offsets = EmbeddedOffsets;
            return new UvarFile(_version, Data.Slice((int)offsets[index]));
        }

        public int Version => _version;
        public int Hash => (int)Header.UvarHash;
        public int VariableCount => Header.VariableCount;
        public int EmbeddedCount => Header.EmbedCount;
        public string Name => GetString(Header.StringsOffset);

        public Builder ToBuilder()
        {
            return new Builder(this);
        }

        [DebuggerDisplay("{Name}")]
        public class Builder
        {
            public int Version { get; set; } = 3;
            public int Hash { get; set; }
            public string Name { get; set; } = "";
            public ulong UnknownHeaderValue { get; set; }
            public List<Variable> Variables { get; } = [];
            public List<Builder> Children { get; } = [];

            public Builder(UvarFile instance)
            {
                Version = instance.Version;
                Hash = instance.Hash;
                Name = instance.Name;
                UnknownHeaderValue = instance.Header.UnknownHeaderValue;

                var variables = instance.Variables;
                var valueData = instance.GetVariableValueData();
                var expressionData = instance.GetVariableExpressionData();
                for (var i = 0; i < variables.Length; i++)
                {
                    var variable = variables[i];
                    Variables.Add(new Variable
                    {
                        Guid = variable.Guid,
                        Name = instance.GetString(variable.NameOffset),
                        Value = valueData[i].Length >= sizeof(float)
                            ? BitConverter.ToSingle(valueData[i], 0)
                            : 0,
                        TypeVal = variable.TypeVal,
                        NumBits = variable.NumBits,
                        ValueData = valueData[i],
                        ValueOffset = variable.FloatOffset,
                        ExpressionData = expressionData[i],
                        ExpressionOffset = variable.UknOffset
                    });
                }

                for (var i = 0; i < instance.EmbeddedCount; i++)
                {
                    var embedded = instance.GetEmbedded(i);
                    Children.Add(new Builder(embedded));
                }
            }

            public UvarFile Build()
            {
                var header = new UvarHeader
                {
                    Version = (uint)Version,
                    Magic = MAGIC,
                    UnknownHeaderValue = UnknownHeaderValue,
                    UvarHash = (uint)Hash,
                    VariableCount = (ushort)Variables.Count,
                    EmbedCount = (ushort)Children.Count
                };
                var variables = new Span<UvarVariable>(new UvarVariable[Variables.Count]);
                var embedOffsets = new Span<ulong>(new ulong[Children.Count]);
                for (var i = 0; i < Variables.Count; i++)
                {
                    var entry = Variables[i];
                    ref var var = ref variables[i];
                    var.Guid = entry.Guid;
                    var.NameHash = (uint)MurMur3.HashData(entry.Name);
                    var.TypeVal = entry.TypeVal;
                    var.NumBits = entry.NumBits;
                }

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                // First pass
                if (Version == 2)
                {
                    bw.WriteZeros<UvarHeaderV2>();
                }
                else
                {
                    bw.WriteZeros<UvarHeaderV3>();
                }
                bw.Align(16);

                header.DataOffset = (ulong)ms.Position;
                bw.WriteZeros<UvarVariable>(Variables.Count);

                var valueDataEntries = new List<(int Index, ulong Offset)>();
                for (var i = 0; i < Variables.Count; i++)
                {
                    if (Variables[i].ValueOffset != 0)
                    {
                        valueDataEntries.Add((i, Variables[i].ValueOffset));
                    }
                }
                valueDataEntries.Sort(CompareOffsetEntries);
                foreach (var (index, _) in valueDataEntries)
                {
                    variables[index].FloatOffset = (ulong)ms.Position;
                    bw.Write(GetValueBytes(Variables[index]));
                }
                bw.Align(16);

                var expressionDataEntries = new List<(int Index, ulong Offset)>();
                for (var i = 0; i < Variables.Count; i++)
                {
                    if (Variables[i].ExpressionOffset != 0)
                    {
                        expressionDataEntries.Add((i, Variables[i].ExpressionOffset));
                    }
                }
                expressionDataEntries.Sort(CompareOffsetEntries);
                foreach (var (index, _) in expressionDataEntries)
                {
                    variables[index].UknOffset = (ulong)ms.Position;
                    bw.Write(Variables[index].ExpressionData);
                }

                header.StringsOffset = (ulong)ms.Position;
                bw.WriteUTF16(Name);
                for (var i = 0; i < Variables.Count; i++)
                {
                    variables[i].NameOffset = (ulong)ms.Position;
                    bw.WriteUTF16(Variables[i].Name);
                }
                bw.Align(16);

                if (Children.Count != 0)
                {
                    bw.Align(16);
                    header.EmbedsInfoOffset = (ulong)ms.Position;
                    bw.WriteZeros<ulong>(Children.Count);

                    for (var i = 0; i < Children.Count; i++)
                    {
                        bw.Align(16);
                        embedOffsets[i] = (ulong)ms.Position;
                        bw.Write(Children[i].Build().Data.Span);
                    }
                }
                bw.Align(16);

                // Write offsets for sorted tables
                header.HashInfoOffset = (ulong)ms.Position;
                var offset = ms.Position + 32;
                bw.Write(offset);
                offset += variables.Length * sizeof(Guid);
                bw.Write(offset);
                offset += variables.Length * sizeof(int);
                bw.Write(offset);
                offset += variables.Length * sizeof(int);
                bw.Write(offset);

                // Write sorted guids
                var sortedGuidArray = new (int Index, Guid Guid)[variables.Length];
                for (var i = 0; i < variables.Length; i++)
                {
                    sortedGuidArray[i] = (i, variables[i].Guid);
                }
                Array.Sort(sortedGuidArray, (a, b) => a.Guid.CompareTo(b.Guid));
                for (var i = 0; i < variables.Length; i++)
                {
                    bw.Write(sortedGuidArray[i].Guid);
                }
                for (var i = 0; i < variables.Length; i++)
                {
                    bw.Write(sortedGuidArray[i].Index);
                }

                // Write sorted hash names
                var sortedHashArray = new (int Index, uint NameHash)[variables.Length];
                for (var i = 0; i < variables.Length; i++)
                {
                    sortedHashArray[i] = (i, variables[i].NameHash);
                }
                Array.Sort(sortedHashArray, (a, b) => a.NameHash.CompareTo(b.NameHash));
                for (var i = 0; i < variables.Length; i++)
                {
                    bw.Write(sortedHashArray[i].NameHash);
                }
                for (var i = 0; i < variables.Length; i++)
                {
                    bw.Write(sortedHashArray[i].Index);
                }

                // Second pass
                ms.Position = 0;
                WriteHeader(bw, header);

                ms.Position = (long)header.DataOffset;
                bw.Write(variables);

                if (embedOffsets.Length != 0)
                {
                    ms.Position = (long)header.EmbedsInfoOffset;
                    bw.Write(embedOffsets);
                }

                return new UvarFile(Version, ms.ToArray());
            }

            private void WriteHeader(BinaryWriter bw, in UvarHeader header)
            {
                if (Version == 2)
                {
                    var headerV2 = new UvarHeaderV2
                    {
                        Version = header.Version,
                        Magic = header.Magic,
                        StringsOffset = header.StringsOffset,
                        DataOffset = header.DataOffset,
                        EmbedsInfoOffset = header.EmbedsInfoOffset,
                        HashInfoOffset = header.HashInfoOffset,
                        UnknownHeaderValue = header.UnknownHeaderValue,
                        UvarHash = header.UvarHash,
                        VariableCount = header.VariableCount,
                        EmbedCount = header.EmbedCount
                    };
                    bw.Write(in headerV2);
                }
                else
                {
                    var headerV3 = new UvarHeaderV3
                    {
                        Version = header.Version,
                        Magic = header.Magic,
                        StringsOffset = header.StringsOffset,
                        DataOffset = header.DataOffset,
                        EmbedsInfoOffset = header.EmbedsInfoOffset,
                        HashInfoOffset = header.HashInfoOffset,
                        UvarHash = header.UvarHash,
                        VariableCount = header.VariableCount,
                        EmbedCount = header.EmbedCount
                    };
                    bw.Write(in headerV3);
                }
            }

            private static byte[] GetValueBytes(Variable variable)
            {
                if (variable.ValueData.Length == 0)
                {
                    return [];
                }

                if (variable.ValueData.Length == sizeof(float))
                {
                    var result = (byte[])variable.ValueData.Clone();
                    BitConverter.TryWriteBytes(result, variable.Value);
                    return result;
                }

                return variable.ValueData;
            }

            [DebuggerDisplay("{Name}")]
            public class Variable
            {
                public Guid Guid { get; set; }
                public string Name { get; set; } = "";
                public float Value { get; set; }
                public int TypeVal { get; set; }
                public int NumBits { get; set; }
                public byte[] ValueData { get; set; } = [];
                public ulong ValueOffset { get; set; }
                public byte[] ExpressionData { get; set; } = [];
                public ulong ExpressionOffset { get; set; }
            }
        }
    }
}
