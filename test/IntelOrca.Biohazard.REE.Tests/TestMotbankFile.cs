using IntelOrca.Biohazard.REE.Animation;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestMotbankFile : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE9_CH0200()
        {
            AssertRebuild(GameNames.RE9, "natives/stm/animation/ch/ch02/ch0200/motbank/ch0200.motbank.4");
        }

        [Fact]
        public void Rebuild_RE9_CH0200fps()
        {
            var data = _pakHelper.GetFileData(GameNames.RE9, "natives/stm/animation/ch/ch02/ch0200/motbank/ch0200fps.motbank.4");
            var builder = new MotbankFile(data).ToBuilder();
            Assert.Equal("AppSystem/Character/CharacterPrefab/PlayerCommon/UserVariables/PlayerCommonMotionUserVariables.uvar", builder.UvarPath);
            Assert.Equal("GameAssets/Character/CharacterPrefab/cp_A1/cp_A100/JointMap/cp_A1JointMap_ForConvert.jmap", builder.JmapPath);
            builder.Items.Add(new MotbankFile.MotlistItem()
            {
                Path = "example",
                BankId = 100,
                BankType = 257,
                MaskBits = 3071
            });

            var output = builder.Build();
            Assert.Equal(57, output.Items.Length);

            var item56 = output.Items[56];
            Assert.Equal("example", item56.Path);
            Assert.Equal(100L, item56.BankId);
            Assert.Equal(257U, item56.BankType);
            Assert.Equal(3071L, item56.MaskBits);
        }

        private void AssertRebuild(string gameName, string path)
        {
            var data = _pakHelper.GetFileData(gameName, path);
            var input = new MotbankFile(data);
            var inputBuilder = input.ToBuilder();
            var output = inputBuilder.Build();

            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            Assert.Equal(input.Data.Length, output.Data.Length);
        }
    }
}
