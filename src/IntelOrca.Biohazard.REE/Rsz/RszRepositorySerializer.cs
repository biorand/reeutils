using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelOrca.Biohazard.REE.Compression;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszRepositorySerializer
    {
        public static RszRepositorySerializer Default { get; } = new RszRepositorySerializer();

        private RszRepositorySerializer() { }

        public RszTypeRepository FromJsonGz(byte[] compressed)
        {
            var uncompressed = Gzip.DecompressData(compressed);
            return FromJson(uncompressed);
        }

        public RszTypeRepository FromJson(ReadOnlySpan<byte> utf8Json)
        {
            var stringIdMap = JsonSerializer.Deserialize<Dictionary<string, RszTypeModel>>(utf8Json, new JsonSerializerOptions()
            {
                Converters = {
                    new HexUIntJsonConverter()
                },
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            var idMap = stringIdMap
                .Where(x => x.Key != "metadata")
                .ToDictionary(x => Convert.ToUInt32(x.Key, 16), x => x.Value);

            var repo = new RszTypeRepository();
            foreach (var kvp in idMap)
            {
                var rszType = new RszType();
                rszType.Id = kvp.Key;
                rszType.Crc = kvp.Value.Crc;
                rszType.Name = kvp.Value.Name ?? "";

                var fields = ImmutableArray.CreateBuilder<RszTypeField>();
                foreach (var f in kvp.Value.Fields ?? [])
                {
                    Enum.TryParse<RszFieldType>(f.Type, out var fieldType);

                    fields.Add(new RszTypeField()
                    {
                        Align = f.Align,
                        Name = f.Name ?? "",
                        Size = f.Size,
                        Type = fieldType,
                        IsArray = f.Array
                    });
                }
                rszType.Fields = fields.ToImmutable();
                repo.AddType(rszType);
            }
            return repo;
        }

        private class RszTypeModel
        {
            public uint Crc { get; set; }
            public string? Name { get; set; }
            public RszFieldModel[]? Fields { get; set; }
        }

        private class RszFieldModel
        {
            public int Align { get; set; }
            public bool Array { get; set; }
            public string? Name { get; set; }
            public bool Native { get; set; }
            public string? OriginalType { get; set; }
            public int Size { get; set; }
            public string? Type { get; set; }
        }

        private class HexUIntJsonConverter : JsonConverter<uint>
        {
            public override uint Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    string? text = reader.GetString();
                    if (text == null) return default;
                    return Convert.ToUInt32(text, 16);
                }

                return reader.GetUInt32();
            }

            public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
            {
                writer.WriteStringValue($"0x{value:x}");
            }
        }
    }
}
