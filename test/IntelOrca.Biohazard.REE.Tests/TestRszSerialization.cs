using System.Collections.Immutable;
using System.ComponentModel;
using System.Numerics;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszSerialization : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Serialize_FieldType()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var rszType = repo.FromName("chainsaw.EnemyBurnParamUserData")!;

            AssertSerialize(new RszValueNode(RszFieldType.S32, new byte[] { 4, 0, 0, 0 }), RszFieldType.S32, new RszValueNode(RszFieldType.S32, new byte[] { 4, 0, 0, 0 }));
            AssertSerialize(new RszStringNode(), RszFieldType.String, null);
            AssertSerialize(new RszStringNode(""), RszFieldType.String, "");
            AssertSerialize(new RszStringNode("Resident Evil"), RszFieldType.String, "Resident Evil");
            AssertSerialize(new RszStringNode("Resident Evil"), RszFieldType.String, new RszStringNode("Resident Evil"));
            AssertSerialize(new RszResourceNode(), RszFieldType.Resource, null);
            AssertSerialize(new RszResourceNode(""), RszFieldType.Resource, "");
            AssertSerialize(new RszResourceNode("prefabs/test.pfb"), RszFieldType.Resource, "prefabs/test.pfb");
            AssertSerialize(new RszResourceNode("prefabs/test.pfb"), RszFieldType.Resource, new RszResourceNode("prefabs/test.pfb"));
            AssertSerialize(new RszUserDataNode(), RszFieldType.UserData, null);
            AssertSerialize(new RszUserDataNode(), RszFieldType.UserData, new RszUserDataNode());
            AssertSerialize(new RszUserDataNode(rszType, "userdata/burn.user"), RszFieldType.UserData, new RszUserDataNode(rszType, "userdata/burn.user"));

            static void AssertSerialize(object expected, RszFieldType type, object? value)
            {
                var actual = RszSerializer.Serialize(type, value);
                Assert.StrictEqual(expected, actual);
            }
        }

        [Fact]
        public void Reserialize_FieldType()
        {
            AssertReserialize<bool>(RszFieldType.Bool, false);
            AssertReserialize<bool>(RszFieldType.Bool, true);
            AssertReserialize<sbyte>(RszFieldType.S8, -30);
            AssertReserialize<byte>(RszFieldType.U8, 250);
            AssertReserialize<short>(RszFieldType.S16, 20000);
            AssertReserialize<ushort>(RszFieldType.U16, 0xFFFF);
            AssertReserialize<int>(RszFieldType.S32, 123456);
            AssertReserialize<uint>(RszFieldType.U32, 0xC4FB4A12);
            AssertReserialize<long>(RszFieldType.S64, -83785375383123456);
            AssertReserialize<ulong>(RszFieldType.U64, 0xFFFFFFFFC4FB4A12);
            AssertReserialize<float>(RszFieldType.F32, 0.42445f);
            AssertReserialize<double>(RszFieldType.F64, 0.42445222635);
            AssertReserialize<Vector2>(RszFieldType.Vec2, new Vector2(10, 22));
            AssertReserialize<Vector3>(RszFieldType.Vec3, new Vector3(10, 22, 33));
            AssertReserialize<Vector4>(RszFieldType.Vec4, new Vector4(10, 22, 33, 44));
            AssertReserialize<Quaternion>(RszFieldType.Quaternion, Quaternion.Identity);
            AssertReserialize<Guid>(RszFieldType.Guid, new Guid("63680a70-f2ce-4a41-83f3-485a22717d12"));
            AssertReserialize<Guid>(RszFieldType.GameObjectRef, new Guid("63680a70-f2ce-4a41-83f3-485a22717d12"));

            static void AssertReserialize<T>(RszFieldType type, T value)
            {
                var node = RszSerializer.Serialize(type, value);
                var actual = RszSerializer.Deserialize<T>(node);
                Assert.StrictEqual(value, actual);
            }
        }

        [Fact]
        public void RE4_WEAPONPARTSCOMBINEDEFINITIONUSERDATA_Decode()
        {
            var path = "natives/stm/_chainsaw/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata.user.2";

            var repo = GetTypeRepository();
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE4, path)).ToBuilder(repo);

            var userData = RszSerializer.Deserialize<chainsaw.WeaponPartsCombineDefinitionUserdata>(input.Objects[0])!;

            Assert.Equal(116000000, userData._Datas[0]._ItemId);
            Assert.Equal(275475456, userData._Datas[0]._TargetItemIds[0]);
            Assert.Equal(275477056, userData._Datas[0]._TargetItemIds[1]);
            Assert.Equal(275158656, userData._Datas[0]._TargetItemIds[2]);
            Assert.Equal(275478656, userData._Datas[0]._TargetItemIds[3]);
            Assert.Equal(116008000, userData._Datas[6]._ItemId);
            Assert.Equal(274835456, userData._Datas[6]._TargetItemIds[0]);
            Assert.Equal(274837056, userData._Datas[6]._TargetItemIds[1]);
            Assert.Equal(278035456, userData._Datas[6]._TargetItemIds[2]);
        }

        [Fact]
        public void RE4_WEAPONPARTSCOMBINEDEFINITIONUSERDATA_Encode()
        {
            var repo = GetTypeRepository();
            var rszType = repo.FromName("chainsaw.WeaponPartsCombineDefinitionUserdata");
            Assert.NotNull(rszType);

            var node = RszSerializer.Serialize(rszType, new chainsaw.WeaponPartsCombineDefinitionUserdata()
            {
                _Datas =
                {
                    new chainsaw.WeaponPartsCombineDefinition()
                    {
                        _ItemId = 116008000,
                        _TargetItemIds =
                        {
                            274835456,
                            274837056,
                            278035456
                        }
                    }
                }
            });

            var userData = Assert.IsType<RszObjectNode>(node);
            var userDataDatas = Assert.IsType<RszArrayNode>(userData.Children[0]);
            var def = Assert.IsType<RszObjectNode>(userDataDatas.Children[0]);
            var defItemId = Assert.IsType<RszValueNode>(def.Children[0]);
            var defTargetItemIds = Assert.IsType<RszArrayNode>(def.Children[1]);
            var defTargetItemIds0 = Assert.IsType<RszValueNode>(defTargetItemIds.Children[0]);
            var defTargetItemIds1 = Assert.IsType<RszValueNode>(defTargetItemIds.Children[1]);
            var defTargetItemIds2 = Assert.IsType<RszValueNode>(defTargetItemIds.Children[2]);

            Assert.Equal("chainsaw.WeaponPartsCombineDefinitionUserdata", userData.Type.Name);
            Assert.Equal("chainsaw.WeaponPartsCombineDefinition", def.Type.Name);

            Assert.Equal(116008000, defItemId.Get<int>());
            Assert.Equal(274835456, defTargetItemIds0.Get<int>());
            Assert.Equal(274837056, defTargetItemIds1.Get<int>());
            Assert.Equal(278035456, defTargetItemIds2.Get<int>());
        }

        [Fact]
        [Description("Test a range of different target types to serialize objects/collections to.")]
        public void RE4_ITEMCRAFTSETTINGUSERDATA()
        {
            var path = "natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2";

            var repo = GetTypeRepository();
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE4, path));
            var inputBuilder = input.ToBuilder(repo);
            var root = inputBuilder.Objects[0];
            var rootRszType = root.Type;
            var userData = RszSerializer.Deserialize<chainsaw.ItemCraftSettingUserdata>(root)!;
            inputBuilder.Objects = [(RszObjectNode)RszSerializer.Serialize(rootRszType, userData)];
            var output = inputBuilder.Build();
            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
        }

        private static RszTypeRepository GetTypeRepository()
        {
            var jsonPath = @"G:\apps\reasy\rszre4_reasy.json";
            var json = File.ReadAllBytes(jsonPath);
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }
    }
}

namespace chainsaw
{
    public class WeaponPartsCombineDefinitionUserdata
    {
        public List<WeaponPartsCombineDefinition> _Datas { get; set; } = [];
    }

    public class WeaponPartsCombineDefinition
    {
        public int _ItemId { get; set; }
        public List<int> _TargetItemIds { get; set; } = [];
    }

    public class ItemCraftSettingUserdata
    {
        public ImmutableArray<int> _MaterialItemIds { get; set; } = [];
        public RszArrayNode _RecipeIdOrders { get; set; } = new RszArrayNode(RszFieldType.S32, []);
        public ItemCraftRecipe[] _Datas { get; set; } = [];
    }

    public class ItemCraftRecipe
    {
        public ItemCraftResultSetting[] _ResultSettings { get; set; } = [];
        public IRszNode? _RequiredItems { get; set; }
        public IRszNodeContainer? _BonusSetting { get; set; }
        public int _RecipeID { get; set; }
        public int _Category { get; set; }
        public IRszNode _CraftTime { get; set; } = RszSerializer.Serialize(RszFieldType.S32, 0);
        public bool _DrawWave { get; set; }
    }

    public class ItemCraftResultSetting
    {
        public int _Difficulty { get; set; }
        public RszObjectNode? _Result { get; set; }
    }
}
