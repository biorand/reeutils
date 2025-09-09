using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}")]
    public class RszType
    {
        public required RszTypeRepository Repository { get; init; }
        public required uint Id { get; init; }
        public required uint Crc { get; init; }
        public required string Name { get; init; }
        public ImmutableArray<RszTypeField> Fields { get; set; } = [];

        /// <summary>
        /// Gets the parent type that is likely the super class for this type.
        /// </summary>
        public RszType? Parent { get; set; }

        /// <summary>
        /// Gets other types that are likely sub classes of this type.
        /// </summary>
        public IEnumerable<RszType> Children => Repository.Types.Where(x => x.Parent == this);

        public string Namespace
        {
            get
            {
                var lastFullStop = Name.LastIndexOf('.');
                return lastFullStop == -1 ? "" : Name[..lastFullStop];
            }
        }

        public string NameWithoutNamespace
        {
            get
            {
                var lastFullStop = Name.LastIndexOf('.');
                return lastFullStop == -1 ? Name : Name[(lastFullStop + 1)..];
            }
        }

        public bool IsEnum
        {
            get
            {
                if (Fields.Length == 1)
                {
                    var field = Fields[0];
                    if (field.Name == "value__")
                        return true;
                }
                return false;
            }
        }

        public int FindFieldIndex(string name)
        {
            for (var i = 0; i < Fields.Length; i++)
            {
                if (Fields[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        public RszObjectNode Create()
        {
            var children = ImmutableArray.CreateBuilder<IRszNode>();
            foreach (var field in Fields)
            {
                if (field.IsArray)
                {
                    children.Add(new RszArrayNode(field.Type, []));
                }
                else
                {
                    if (field.Type == RszFieldType.String)
                    {
                        children.Add(new RszStringNode(""));
                    }
                    else if (field.Type == RszFieldType.Object)
                    {
                        if (field.ObjectType == null)
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            children.Add(field.ObjectType.Create());
                        }
                    }
                    else if (field.Type == RszFieldType.UserData)
                    {
                        children.Add(new RszUserDataNode());
                    }
                    else if (field.Type == RszFieldType.Resource)
                    {
                        children.Add(new RszResourceNode());
                    }
                    else
                    {
                        children.Add(new RszValueNode(field.Type, new byte[field.Size]));
                    }
                }
            }
            return new RszObjectNode(this, children.ToImmutable());
        }

        public bool IsFieldInherited(string fieldName)
        {
            var parent = Parent;
            while (parent != null)
            {
                if (parent.Fields.Any(x => x.Name == fieldName))
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return false;
        }
    }
}
