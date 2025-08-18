using System.Text;
using IntelOrca.Biohazard.REE.Variables;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestUvar : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE4_GLOBALVARIABLES()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_authoring/appsystem/globalvariables/globalvariables.uvar.3");
        }

        private void AssertRebuild(string gameName, string path)
        {
            var input = new UvarFile(_pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder();
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder();

            var oldSpan = input.Data.Span;
            var newSpan = output.Data.Span;
            var sb = new StringBuilder();
            for (var i = 0; i < oldSpan.Length; i++)
            {
                if (oldSpan[i] != newSpan[i])
                {
                    sb.AppendLine($"{i:X8}");
                }
            }

            // Check our new file is same size as old one (should be for most cases)
            Assert.Equal(input.Data.Length, output.Data.Length);
        }
    }
}
