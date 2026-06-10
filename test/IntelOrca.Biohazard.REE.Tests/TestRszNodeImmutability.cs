using System;
using System.Collections.Immutable;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszNodeImmutability
    {
        [Fact]
        public void Folder_WithName_DoesNotMutateOriginal()
        {
            var folder = new RszFolder(
                CreateObjectNode("via.Folder", ("Name", new RszStringNode("Old Folder"))),
                []);

            var updated = folder.WithName("New Folder");

            Assert.Equal("Old Folder", folder.Name);
            Assert.Equal("New Folder", updated.Name);
            Assert.NotSame(folder.Settings, updated.Settings);
        }

        [Fact]
        public void GameObject_WithSettings_DoesNotMutateOriginal()
        {
            var gameObject = new RszGameObject(
                Guid.NewGuid(),
                null,
                CreateObjectNode("via.GameObject", ("Name", new RszStringNode("Original"))),
                [],
                []);

            var updatedSettings = gameObject.Settings.SetField("Name", new RszStringNode("Updated"));
            var updated = gameObject.WithSettings(updatedSettings);

            Assert.Equal("Original", gameObject.Name);
            Assert.Equal("Updated", updated.Name);
            Assert.NotSame(gameObject.Settings, updated.Settings);
        }

        [Fact]
        public void ObjectNode_SetField_DoesNotMutateOriginal()
        {
            var node = CreateObjectNode(
                "tests.Object",
                ("Value", new RszStringNode("before")));

            var updated = node.SetField("Value", new RszStringNode("after"));

            Assert.Equal("before", ((RszStringNode)node["Value"]).Value);
            Assert.Equal("after", ((RszStringNode)updated["Value"]).Value);
            Assert.NotSame(node, updated);
        }

        [Fact]
        public void ArrayNode_SetItem_DoesNotMutateOriginal()
        {
            var node = new RszArrayNode(
                RszFieldType.S32,
                [
                    RszSerializer.Serialize(RszFieldType.S32, 1),
                    RszSerializer.Serialize(RszFieldType.S32, 2)
                ]);

            var updated = node.SetItem(1, 3);

            Assert.Equal(2, ((RszValueNode)node[1]).Get<int>());
            Assert.Equal(3, ((RszValueNode)updated[1]).Get<int>());
            Assert.NotSame(node, updated);
        }

        private static RszObjectNode CreateObjectNode(string typeName, params (string Name, IRszNode Value)[] fields)
        {
            var repository = new RszTypeRepository();
            var type = new RszType
            {
                Repository = repository,
                Id = 1,
                Crc = 1,
                TypeName = new RszTypeName(typeName),
                Fields = fields
                    .Select(x => new RszTypeField
                    {
                        Name = x.Name,
                        Type = InferFieldType(x.Value),
                        Size = x.Value is RszValueNode valueNode ? valueNode.Data.Length : 0
                    })
                    .ToImmutableArray()
            };
            repository.AddType(type);
            return new RszObjectNode(type, fields.Select(x => x.Value).ToImmutableArray());
        }

        private static RszFieldType InferFieldType(IRszNode value) =>
            value switch
            {
                RszStringNode => RszFieldType.String,
                RszResourceNode => RszFieldType.Resource,
                RszUserDataNode => RszFieldType.UserData,
                RszValueNode valueNode => valueNode.Type,
                RszArrayNode arrayNode => arrayNode.Type,
                _ => RszFieldType.Object
            };
    }
}
