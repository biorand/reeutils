using System.Collections.Immutable;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Fsm;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestHfsm : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = new();
        private readonly ImmutableArray<string> _fsmPaths;

        public TestHfsm()
        {
            _fsmPaths = [.. _pakHelper.GetPakList(GameNames.RE7).Entries.Where(p => p.EndsWith(".fsm.16"))];
        }

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Read_All_RE7_HFSM_Files()
        {
            Assert.Equal(2941, _fsmPaths.Length);
            foreach (var path in _fsmPaths)
            {
                AssertRead(path);
            }
        }

        [Fact]
        public void Read_Write_All_RE7_HFSM_Files()
        {
            Assert.Equal(2941, _fsmPaths.Length);
            foreach (var path in _fsmPaths)
            {
                AssertReadWrite(path);
            }
        }

        [Fact]
        public void Export_Import_RE7_HFSM_Graph_Json()
        {
            var path = _fsmPaths.First();
            var data = _pakHelper.GetFileData(GameNames.RE7, path);
            var graph = HfsmGraphDocument.FromFile(new HfsmFile(data));
            var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
            var importedGraph = JsonSerializer.Deserialize<HfsmGraphDocument>(json)!;
            var rebuilt = importedGraph.Build();

            AssertSameBytes(path, data, rebuilt.Data.Span);
            Assert.StartsWith("digraph HFSM", graph.ToDot());
        }

        private void AssertRead(string path)
        {
            try
            {
                var data = _pakHelper.GetFileData(GameNames.RE7, path);
                Assert.Equal(FileKind.HFSM, FileVersion.DetectFileKind(data));

                var hfsm = new HfsmFile(data);
                Assert.Equal(16, hfsm.Version);
                Assert.Equal(hfsm.StateDataEntryCount, hfsm.StateEntries.Length);
                Assert.All(hfsm.Sections, section =>
                {
                    Assert.InRange(section.Offset, 0, data.Length);
                    Assert.InRange(section.Offset + section.Size, section.Offset, data.Length);
                });

                _ = hfsm.ActionData.InstanceCount;
                _ = hfsm.ConditionData.InstanceCount;
                _ = hfsm.SelectorData.InstanceCount;
                _ = hfsm.ExpressionData.InstanceCount;
                _ = hfsm.UserVariables.VariableCount;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse HFSM file '{path}'.", ex);
            }
        }

        private void AssertReadWrite(string path)
        {
            try
            {
                var data = _pakHelper.GetFileData(GameNames.RE7, path);
                var hfsm = new HfsmFile(data);
                var graph = HfsmGraphDocument.FromFile(hfsm);
                var rebuilt = graph.Build();

                AssertSameBytes(path, data, rebuilt.Data.Span);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to rebuild HFSM file '{path}'.", ex);
            }
        }

        private static void AssertSameBytes(string path, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
        {
            if (expected.SequenceEqual(actual))
                return;

            var minLength = Math.Min(expected.Length, actual.Length);
            for (var i = 0; i < minLength; i++)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(
                        $"HFSM rebuild changed '{path}' at 0x{i:X}: expected 0x{expected[i]:X2}, actual 0x{actual[i]:X2}. " +
                        $"Expected length 0x{expected.Length:X}, actual length 0x{actual.Length:X}.");
                }
            }

            Assert.Fail(
                $"HFSM rebuild changed '{path}' length. Expected length 0x{expected.Length:X}, actual length 0x{actual.Length:X}.");
        }
    }
}
