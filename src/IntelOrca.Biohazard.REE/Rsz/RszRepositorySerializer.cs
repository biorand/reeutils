using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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

        public RszTypeRepository FromJsonFile(string path) => FromJson(File.ReadAllBytes(path));

        public RszTypeRepository FromJson(ReadOnlySpan<byte> utf8Json)
        {
            var stringIdMap = JsonSerializer.Deserialize<Dictionary<string, RszTypeModel>>(utf8Json, new JsonSerializerOptions()
            {
                Converters = {
                    new HexUIntJsonConverter()
                },
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            })!;
            stringIdMap.Remove("metadata");
            foreach (var kvp in stringIdMap)
            {
                kvp.Value.Id = Convert.ToUInt32(kvp.Key, 16);
            }
            var typeModels = stringIdMap.Values.OrderBy(x => x.Id).ToImmutableArray();

            var repo = new RszTypeRepository();

            // Create types
            foreach (var typeModel in typeModels)
            {
                repo.AddType(new RszType
                {
                    Repository = repo,
                    Id = typeModel.Id,
                    Crc = typeModel.Crc,
                    Name = typeModel.Name ?? ""
                });
            }

            // Set parents
            foreach (var typeModel in typeModels)
            {
                var parentTypeName = typeModel.Parent;
                if (string.IsNullOrEmpty(parentTypeName))
                    continue;

                var parentType = repo.FromName(parentTypeName);
                if (parentType == null)
                    continue;

                var rszType = repo.FromId(typeModel.Id) ?? throw new Exception("Type not found");
                rszType.Parent = parentType;
            }

            // Create fields
            foreach (var kvp in stringIdMap)
            {
                var id = Convert.ToUInt32(kvp.Key, 16);
                var rszType = repo.FromId(id) ?? throw new Exception();
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
                        IsArray = f.Array,
                        ObjectType = string.IsNullOrEmpty(f.OriginalType) ? null : repo.FromName(f.OriginalType)
                    });
                }
                rszType.Fields = fields.ToImmutable();
            }
            return repo;
        }

        private class RszTypeModel
        {
            public uint Id { get; set; }
            public uint Crc { get; set; }
            public string? Name { get; set; }
            public RszFieldModel[]? Fields { get; set; }
            public string? Parent { get; set; }
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
