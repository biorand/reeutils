﻿using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}")]
    public class RszType(RszTypeRepository repository)
    {
        public RszTypeRepository Repository => repository;
        public uint Id { get; set; }
        public uint Crc { get; set; }
        public string Name { get; set; } = "";
        public ImmutableArray<RszTypeField> Fields { get; set; } = [];

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

        public RszStructNode Create()
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

                        }
                        else
                        {
                            children.Add(field.ObjectType.Create());
                        }
                    }
                    else if (field.Type == RszFieldType.UserData || field.Type == RszFieldType.Resource)
                    {
                        throw new NotSupportedException();
                    }
                    else
                    {
                        children.Add(new RszDataNode(field.Type, new byte[field.Size]));
                    }
                }
            }
            return new RszStructNode(this, children.ToImmutable());
        }
    }
}
