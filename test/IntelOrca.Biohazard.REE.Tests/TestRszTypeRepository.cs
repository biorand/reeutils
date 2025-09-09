using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszTypeRepository : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void FromId()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var node = repo.FromId(1575112409);
            Assert.NotNull(node);
            Assert.Equal(1575112409U, node.Id);
            Assert.Equal("chainsaw.WeaponPartsCombineDefinition", node.Name);
        }

        [Fact]
        public void FromName()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var node = repo.FromName("chainsaw.WeaponPartsCombineDefinition");
            Assert.NotNull(node);
            Assert.Equal(1575112409U, node.Id);
            Assert.Equal("chainsaw.WeaponPartsCombineDefinition", node.Name);
        }

        [Fact]
        public void Inheritance()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);

            var spCategoryBase = repo.FromName("chainsaw.SpCategoryEvaluationSettingBase")!;
            var spCategory00 = repo.FromName("chainsaw.SpCategory00EvaluationSetting")!;
            Assert.Same(spCategoryBase, spCategory00.Parent);
            Assert.Equal(4, spCategoryBase.Children.Count());
        }

        [Fact]
        public void Create_WeaponPartsCombineDefinition()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var node = repo.Create("chainsaw.WeaponPartsCombineDefinition");

            Assert.StrictEqual(new RszValueNode(RszFieldType.S32, new byte[] { 0, 0, 0, 0 }), node["_ItemId"]);
            var arrayNode = Assert.IsType<RszArrayNode>(node["_TargetItemIds"]);
            Assert.Equal(RszFieldType.S32, arrayNode.Type);
            Assert.Equal(0, arrayNode.Length);
        }

        [Fact]
        public void Create_AttackData()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var node = repo.Create("chainsaw.collision.AttackHitUserData.AttackData");

            // Basic types
            Assert.StrictEqual(new RszValueNode(RszFieldType.Bool, new byte[] { 0 }), node["_CheckRigidBody"]);
            Assert.StrictEqual(new RszValueNode(RszFieldType.F32, new byte[] { 0, 0, 0, 0 }), node["_EffectCheckInterval"]);
            Assert.StrictEqual(new RszValueNode(RszFieldType.U32, new byte[] { 0, 0, 0, 0 }), node["_EffectJointNameHash"]);
            Assert.StrictEqual(new RszValueNode(RszFieldType.S32, new byte[] { 0, 0, 0, 0 }), node["_BreakLevel"]);

            // Other types
            Assert.StrictEqual(new RszUserDataNode(), node["_AttackToEnemyUserData"]);
            Assert.StrictEqual(new RszUserDataNode(), node["_AttackToPlayerUserData"]);
            Assert.StrictEqual(new RszUserDataNode(), node["_AttackToGimmickUserData"]);

            // Object type
            var subObjectNode = Assert.IsType<RszObjectNode>(node["_OffsetAttackDirection"]);
            Assert.Same(repo.FromName("chainsaw.collision.AttackHitUserData.AttackData.OffsetDirection"), subObjectNode.Type);
            Assert.StrictEqual(new RszValueNode(RszFieldType.Bool, new byte[] { 0 }), subObjectNode["_Inverce"]);
        }
    }
}
