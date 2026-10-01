using System.Text.Json.Nodes;
using IntelOrca.Biohazard.REE.Compression;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszUnreferencedInstances : IDisposable
    {
        private const string ColliderTypeId = "15d3b4ec";
        private const string GimmickPath = "natives/x64/objectroot/scene/location/rpd/level_100/environments/st4_701_0/gimmick.scn.19";

        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Vanilla_RE2_Gimmick_HasNoUnreferencedInstances()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE2);
            var scn = new ScnFile(FileVersion.FromPath(GimmickPath), _pakHelper.GetFileData(GameNames.RE2, GimmickPath));
            Assert.Empty(scn.Rsz.FindUnreferencedInstances(repo));
        }

        [Fact]
        public void Vanilla_RE2_LeonS020200_HasNoUnreferencedInstances()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE2);
            var scn = ReadLeonS020200();
            Assert.Empty(scn.Rsz.FindUnreferencedInstances(repo));
        }

        [Fact]
        public void MistypedPointerFields_LeaveUnreferencedInstances()
        {
            var scn = ReadLeonS020200();
            var mistyped = MistypeColliderPointers();
            var orphans = scn.Rsz.FindUnreferencedInstances(mistyped);
            Assert.NotEmpty(orphans);
            Assert.All(orphans, id => Assert.True(id.Index > 0));
        }

        private ScnFile ReadLeonS020200()
        {
            var path = Environment.GetEnvironmentVariable("REEUTILS_RE2_LEON_S02_0200")
                ?? @"E:\Game Files\PAK Files\RE2 Remake Non-RT\natives\x64\objectroot\scene\scenario\scenariono\rpd\scenario\leon_s02_0200.scn.19";
            if (!File.Exists(path))
            {
                Assert.Skip($"Skipping because required scene '{path}' was not found.");
            }
            return new ScnFile(FileVersion.FromPath(path), File.ReadAllBytes(path));
        }

        /// <summary>
        /// Loads the RE2 repository with via.physics.Collider's Shape / FilterInfo pointer fields
        /// retyped from Object to U32 (same size and alignment, so the layout still decodes).
        /// </summary>
        private RszTypeRepository MistypeColliderPointers()
        {
            var dataPath = Path.Combine(OriginalPakHelper.GetRepoDataPath(), "rszre2.json.gz");
            var json = JsonNode.Parse(Gzip.DecompressData(File.ReadAllBytes(dataPath)))!;
            foreach (var field in json[ColliderTypeId]!["fields"]!.AsArray())
            {
                var name = field!["name"]!.GetValue<string>();
                if (name == "Shape" || name == "FilterInfo")
                {
                    field["type"] = "U32";
                }
            }
            return RszRepositorySerializer.Default.FromJson(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()));
        }
    }
}
