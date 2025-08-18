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
    public unsafe class UvarFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x72617675;

        public ReadOnlyMemory<byte> Data => data;

        private ReadOnlySpan<T> GetSpan<T>(ulong offset, int count) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(data.Slice((int)offset, count * sizeof(T)).Span);
        }

        private UvarHeader Header => MemoryMarshal.Read<UvarHeader>(data.Span);
        private ulong ValuesOffset => Header.DataOffset + (Header.VariableCount * (ulong)sizeof(UvarHeader));
        private ReadOnlySpan<UvarVariable> Variables => GetSpan<UvarVariable>(Header.DataOffset, Header.VariableCount);
        private ReadOnlySpan<float> Values => GetSpan<float>(ValuesOffset, Header.VariableCount);
        private ReadOnlySpan<ulong> EmbeddedOffsets => GetSpan<ulong>(Header.EmbedsInfoOffset, Header.EmbedCount);
        private ReadOnlySpan<ulong> HashDataOffsets => GetSpan<ulong>(Header.HashInfoOffset, 4);
        private ReadOnlySpan<Guid> HashDataGuids => GetSpan<Guid>(HashDataOffsets[0], Header.VariableCount);
        private ReadOnlySpan<int> HashDataGuidMap => GetSpan<int>(HashDataOffsets[1], Header.VariableCount);
        private ReadOnlySpan<uint> HashDataNameHashes => GetSpan<uint>(HashDataOffsets[2], Header.VariableCount);
        private ReadOnlySpan<int> HashDataNameHashMap => GetSpan<int>(HashDataOffsets[3], Header.VariableCount);

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
            return new UvarFile(Data.Slice((int)offsets[index]));
        }

        public int Version => (int)Header.Version;
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
            public int Hash { get; set; }
            public string Name { get; set; } = "";
            public List<Variable> Variables { get; } = [];
            public List<Builder> Children { get; } = [];

            public Builder(UvarFile instance)
            {
                Hash = instance.Hash;
                Name = instance.Name;
                var variables = instance.Variables;
                var values = instance.Values;
                for (var i = 0; i < variables.Length; i++)
                {
                    var variable = variables[i];
                    Variables.Add(new Variable
                    {
                        Guid = variable.Guid,
                        Name = instance.GetString(variable.NameOffset),
                        Value = values[i],
                        TypeVal = variable.TypeVal,
                        NumBits = variable.NumBits
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
                    Version = 3,
                    Magic = MAGIC,
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
                bw.Skip<UvarHeader>();
                bw.Align(16);

                header.DataOffset = (ulong)ms.Position;
                bw.Skip<UvarVariable>(Variables.Count);
                for (var i = 0; i < Variables.Count; i++)
                {
                    variables[i].FloatOffset = (ulong)ms.Position;
                    bw.Write(Variables[i].Value);
                }
                bw.Align(16);

                header.StringsOffset = (ulong)ms.Position;
                bw.WriteUTF16(Name);
                for (var i = 0; i < Variables.Count; i++)
                {
                    variables[i].NameOffset = (ulong)ms.Position;
                    bw.WriteUTF16(Variables[i].Name);
                }
                bw.Align(16);

                header.EmbedsInfoOffset = (ulong)ms.Position;
                bw.Skip<ulong>(Children.Count);

                for (var i = 0; i < Children.Count; i++)
                {
                    embedOffsets[i] = (ulong)ms.Position;
                    bw.Write(Children[i].Build().Data.Span);
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
                bw.Write(in header);

                ms.Position = (long)header.DataOffset;
                bw.Write(variables);

                ms.Position = (long)header.EmbedsInfoOffset;
                bw.Write(embedOffsets);

                return new UvarFile(ms.ToArray());
            }

            [DebuggerDisplay("{Name}")]
            public class Variable
            {
                public Guid Guid { get; set; }
                public string Name { get; set; } = "";
                public float Value { get; set; }
                public int TypeVal { get; set; }
                public int NumBits { get; set; }
            }
        }
    }
}
