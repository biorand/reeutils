using System.Collections.Immutable;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszSerialization : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Serialize_ObjectType()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var value = new chainsaw.WeaponPartsCombineDefinition
            {
                _ItemId = 2,
            };
            var rszValue = RszSerializer.Serialize(RszFieldType.Object, value, repo);
            Assert.Equal(2, rszValue.Get<int>("_ItemId"));
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
            AssertReserialize<Matrix4x4>(RszFieldType.Mat4, Matrix4x4.Identity);
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
        public void Reserialize_NativeFieldType()
        {
            AssertReserialize(RszFieldType.Uint2, new via.Uint2 { x = 1, y = 2 });
            AssertReserialize(RszFieldType.Uint3, new via.Uint3 { x = 1, y = 2, z = 3 });
            AssertReserialize(RszFieldType.Uint4, new via.Uint4 { x = 1, y = 2, z = 3, w = 4 });
            AssertReserialize(RszFieldType.Int2, new via.Int2(1, 2));
            AssertReserialize(RszFieldType.Int3, new via.Int3 { x = 1, y = 2, z = 3 });
            AssertReserialize(RszFieldType.Int4, new via.Int4 { x = 1, y = 2, z = 3, w = 4 });
            AssertReserialize(RszFieldType.Color, new via.Color(0x44332211));
            AssertReserialize(RszFieldType.AABB, new via.AABB(new Vector3(1, 2, 3), new Vector3(4, 5, 6)));
            AssertReserializeCapsule(CreateValueNode(
                RszFieldType.Capsule,
                CreateBytes(
                    new Vector3(1, 2, 3),
                    16,
                    new Vector3(4, 5, 6),
                    16,
                    7f,
                    12)));
            AssertReserialize(RszFieldType.TaperedCapsule, new via.TaperedCapsule { VertexRadiusA = new Vector4(1, 2, 3, 4), VertexRadiusB = new Vector4(5, 6, 7, 8) });
            AssertReserialize(RszFieldType.Cone, new via.Cone(new Vector3(1, 2, 3), 4, new Vector3(5, 6, 7), 8));
            AssertReserialize(RszFieldType.Line, new via.Line(new Vector3(1, 2, 3), new Vector3(4, 5, 6)));
            AssertReserialize(RszFieldType.LineSegment, new via.LineSegment(new Vector3(1, 2, 3), new Vector3(4, 5, 6)));
            AssertReserialize(RszFieldType.OBB, new via.OBB(Matrix4x4.CreateTranslation(1, 2, 3), new Vector3(4, 5, 6)));
            AssertReserialize(RszFieldType.Plane, new via.Plane(1, 2, 3, 4));
            AssertReserialize(RszFieldType.PlaneXZ, new via.PlaneXZ { dist = 5 });
            AssertReserialize(RszFieldType.Point, new via.Point { x = 1, y = 2 });
            AssertReserializeValue(RszFieldType.Range, CreateValueNode(RszFieldType.Range, CreateBytes(1f, 2f)));
            AssertReserialize(RszFieldType.RangeI, new via.RangeI { r = 1, s = 2 });
            AssertReserialize(RszFieldType.Ray, new via.Ray { from = new Vector3(1, 2, 3), dir = new Vector3(4, 5, 6) });
            AssertReserialize(RszFieldType.RayY, new via.RayY { from = new Vector3(1, 2, 3), dir = 4 });
            AssertReserialize(RszFieldType.Segment, new via.Segment { from = new Vector4(1, 2, 3, 4), dir = new Vector3(5, 6, 7) });
            AssertReserialize(RszFieldType.Size, new via.Size { w = 1, h = 2 });
            AssertReserialize(RszFieldType.Sphere, new via.Sphere(new Vector3(1, 2, 3), 4));
            AssertReserialize(RszFieldType.Triangle, new via.Triangle { p0 = new Vector3(1, 2, 3), p1 = new Vector3(4, 5, 6), p2 = new Vector3(7, 8, 9) });
            AssertReserialize(RszFieldType.Cylinder, new via.Cylinder(new Vector3(1, 2, 3), new Vector3(4, 5, 6), 7));
            AssertReserialize(RszFieldType.Ellipsoid, new via.Ellipsoid { pos = new Vector3(1, 2, 3), r = new Vector3(4, 5, 6) });
            AssertReserialize(RszFieldType.Area, new via.Area { p0 = new Vector2(1, 2), p1 = new Vector2(3, 4), p2 = new Vector2(5, 6), p3 = new Vector2(7, 8), height = 9, bottom = 10 });
            AssertReserialize(RszFieldType.Torus, new via.Torus { pos = new Vector3(1, 2, 3), r = 4, axis = new Vector3(5, 6, 7), cr = 8 });
            AssertReserialize(RszFieldType.Rect, new via.Rect(1, 2, 3, 4));
            AssertReserialize(RszFieldType.Rect3D, new via.Rect3D { normal = new Vector3(1, 2, 3), sizeW = 4, center = new Vector3(5, 6, 7), sizeH = 8 });
            AssertReserialize(RszFieldType.Frustum, new via.Frustum
            {
                plane0 = new via.Plane(1, 0, 0, 1),
                plane1 = new via.Plane(0, 1, 0, 2),
                plane2 = new via.Plane(0, 0, 1, 3),
                plane3 = new via.Plane(-1, 0, 0, 4),
                plane4 = new via.Plane(0, -1, 0, 5),
                plane5 = new via.Plane(0, 0, -1, 6),
            });
            AssertReserializeValue(RszFieldType.KeyFrame, CreateValueNode(RszFieldType.KeyFrame, CreateBytes(1f, 2u, 3u, 4u)));
            AssertReserialize(RszFieldType.Sfix, new via.sfix { v = 1 });
            AssertReserialize(RszFieldType.Sfix2, new via.Sfix2 { x = new via.sfix { v = 1 }, y = new via.sfix { v = 2 } });
            AssertReserialize(RszFieldType.Sfix3, new via.Sfix3 { x = new via.sfix { v = 1 }, y = new via.sfix { v = 2 }, z = new via.sfix { v = 3 } });
            AssertReserialize(RszFieldType.Sfix4, new via.Sfix4 { x = new via.sfix { v = 1 }, y = new via.sfix { v = 2 }, z = new via.sfix { v = 3 }, w = new via.sfix { v = 4 } });
            AssertReserialize(RszFieldType.Position, new via.Position { x = 1, y = 2, z = 3 });

            static void AssertReserialize<T>(RszFieldType type, T value) where T : struct
            {
                var node = RszSerializer.Serialize(type, value);
                var actual = RszSerializer.Deserialize<T>(node);
                var reserialized = RszSerializer.Serialize(type, actual);
                Assert.StrictEqual(node, reserialized);
            }

            static void AssertReserializeValue(RszFieldType type, RszValueNode node)
            {
                var actual = RszSerializer.Deserialize(node);
                var reserialized = Assert.IsType<RszValueNode>(RszSerializer.Serialize(type, actual));
                Assert.StrictEqual(node, reserialized);
            }

            static void AssertReserializeCapsule(RszValueNode node)
            {
                var actual = RszSerializer.Deserialize(node);
                var reserialized = Assert.IsType<RszValueNode>(RszSerializer.Serialize(RszFieldType.Capsule, actual));
                var roundTripped = RszSerializer.Deserialize(reserialized);

                Assert.Equal(
                    actual.GetType().GetField("Start")!.GetValue(actual),
                    roundTripped.GetType().GetField("Start")!.GetValue(roundTripped));
                Assert.Equal(
                    actual.GetType().GetField("End")!.GetValue(actual),
                    roundTripped.GetType().GetField("End")!.GetValue(roundTripped));
                Assert.Equal(
                    actual.GetType().GetField("Radius")!.GetValue(actual),
                    roundTripped.GetType().GetField("Radius")!.GetValue(roundTripped));
            }

            static RszValueNode CreateValueNode(RszFieldType type, byte[] data) => new RszValueNode(type, data);

            static byte[] CreateBytes(params object[] values)
            {
                var data = new List<byte>();
                foreach (var value in values)
                {
                    switch (value)
                    {
                        case float f:
                            data.AddRange(BitConverter.GetBytes(f));
                            break;
                        case uint u:
                            data.AddRange(BitConverter.GetBytes(u));
                            break;
                        case Vector3 v3:
                            data.AddRange(MemoryMarshal.AsBytes(new[] { v3 }.AsSpan()).ToArray());
                            break;
                        case int padding:
                            data.AddRange(new byte[padding]);
                            break;
                        default:
                            throw new NotSupportedException(value.GetType().FullName);
                    }
                }
                return [.. data];
            }
        }

        [Fact]
        public void RE4_WEAPONPARTSCOMBINEDEFINITIONUSERDATA_Decode()
        {
            var path = "natives/stm/_chainsaw/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata.user.2";

            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
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
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
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

            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE4, path));
            var inputBuilder = input.ToBuilder(repo);
            var root = inputBuilder.Objects[0];
            var rootRszType = root.Type;
            var userData = RszSerializer.Deserialize<chainsaw.ItemCraftSettingUserdata>(root)!;
            inputBuilder.Objects = [(RszObjectNode)RszSerializer.Serialize(rootRszType, userData)];
            var output = inputBuilder.Build();
            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
        }

        /// <summary>
        /// Round-trip of ItemAppointSetting with generic ContextIDRef<T>.
        /// </summary>
        [Fact]
        public void RE9_ITEMAPPOINTSETTING_RoundTrip()
        {
            var path = "natives/stm/leveldesign/item/userdata/itemappointment/it60_00_002.user.3";

            var repo = _pakHelper.GetTypeRepository(GameNames.RE9);
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE9, path));
            var inputBuilder = input.ToBuilder(repo);
            var root = inputBuilder.Objects[0];
            var rootRszType = root.Type;

            var userData = RszSerializer.Deserialize<app.ItemAppointSetting>(root)!;

            inputBuilder.Objects = [(RszObjectNode)RszSerializer.Serialize(rootRszType, userData)];
            var output = inputBuilder.Build();

            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
        }

        [Fact]
        public void RE9_CRAFTRECIPECATALOGUSERDATA()
        {
            var path = "natives/stm/leveldesign/item/userdata/craftrecipecataloguserdata.user.3";

            var repo = _pakHelper.GetTypeRepository(GameNames.RE9);
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE9, path));
            var inputBuilder = input.ToBuilder(repo);
            var root = inputBuilder.Objects[0];
            var rootRszType = root.Type;
            var userData = RszSerializer.Deserialize<app.CraftRecipeCatalogUserData>(root)!;

            var m = Assert.IsType<app.CraftRecipe.MaterialItemData_Basic>(userData._Recipes[22]._MaterialItems[0]);
            Assert.Equal(1, m._Stock);

            inputBuilder.Objects = [(RszObjectNode)RszSerializer.Serialize(rootRszType, userData)];
            var output = inputBuilder.Build();
            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
        }
    }
}

namespace chainsaw
{
    public class WeaponPartsCombineDefinitionUserdata
    {
        public static WeaponPartsCombineDefinitionUserdata Default => new();

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

namespace app
{
    internal class ItemAppointSetting
    {
        public List<AppointGimmickData> _AppointGimmicks { get; set; } = [];

        internal class AppointGimmickData
        {
            public ContextIDRef<GimmickCore> _Target { get; set; } = null!;
        }
    }

    internal class AppUserdataBase
    {
    }

    internal class AppObjectBase
    {
    }

    internal class ContextIDRef<T> : AppObjectBase
    {
        public Guid _RawContextID { get; set; }
    }

    internal class GimmickCore : AppObjectBase
    {
    }

    internal class CraftRecipeCatalogUserData
    {
        public RszUserDataNode _SoundCatalogUserData { get; set; } = new();
        public System.Collections.Generic.List<app.CraftRecipe> _Recipes { get; set; } = [];
    }
    internal class CraftRecipe
    {
        public string _RecipeIDStr { get; set; } = "";
        public string _RecipeSectionID { get; set; } = "";
        public bool _IsStartupRecipe { get; set; }
        public int _UnlockCondition { get; set; }
        public string _UnlockDifficulty { get; set; } = "";
        public System.Collections.Generic.List<MaterialItemDataBase> _MaterialItems { get; set; } = [];
        public System.Collections.Generic.List<ProductItemDataBase> _ProductItems { get; set; } = [];
        public System.Collections.Generic.List<IgnoreCraftingCharacterSetting> _IgnoreCraftingCharacterSettings { get; set; } = [];
        internal class IgnoreCraftingCharacterSetting
        {
            public string _IgnorePlayerID { get; set; } = "";
        }
        internal class MaterialItemData_Basic : MaterialItemDataBase
        {
            public int _Stock { get; set; }
        }
        internal class MaterialItemData_Contained : MaterialItemDataBase
        {
            public int _Stock { get; set; }
            public System.Collections.Generic.List<OptionalData> _DiscountData { get; set; } = [];
        }
        internal class MaterialItemDataBase
        {
            public string _ItemID { get; set; } = "";
        }
        internal class ProductItemData_Basic : ProductItemDataBase
        {
            public int _Stock { get; set; }
        }
        internal class ProductItemData_Loadable : ProductItemDataBase
        {
            public int _LoadingCount { get; set; }
            public int _LoadingType { get; set; }
        }
        internal class ProductItemDataBase
        {
            public string _ItemID { get; set; } = "";
        }
        internal class OptionalData
        {
            public string _EffectiveItemID { get; set; } = "";
            public int _Value { get; set; }
        }
    }
}
