using System;
using System.ComponentModel;
using System.Linq;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IntelOrca.Biohazard.REEUtils.Tools
{
    [McpServerToolType]
    internal sealed class RszTools
    {
        [McpServerTool(Name = "generate_class", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Generates C# classes for RSZ types similarly to the class command.")]
        public static string GenerateClass(
            [Description("One or more fully-qualified RSZ type names.")] string[] typeNames,
            [Description("Whether enum dependencies should also be generated.")] bool includeEnums,
            McpSession session)
        {
            if (typeNames == null || typeNames.Length == 0)
                throw new McpException("At least one type name must be specified.");

            var repo = session.RszTypeRepository ?? throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");
            var types = typeNames.Select(x => ResolveType(repo, x)).ToArray();

            var writer = new RszTypeCsharpWriter
            {
                GenerateEnums = includeEnums
            };
            return writer.Generate(types);
        }

        [McpServerTool(Name = "get_type", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Gets structured RSZ type information for a single type.")]
        public static string GetType(
            [Description("Fully-qualified RSZ type name.")] string typeName,
            McpSession session)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new McpException("A type name must be specified.");

            var repo = session.RszTypeRepository ?? throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");
            var type = ResolveType(repo, typeName);
            var nestedTypes = repo.GetNestedTypes(type);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                name = type.Name,
                id = $"0x{type.Id:X8}",
                crc = $"0x{type.Crc:X8}",
                @namespace = type.Namespace,
                nameWithoutNamespace = type.NameWithoutNamespace,
                declaringType = type.DeclaringType?.Name,
                parent = type.Parent?.Name,
                children = type.Children.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                nestedTypes = nestedTypes.Select(x => x.Name).ToArray(),
                isEnum = type.IsEnum,
                fields = type.Fields.Select(field => new
                {
                    name = field.Name,
                    type = field.Type.ToString(),
                    objectType = field.ObjectType?.Name,
                    isArray = field.IsArray,
                    isNative = field.IsNative,
                    align = field.Align,
                    size = field.Size,
                    isInherited = type.IsFieldInherited(field.Name),
                    propertyId = field.Id
                }).ToArray()
            }, JsonSupport.CreateOptions(camelCase: true));
        }

        private static RszType ResolveType(RszTypeRepository repo, string name)
        {
            var type = repo.FromName(name);
            if (type != null)
                return type;

            var suggestions = repo.Types
                .Where(x =>
                    !x.Name.ContainsAny(['[', ']', '<', '>', '`']) &&
                    x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();

            if (suggestions.Length == 1)
                return repo.FromName(suggestions[0])!;

            if (suggestions.Length == 0)
                throw new McpException($"Type '{name}' was not found.");

            throw new McpException($"Type '{name}' was not found. Suggestions: {string.Join(", ", suggestions)}.");
        }
    }
}
