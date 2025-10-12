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
            var impl = new HashSet<RszType>();
            var allTypes = FindTypes([], impl, rszType);

            foreach (var g in allTypes.GroupBy(x => x.Namespace))
            {
                if (rszType.Repository.FromName(g.Key) != null)
                    continue;

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
                    else if (t.DeclaringType == null)
                    {
                        WriteType(t);
                    }
                }
                writer.EndBlock();
            }

            return writer.ToString();

            void WriteType(RszType t)
            {
                if (t.Name.Contains("[]") || t.Name.Contains('<'))
                    return;

                string? parentName = null;
                RszType? parent = null;
                if (t.Parent != null && allTypes.Contains(t.Parent))
                {
                    parent = t.Parent;
                    parentName = t.Parent.Namespace == t.Namespace
                        ? t.Parent.NameWithoutNamespace
                        : t.Parent.Name;
                }

                writer.BeginClassBlock(t.NameWithoutNamespace, parentName);
                if (impl.Contains(t))
                {
                    foreach (var f in t.Fields)
                    {
                        if (parent != null && t.IsFieldInherited(f.Name))
                            continue;

                        var fieldType = GetFieldTypeName(f);
                        if (f.IsArray)
                        {
                            writer.Property($"System.Collections.Generic.List<{fieldType}>", f.Name, "[]");
                        }
                        else
                        {
                            writer.Property(fieldType, f.Name, GetInitializer(f));
                        }
                    }
                }

                var nestedTypes = t.Repository.GetNestedTypes(t);
                foreach (var nestedType in nestedTypes)
                {
                    if (allTypes.Contains(nestedType))
                    {
                        WriteType(nestedType);
                    }
                }

                writer.EndBlock();
            }
        }

        private string? GetInitializer(RszTypeField f)
        {
            if (f.Type == RszFieldType.String)
                return "\"\"";

            if (f.ObjectType is RszType rszType)
            {
                if (rszType.IsEnum)
                {
                    return null;
                }
                else if (rszType.Name.StartsWith("System."))
                {
                    return null;
                }
            }

            return "new()";
        }

        private string GetFieldTypeName(RszTypeField field)
        {
            var objectType = field.ObjectType;
            if (!GenerateEnums)
            {
                if (objectType?.IsEnum == true)
                {
                    objectType = objectType.Fields[0].ObjectType;
                }
            }

            return field.Type switch
            {
                RszFieldType.Bool => "bool",
                RszFieldType.S8 => "sbyte",
                RszFieldType.U8 => "byte",
                RszFieldType.S16 => "short",
                RszFieldType.U16 => "ushort",
                RszFieldType.S32 => "int",
                RszFieldType.U32 => "uint",
                RszFieldType.S64 => "long",
                RszFieldType.U64 => "ulong",
                RszFieldType.F32 => "float",
                RszFieldType.F64 => "double",
                RszFieldType.Vec2 => "System.Numerics.Vector2",
                RszFieldType.Vec3 => "System.Numerics.Vector3",
                RszFieldType.Vec4 => "System.Numerics.Vector4",
                RszFieldType.Mat4 => "System.Numerics.Matrix4x4",
                RszFieldType.Quaternion => "System.Numerics.Quaternion",
                RszFieldType.Guid or RszFieldType.GameObjectRef => "System.Guid",
                RszFieldType.Range => "IntelOrca.Biohazard.REE.Rsz.Native.Range",
                RszFieldType.KeyFrame => "IntelOrca.Biohazard.REE.Rsz.Native.KeyFrame",
                RszFieldType.String => "string",
                RszFieldType.UserData => "RszUserDataNode",
                RszFieldType.Resource => "RszResourceNode",
                _ => objectType?.Name ?? "object"
            };
        }

        private List<RszType> FindTypes(List<RszType> decl, HashSet<RszType> impl, RszType type)
        {
            if (type.Name.StartsWith("System."))
                return decl;

            if (type.Name.StartsWith("via."))
                return decl;

            if (type.IsEnum && !GenerateEnums)
                return decl;

            if (decl.Contains(type))
                return decl;

            var definingType = type.DeclaringType;
            if (definingType != null && !decl.Contains(definingType))
            {
                if (!decl.Contains(type))
                {
                    decl.Add(definingType);
                }
            }

            decl.Add(type);
            impl.Add(type);

            // Include sub classes
            foreach (var subType in type.Children.OrderBy(x => x.Name))
            {
                decl.Add(subType);
                impl.Add(subType);
            }

            foreach (var f in type.Fields)
            {
                if (f.ObjectType != null)
                {
                    FindTypes(decl, impl, f.ObjectType);
                }
            }
            return decl;
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

            public void BeginClassBlock(string name, string? parentName)
            {
                var inhertiance = "";
                if (parentName != null)
                    inhertiance = $" : {parentName}";
                else
                    inhertiance = "";

                AppendLine($"internal class {name}{inhertiance}");
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
