using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Creates C# code that a RSZ instance can deserialize into.
    /// </summary>
    public class RszTypeCsharpWriter
    {
        public bool GenerateEnums { get; set; }

        public string Generate(RszType rszType)
        {
            var writer = new CsharpWriter();

            // Collate types
            var allTypes = FindTypes([], rszType);

            foreach (var g in allTypes.GroupBy(x => x.Namespace))
            {
                writer.BeginNamespaceBlock(g.Key);
                foreach (var t in g)
                {
                    if (t.IsEnum)
                    {
                        if (GenerateEnums)
                        {
                            writer.BeginEnumBlock(t.NameWithoutNamespace);
                            writer.EndBlock();
                        }
                    }
                    else
                    {
                        WriteType(t);
                    }
                }
                writer.EndBlock();
            }

            return writer.ToString();

            void WriteType(RszType t)
            {
                writer.BeginClassBlock(t.NameWithoutNamespace);
                foreach (var f in t.Fields)
                {
                    var fieldType = GetFieldTypeName(f);
                    if (f.IsArray)
                    {
                        writer.Property($"System.Collections.Generic.List<{fieldType}>", f.Name, "[]");
                    }
                    else
                    {
                        writer.Property(fieldType, f.Name, f.Type == RszFieldType.String
                            ? "\"\""
                            : fieldType.StartsWith("System.")
                                ? null
                                : "new()");
                    }
                }

                var nestedTypes = t.Repository.GetNestedTypes(t);
                foreach (var nestedType in nestedTypes)
                {
                    WriteType(nestedType);
                }

                writer.EndBlock();
            }
        }

        private string GetFieldTypeName(RszTypeField field)
        {
            if (field.Type == RszFieldType.String)
                return "string";

            var objectType = field.ObjectType;
            if (!GenerateEnums)
            {
                if (objectType?.IsEnum == true)
                {
                    objectType = objectType.Fields[0].ObjectType;
                }
            }

            return objectType?.Name ?? "object";
        }

        private List<RszType> FindTypes(List<RszType> types, RszType type)
        {
            if (type.Name.StartsWith("System."))
                return types;

            if (types.Contains(type))
                return types;

            // Do not include nested types
            if (type.Repository.FromName(type.Namespace) == null)
                types.Add(type);

            foreach (var f in type.Fields)
            {
                if (f.ObjectType != null)
                {
                    FindTypes(types, f.ObjectType);
                }
            }
            return types;
        }

        private class CsharpWriter
        {
            private StringBuilder _sb = new StringBuilder();
            private int _indent;

            public void AppendLine(string line)
            {
                _sb.Append(new string(' ', _indent * 4));
                _sb.Append(line);
                _sb.AppendLine();
            }

            public void BeginNamespaceBlock(string ns)
            {
                AppendLine($"namespace {ns}");
                AppendLine("{");
                Indent();
            }

            public void BeginEnumBlock(string name)
            {
                AppendLine($"internal enum {name}");
                AppendLine("{");
                Indent();
            }

            public void BeginClassBlock(string name)
            {
                AppendLine($"internal class {name}");
                AppendLine("{");
                Indent();
            }

            public void Property(string type, string name, string? initializer)
            {
                if (initializer == null)
                    AppendLine($"public {type} {name} {{ get; set; }}");
                else
                    AppendLine($"public {type} {name} {{ get; set; }} = {initializer};");
            }

            public void EndBlock()
            {
                Outdent();
                AppendLine("}");
            }

            public void Indent()
            {
                _indent++;
            }

            public void Outdent()
            {
                _indent--;
            }

            public override string ToString() => _sb.ToString();
        }
    }
}
