using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestRszTypeCsharpWriter : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Generate_Simple()
        {
            AssertCode(GameNames.RE4, "chainsaw.WeaponPartsCombineDefinitionUserdata",
                """
                namespace chainsaw
                {
                    internal class WeaponPartsCombineDefinitionUserdata
                    {
                        public System.Collections.Generic.List<chainsaw.WeaponPartsCombineDefinition> _Datas { get; set; } = [];
                    }
                    internal class WeaponPartsCombineDefinition
                    {
                        public int _ItemId { get; set; }
                        public System.Collections.Generic.List<int> _TargetItemIds { get; set; } = [];
                    }
                }
                """);
        }

        [Fact]
        public void Generate_Inheritance()
        {
            AssertCode(GameNames.RE4, "chainsaw.RaderChartGuiSingleSettingData",
                """
                namespace chainsaw
                {
                    internal class RaderChartGuiSingleSettingData
                    {
                        public int _ItemId { get; set; }
                        public int _ColorPresetType { get; set; }
                        public System.Collections.Generic.List<chainsaw.RaderChartGuiSingleSettingData.Setting> _Settings { get; set; } = [];
                        internal class Setting
                        {
                            public int _Category { get; set; }
                            public IntelOrca.Biohazard.REE.Rsz.Native.Range _Range { get; set; }
                            public float _Rate { get; set; }
                            public System.Collections.Generic.List<chainsaw.StabilityEvaluationSetting> _StabilityEvaluationSettings { get; set; } = [];
                            public System.Collections.Generic.List<chainsaw.SpCategoryEvaluationSettingBase> _SpCategoryEvaluationSettings { get; set; } = [];
                        }
                    }
                    internal class StabilityEvaluationSetting
                    {
                        public int PartsItemId { get; set; }
                        public float Value { get; set; }
                    }
                    internal class SpCategoryEvaluationSettingBase
                    {
                        public float Value { get; set; }
                    }
                    internal class SpCategory00EvaluationSetting : SpCategoryEvaluationSettingBase
                    {
                        public int PartsItemId { get; set; }
                    }
                    internal class SpCategory01EvaluationSetting : SpCategoryEvaluationSettingBase
                    {
                        public int PartsItemId { get; set; }
                    }
                    internal class SpCategory02EvaluationSetting : SpCategoryEvaluationSettingBase
                    {
                        public int PartsItemId { get; set; }
                    }
                    internal class SpCategory03EvaluationSetting : SpCategoryEvaluationSettingBase
                    {
                    }
                }
                """);
        }

        private void AssertCode(string gameName, string typeName, string expected)
        {
            var actual = GenerateCode(gameName, typeName);
            Assert.Equal(Normalize(expected), Normalize(actual));
        }

        private string Normalize(string str)
        {
            return str.ReplaceLineEndings("\n").Trim();
        }

        private string GenerateCode(string gameName, string typeName)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var userData = repo.FromName(typeName)!;
            var csharpWriter = new RszTypeCsharpWriter();
            var output = csharpWriter.Generate(userData);
            return output;
        }
    }
}
