using System.Collections.Immutable;
using IntelOrca.Biohazard.REE.Rsz;
using Xunit.Sdk;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestBhvt : IDisposable
    {
        private const int Version = 40;

        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;
        private readonly ImmutableArray<string> _fsmv2Paths;

        public TestBhvt()
        {
            _fsmv2Paths = [.. _pakHelper.GetPakList(GameNames.RE8).Entries.Where(p => p.EndsWith(".fsmv2.40"))];
        }

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Read_All_RE8_FSMV2_Files()
        {
            Assert.Equal(2026, _fsmv2Paths.Length);
            foreach (var path in _fsmv2Paths)
            {
                AssertRead(path);
            }
        }

        [Fact]
        public void Read_Build_All_RE8_FSMV2_Files()
        {
            Assert.Equal(2026, _fsmv2Paths.Length);
            foreach (var path in _fsmv2Paths)
            {
                AssertReadBuild(path);
            }
        }

        private void AssertRead(string path)
        {
            try
            {
                var data = _pakHelper.GetFileData(GameNames.RE8, path);
                var repository = _pakHelper.GetTypeRepository(GameNames.RE8);

                var file = new BhvtFile(Version, data);
                var tree = file.ReadTree(repository);

                Assert.Equal("root", tree.Name);
            }
            catch (SkipException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse BHVT file '{path}'.", ex);
            }
        }

        private void AssertReadBuild(string path)
        {
            try
            {
                var data = _pakHelper.GetFileData(GameNames.RE8, path);
                var repository = _pakHelper.GetTypeRepository(GameNames.RE8);

                var file = new BhvtFile(Version, data);
                var originalTree = file.ReadTree(repository);

                // Build() fully regenerates the file from the tree rather than preserving the original
                // bytes (unlike HfsmFile), so round-trip correctness is checked semantically: re-reading
                // the rebuilt file must produce the same node names, resolved actions/conditions/selectors,
                // and state/transition/allstate targets as the original.
                var rebuilt = file.ToBuilder(repository).Build();
                var rebuiltTree = new BhvtFile(Version, rebuilt.Data).ReadTree(repository);

                AssertSameTree(path, originalTree, rebuiltTree);
            }
            catch (SkipException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to rebuild BHVT file '{path}'.", ex);
            }
        }

        private static void AssertSameTree(string path, BhvtNode expected, BhvtNode actual)
        {
            var expectedDump = Dump(expected);
            var actualDump = Dump(actual);
            if (expectedDump != actualDump)
            {
                Assert.Fail($"BHVT rebuild changed the semantic content of '{path}'.\n--- expected ---\n{expectedDump}\n--- actual ---\n{actualDump}");
            }
        }

        private static string Dump(BhvtNode node)
        {
            static string ActionId(RszObjectNode obj) =>
                obj.Children.Length > 1 && obj.Children[1] is RszValueNode v ? v.ToString() ?? "?" : "?";

            var sb = new System.Text.StringBuilder();
            void Visit(BhvtNode n)
            {
                sb.Append(n.Name).Append('|')
                  .Append(n.Selector?.Type.Name).Append('|')
                  .Append(string.Join(",", n.Actions.Select(a => $"{a.Instance.Type.Name}:{ActionId(a.Instance)}"))).Append('|')
                  .Append(string.Join(",", n.States.Select(s => $"{s.Target}->{s.Condition?.Type.Name}"))).Append('|')
                  .Append(string.Join(",", n.Transitions.Select(t => $"->{t.Start}"))).Append('|')
                  .Append(string.Join(",", n.AllStates.Select(s => $"->{s.Target}:{s.Condition?.Type.Name}"))).Append('\n');
                foreach (var c in n.Children) Visit(c.Node);
            }
            Visit(node);
            return sb.ToString();
        }
    }
}
