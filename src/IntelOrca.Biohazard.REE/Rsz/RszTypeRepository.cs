using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszTypeRepository
    {
        private Dictionary<uint, RszType> _idToTypeMap = [];
        private Dictionary<string, RszType> _nameToTypeMap = [];

        private ModuleBuilder _moduleBuilder;

        public RszType? FromId(uint id)
        {
            _idToTypeMap.TryGetValue(id, out var result);
            return result;
        }

        public RszType? FromName(string name)
        {
            _nameToTypeMap.TryGetValue(name, out var result);
            return result;
        }

        public void AddType(RszType type)
        {
            GetOrCreateRuntimeType(type);

            _idToTypeMap.Add(type.Id, type);
            _nameToTypeMap.Add(type.Name, type);
        }

        public void AddArrayType(string name, uint id, uint crc)
        {
            var elementType = FromName(name)!;
            AddType(new RszType()
            {
                Kind = RszTypeKind.Array,
                Id = id,
                Crc = crc,
                Name = $"{elementType.Name}[]",
                Fields = [],
                ArrayDimensions = 1,
                ElementType = elementType
            });
        }

        public RszTypeRepository()
        {
            var assemblyName = new AssemblyName("DynamicAssembly");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            AddType(new RszType()
            {
                Kind = RszTypeKind.Null,
                Id = 0x0,
                Crc = 0x0,
                Name = "null",
                Fields = []
            });
            AddType(new RszType()
            {
                Kind = RszTypeKind.Int32,
                Id = 0x25425A57,
                Crc = 0x3923313B,
                Name = "System.Int32",
                Size = 4,
                Fields = []
            });
            AddType(new RszType()
            {
                Kind = RszTypeKind.Enum,
                Id = 0x7AEC2311,
                Crc = 0x431C758D,
                Name = "chainsaw.ItemID",
                Size = 4,
                Fields = [
                    new RszTypeField(FromId(0x25425A57)!, "value__")
                ]
            });
            AddArrayType("chainsaw.ItemID", 0xBCB6E10A, 0x37E3FCB);
            AddType(new RszType()
            {
                Kind = RszTypeKind.Struct,
                Id = 0x5DE24ED9,
                Crc = 0xB0CBC79F,
                Name = "chainsaw.WeaponPartsCombineDefinition",
                Fields = [
                    new RszTypeField(FromId(0x7AEC2311)!, "_ItemId"),
                    new RszTypeField(FromId(0xBCB6E10A)!, "_TargetItemIds")
                ]
            });
            AddArrayType("chainsaw.WeaponPartsCombineDefinition", 0xCC45B258, 0x37E3FCB);
            AddType(new RszType()
            {
                Kind = RszTypeKind.Struct,
                Id = 0xE840EAEF,
                Crc = 0x13AF837A,
                Name = "chainsaw.WeaponPartsCombineDefinitionUserdata",
                Fields = [
                    new RszTypeField(FromId(0xCC45B258)!, "_Datas")
                ]
            });
        }

        private Type? GetOrCreateRuntimeType(RszType type)
        {
            if (type.ClrType != null)
                return type.ClrType;

            switch (type.Kind)
            {
                case RszTypeKind.Null:
                    break;
                case RszTypeKind.Array:
                {
                    var elementClrType = GetOrCreateRuntimeType(type.ElementType!)!;
                    type.ClrType = elementClrType.MakeArrayType();
                    break;
                }
                case RszTypeKind.Enum:
                {
                    var valueType = GetOrCreateRuntimeType(type.Fields[0].Type);
                    var enumBuilder = _moduleBuilder.DefineEnum(type.Name, TypeAttributes.Public, valueType);
                    type.ClrType = enumBuilder.CreateTypeInfo().AsType();
                    break;
                }
                case RszTypeKind.Struct:
                {
                    var typeBuilder = _moduleBuilder.DefineType(type.Name, TypeAttributes.Public);
                    foreach (var f in type.Fields)
                    {
                        var fieldType = GetOrCreateRuntimeType(f.Type);
                        typeBuilder.DefineField(f.Name, fieldType, FieldAttributes.Public);
                    }
                    type.ClrType = typeBuilder.CreateType();
                    break;
                }
                default:
                {
                    type.ClrType = Type.GetType(type.Name);
                    break;
                }
            }
            return type.ClrType;
        }
    }
}
