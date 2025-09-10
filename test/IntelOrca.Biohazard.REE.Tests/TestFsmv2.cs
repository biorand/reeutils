using IntelOrca.Biohazard.REE.Fsm;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestFsmv2 : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE8_C02_2_MAIN()
        {
            AssertRebuild(GameNames.RE8, "natives/stm/leveldesign/mainflowfsm/chapter2_2/c02_2_main.fsmv2.40");
        }

        private void AssertRebuild(string gameName, string path, int? expectedLength = null)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new Fsmv2File(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var stateNames = input.StateNames;
            input.ReadCoreData();
            var rsz = Enumerable
                .Range(1, 11)
                .Select(i => input.GetRsz((Fsmv2File.OffsetKind)i))
                .Select(x => x.ReadObjectList(repo))
                .ToArray();

            // var inputBuilder = input.ToBuilder(repo);
            // var output = inputBuilder.Build();
            // var outputBuilder = output.ToBuilder(repo);
            // 
            // if (input.Data.Length == output.Data.Length)
            // {
            //     Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            // }
            // Assert.Equal(expectedLength ?? input.Data.Length, output.Data.Length);
        }
    }
}
