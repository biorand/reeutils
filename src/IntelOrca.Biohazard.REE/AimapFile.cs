using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE
{
    /// <summary>
    /// A component/section within an AIMAP file.
    /// Each component has a type name and array of navigation points/triangles.
    /// </summary>
    public class AimapComponent
    {
        private readonly ReadOnlyMemory<byte> _data;
        private readonly int _dataOffset;
        
        public string TypeName { get; }
        public int Offset { get; }
        public int DataSize { get; }
        public int PointCount { get; }
        
        internal AimapComponent(ReadOnlyMemory<byte> fileData, string typeName, int offset, int dataOffset, int dataSize)
        {
            _data = fileData;
            TypeName = typeName;
            Offset = offset;
            _dataOffset = dataOffset;
            DataSize = dataSize;
            
            // First uint32 of data is the point count
            if (dataSize >= 4)
            {
                PointCount = BinaryPrimitives.ReadInt32LittleEndian(fileData.Span.Slice(dataOffset, 4));
            }
        }
        
        /// <summary>
        /// Gets the position of a point by index.
        /// Points are stored as Vec3 (12 bytes) + 12 bytes padding = 24 bytes each.
        /// </summary>
        public (float X, float Y, float Z) GetPointPosition(int index)
        {
            if (index < 0 || index >= PointCount)
                return (0, 0, 0);
            
            // Each point is 24 bytes: Vec3 (12 bytes) + 12 bytes of additional data
            const int ELEMENT_SIZE = 24;
            int elementsStart = _dataOffset + 4; // Skip count
            int offset = elementsStart + index * ELEMENT_SIZE;
            var span = _data.Span;
            
            if (offset + 12 > span.Length)
                return (0, 0, 0);
            
            float x = BitConverter.ToSingle(span.Slice(offset, 4).ToArray(), 0);
            float y = BitConverter.ToSingle(span.Slice(offset + 4, 4).ToArray(), 0);
            float z = BitConverter.ToSingle(span.Slice(offset + 8, 4).ToArray(), 0);
            
            return (x, y, z);
        }
        
        public string ShortTypeName => TypeName.Split('.').Length > 0 
            ? TypeName.Split('.')[^1] 
            : TypeName;
    }

    /// <summary>
    /// Parser for RE Engine AIMAP (.aimap) files.
    /// These files contain navigation/AI map data with embedded RSZ data.
    /// </summary>
    public class AimapFile
    {
        private const uint AIMP_MAGIC = 0x504D4941; // "AIMP"

        private readonly ReadOnlyMemory<byte> _data;

        public int NameLength { get; }
        public string MapName { get; }
        public Guid MapGuid { get; }
        public IReadOnlyList<AimapComponent> Components { get; }
        public int RszOffset { get; }
        public RszFile Rsz { get; }

        public AimapFile(string path) : this(File.ReadAllBytes(path))
        {
        }

        public AimapFile(ReadOnlyMemory<byte> data)
        {
            _data = data;
            var span = data.Span;

            if (span.Length < 8)
                throw new InvalidDataException("File too small to be AIMP");

            var magic = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
            if (magic != AIMP_MAGIC)
                throw new InvalidDataException($"Invalid AIMP magic: 0x{magic:X8}");

            NameLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0x04, 4));
            MapName = ReadUtf16String(span, 0x08, 0x08 + NameLength * 2, out _);

            int nameByteEnd = 0x08 + NameLength * 2;
            int guidOffset = (nameByteEnd + 15) & ~15;
            
            MapGuid = guidOffset + 16 <= span.Length 
                ? new Guid(span.Slice(guidOffset, 16)) 
                : Guid.Empty;

            RszOffset = FindPattern(span, new byte[] { 0x52, 0x53, 0x5A, 0x00 });
            if (RszOffset < 0)
                throw new InvalidDataException("Could not find RSZ data in AIMP file.");

            // Find all components
            var components = new List<AimapComponent>();
            byte[] viaPattern = { 0x76, 0x00, 0x69, 0x00, 0x61, 0x00, 0x2E, 0x00 };
            
            int searchPos = guidOffset + 16;
            var typeOffsets = new List<(int offset, string typeName, int typeNameEnd)>();
            
            while (searchPos < RszOffset)
            {
                int viaOffset = FindPattern(span.Slice(searchPos), viaPattern);
                if (viaOffset < 0) break;
                
                viaOffset += searchPos;
                if (viaOffset >= RszOffset) break;
                
                string typeName = ReadUtf16String(span, viaOffset, span.Length, out int typeNameEnd);
                typeOffsets.Add((viaOffset, typeName, typeNameEnd));
                searchPos = typeNameEnd;
            }

            // Create components with proper data offsets
            for (int i = 0; i < typeOffsets.Count; i++)
            {
                var (offset, typeName, typeNameEnd) = typeOffsets[i];
                
                int dataOffset = (typeNameEnd + 3) & ~3; // Align to 4 bytes
                int dataEnd = (i + 1 < typeOffsets.Count) 
                    ? typeOffsets[i + 1].offset - 8 
                    : RszOffset;
                
                components.Add(new AimapComponent(data, typeName, offset, dataOffset, dataEnd - dataOffset));
            }

            Components = components;
            Rsz = new RszFile(data.Slice(RszOffset));
        }

        private static string ReadUtf16String(ReadOnlySpan<byte> data, int start, int maxEnd, out int actualEnd)
        {
            var sb = new StringBuilder();
            int i = start;
            while (i < maxEnd - 1 && i < data.Length - 1)
            {
                if (data[i] == 0 && data[i + 1] == 0) break;
                sb.Append((char)(data[i] | (data[i + 1] << 8)));
                i += 2;
            }
            actualEnd = i + 2;
            return sb.ToString();
        }

        private static int FindPattern(ReadOnlySpan<byte> data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (data[i + j] != pattern[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        public override string ToString() => $"AIMAP: {MapName} ({Components.Count} components, RSZ@0x{RszOffset:X})";
    }
}
