using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using RszTool;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class RszBinarySerializer
    {
        // format
        // NUMTYPES | NUMFIELDS
        // TYPEID | CRC | NAMEID (x NUMTYPES)
        // ALIGN | NAMEID | FLAGS | ORIGINALTYPESTRINGID | SIZE | TYPE

        public static byte[] Serialize(
            byte[] rszJson,
            byte[] patchJson,
            byte[] enumJson)
        {
            return Serialize(
                Encoding.UTF8.GetString(rszJson),
                Encoding.UTF8.GetString(patchJson),
                Encoding.UTF8.GetString(enumJson));
        }

        public static byte[] Serialize(
            string rszJson,
            string patchJson,
            string enumJson)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(0x425A5352);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);

            var classCount = 0;
            var fieldCount = 0;
            var strings = new List<string>();
            var stringIds = new Dictionary<string, int>();
            var stringLengths = new List<int>();

            var rsz = JsonSerializer.Deserialize<Dictionary<string, RszClass>>(rszJson)!;
            var rszPatch = JsonSerializer.Deserialize<Dictionary<string, RszClassPatch>>(patchJson)!;
            var entries = rsz.ToArray();

            foreach (var patch in rszPatch)
            {
                var entry = entries.First(x => x.Value.name == patch.Key);
                if (patch.Value.ReplaceName is string newName)
                    entry.Value.name = newName;
                if (patch.Value.FieldPatches is RszFieldPatch[] fieldPatches)
                {
                    foreach (var fp in fieldPatches)
                    {
                        var field = entry.Value.fields.First(x => x.name == fp.Name);
                        if (fp.ReplaceName is string newFieldName)
                            field.name = newFieldName;
                        if (fp.OriginalType is string newOriginalType)
                            field.original_type = newOriginalType;
                        if (fp.Type is RszFieldType newType)
                            field.type = newType;
                    }
                }
            }

            foreach (var entry in entries)
            {
                var typeId = uint.Parse(entry.Key, NumberStyles.HexNumber);
                bw.Write(typeId);
                bw.Write(entry.Value.crc);
                var flags = 0;
                if (entry.Value.native)
                    flags |= (1 << 0);
                bw.Write(flags);
                bw.Write(AddString(entry.Value.name));
                classCount++;
            }
            var classIndex = 0;
            foreach (var entry in entries)
            {
                foreach (var f in entry.Value.fields)
                {
                    bw.Write(classIndex);
                    bw.Write((byte)f.align);
                    bw.Write((byte)f.size);
                    bw.Write((byte)f.type);
                    var flags = 0;
                    if (f.native)
                        flags |= 1 << 0;
                    if (f.array)
                        flags |= 1 << 1;
                    bw.Write((byte)flags);
                    bw.Write(AddString(f.original_type));
                    bw.Write(AddString(f.name));
                    fieldCount++;
                }
                classIndex++;
            }

            var stringPosition = ms.Position;
            ms.Position += strings.Count * 4;
            foreach (var s in strings)
            {
                var encoded = Encoding.UTF8.GetBytes(s);
                bw.Write(encoded);
                stringLengths.Add(encoded.Length);
            }
            ms.Position = stringPosition;
            foreach (var len in stringLengths)
            {
                bw.Write(len);
            }

            ms.Position = 4;
            bw.Write(classCount);
            bw.Write(fieldCount);
            bw.Write(strings.Count);

            int AddString(string s)
            {
                if (!stringIds.TryGetValue(s, out var id))
                {
                    id = strings.Count;
                    strings.Add(s);
                    stringIds.Add(s, id);
                }
                return id;
            }

            var list = Deserialize(ms.ToArray());
            return ms.ToArray();
        }

        public static RszParser Deserialize(byte[] data)
        {
            var list = new List<RszClass>();

            var ms = new MemoryStream(data);
            var br = new BinaryReader(ms);

            br.ReadInt32();
            var classCount = br.ReadInt32();
            var fieldCount = br.ReadInt32();
            var stringCount = br.ReadInt32();

            var stringLengths = new int[stringCount];
            ms.Position = 16 + (classCount * 16) + (fieldCount * 16);
            for (var i = 0; i < stringCount; i++)
            {
                stringLengths[i] = br.ReadInt32();
            }

            var strings = new string[stringCount];
            var stringData = br.ReadBytes((int)(ms.Length - ms.Position));
            var offset = 0;
            for (var i = 0; i < stringCount; i++)
            {
                var len = stringLengths[i];
                strings[i] = Encoding.UTF8.GetString(stringData, offset, len);
                offset += len;
            }

            ms.Position = 16;
            for (var i = 0; i < classCount; i++)
            {
                var c = new RszClass();
                c.typeId = br.ReadUInt32();
                c.crc = br.ReadUInt32();
                var flags = br.ReadInt32();
                if ((flags & 1) != 0)
                    c.native = true;
                c.name = strings[br.ReadInt32()];
                list.Add(c);
            }

            RszClass? lastParent = null;
            var fields = new List<RszField>();
            for (var i = 0; i < fieldCount; i++)
            {
                var parent = br.ReadInt32();
                var c = list[parent];
                if (lastParent != null && lastParent != c && fields.Count != 0)
                {
                    lastParent.fields = [.. fields];
                    fields.Clear();
                }
                lastParent = c;

                var f = new RszField();
                f.align = br.ReadByte();
                f.size = br.ReadByte();
                f.type = (RszFieldType)br.ReadByte();
                var flags = br.ReadByte();
                if ((flags & 1) != 0)
                    f.native = true;
                if ((flags & 2) != 0)
                    f.array = true;
                f.original_type = strings[br.ReadInt32()];
                f.name = strings[br.ReadInt32()];
                fields.Add(f);
            }
            if (lastParent != null)
            {
                lastParent.fields = [.. fields];
            }
            return new RszParser(list);
        }
    }
}
