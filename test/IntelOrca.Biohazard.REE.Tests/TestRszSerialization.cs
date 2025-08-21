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
        public void RE4_WEAPONPARTSCOMBINEDEFINITIONUSERDATA_Decode()
        {
            var path = "natives/stm/_chainsaw/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata.user.2";

            var repo = GetTypeRepository();
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE4, path)).ToBuilder(repo);

            var userData = RszSerializer.Deserialize<WeaponPartsCombineDefinitionUserdata>(input.Objects[0])!;

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

            var node = RszSerializer.Serialize(rszType, new WeaponPartsCombineDefinitionUserdata()
            {
                _Datas =
                {
                    new WeaponPartsCombineDefinition()
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

            var userData = Assert.IsType<RszStructNode>(node);
            var userDataDatas = Assert.IsType<RszArrayNode>(userData.Children[0]);
            var def = Assert.IsType<RszStructNode>(userDataDatas.Children[0]);
            var defItemId = Assert.IsType<RszDataNode>(def.Children[0]);
            var defTargetItemIds = Assert.IsType<RszArrayNode>(def.Children[1]);
            var defTargetItemIds0 = Assert.IsType<RszDataNode>(defTargetItemIds.Children[0]);
            var defTargetItemIds1 = Assert.IsType<RszDataNode>(defTargetItemIds.Children[1]);
            var defTargetItemIds2 = Assert.IsType<RszDataNode>(defTargetItemIds.Children[2]);

            Assert.Equal("chainsaw.WeaponPartsCombineDefinitionUserdata", userData.Type.Name);
            Assert.Equal("chainsaw.WeaponPartsCombineDefinition", def.Type.Name);

            Assert.Equal(116008000, defItemId.AsInt32());
            Assert.Equal(274835456, defTargetItemIds0.AsInt32());
            Assert.Equal(274837056, defTargetItemIds1.AsInt32());
            Assert.Equal(278035456, defTargetItemIds2.AsInt32());
        }

        private static RszTypeRepository GetTypeRepository()
        {
            var jsonPath = @"G:\apps\reasy\rszre4_reasy.json";
            var json = File.ReadAllBytes(jsonPath);
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }

        public class WeaponPartsCombineDefinitionUserdata
        {
            public List<WeaponPartsCombineDefinition> _Datas { get; set; } = [];
        }

        public class WeaponPartsCombineDefinition
        {
            public int _ItemId { get; set; }
            public List<int> _TargetItemIds { get; set; } = [];
        }
    }
}
