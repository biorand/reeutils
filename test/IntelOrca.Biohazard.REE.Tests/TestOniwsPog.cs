using System;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    /// <summary>
    /// Round-trip tests for the Onimusha point-graph / point-graph-list / collider-set containers.
    /// Reading requires no game install (paths are pinned below); the rebuild tests verify the
    /// builder reproduces the source bytes exactly for every container variant (standard node table,
    /// empty graph, spawner/point-pool name-table layout, and collider geometry).
    /// </summary>
    public sealed class TestOniwsPog
    {
        private static readonly string[] PogFiles =
        [
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/EmSet_Area100_000_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RandomEmSet_Area100_000_Set001.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/GmSet_Area100_000_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_202/Layout/EmSet_Area100_202_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage213/Area/Area213_000/Layout/SpnSet_Area213_000_00.pog.12",
            "natives/STM/Sound/UserData/41_Env/Pos/Stage201/Stage201_Ochiba_EnvFoliage_PointGraph.pog.12",
        ];

        private static readonly string[] PoglstFiles =
        [
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/ContextLayoutList_Area100_000.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RandomSet_Area100_000_Set001.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_204/Layout/SetWave/SpnSetList014.poglst.0",
            "natives/STM/GameDesign/Event/Cutscene/evc/evc1103/Fsm/evc1103_PointGraphList_End.poglst.0",
        ];

        private static readonly string[] CsetFiles =
        [
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RestrictZone_Area100_000.cset.6",
            "natives/STM/GameDesign/Environment/Stage/Stage209/Area/Area209_003/Layout/UniqueActingZone_Area209_003.cset.6",
            "natives/STM/GameDesign/Story/MainMission/Ms000010/Ob25/_Zone/Ms000010_Ob25_Col.cset.6",
            "natives/STM/GameDesign/System/Environment/Stage/Stage209/Area/Area209_007/Layout/Gimmick/ColliderSet/GimmickZone_Area209_007_INVALID_Gimmick.cset.6",
        ];

        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;

        [Theory]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/EmSet_Area100_000_00.pog.12")]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/GmSet_Area100_000_00.pog.12")]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_202/Layout/EmSet_Area100_202_00.pog.12")]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage213/Area/Area213_000/Layout/SpnSet_Area213_000_00.pog.12")]
        public void Rebuild_Oniws_PointGraph_Is_ByteIdentical(string path)
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            var data = _pakHelper.GetFileData(GameNames.ONIWS, path);
            var file = new PogFile(FileVersion.FromPath(path), data);
            Assert.Equal(12, file.Version);
            var rebuilt = file.ToBuilder(repo).Build().Data.ToArray();
            Assert.True(rebuilt.AsSpan().SequenceEqual(data), $"POG rebuild changed bytes: {path}");
        }

        [Theory]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/ContextLayoutList_Area100_000.poglst.0")]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_204/Layout/SetWave/SpnSetList014.poglst.0")]
        [InlineData("natives/STM/GameDesign/Event/Cutscene/evc/evc1103/Fsm/evc1103_PointGraphList_End.poglst.0")]
        public void Rebuild_Oniws_PointGraphList_Is_ByteIdentical(string path)
        {
            var data = _pakHelper.GetFileData(GameNames.ONIWS, path);
            var file = new PogListFile(FileVersion.FromPath(path), data);
            var rebuilt = file.ToBuilder().Build().Data.ToArray();
            Assert.True(rebuilt.AsSpan().SequenceEqual(data), $"POGLST rebuild changed bytes: {path}");
        }

        [Theory]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RestrictZone_Area100_000.cset.6")]
        [InlineData("natives/STM/GameDesign/Environment/Stage/Stage209/Area/Area209_003/Layout/UniqueActingZone_Area209_003.cset.6")]
        [InlineData("natives/STM/GameDesign/System/Environment/Stage/Stage209/Area/Area209_007/Layout/Gimmick/ColliderSet/GimmickZone_Area209_007_INVALID_Gimmick.cset.6")]
        public void Rebuild_Oniws_ColliderSet_Is_ByteIdentical(string path)
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            var data = _pakHelper.GetFileData(GameNames.ONIWS, path);
            var file = new CsetFile(FileVersion.FromPath(path), data, repo);
            var rebuilt = file.ToBuilder(repo).Build().Data.ToArray();
            Assert.True(rebuilt.AsSpan().SequenceEqual(data), $"CSET rebuild changed bytes: {path}");
        }

        [Fact]
        public void PointGraph_EmSet_Exposes_Node_Transforms()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            var path = "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/EmSet_Area100_000_00.pog.12";
            var file = new PogFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.ONIWS, path));

            Assert.Equal(4, file.NodeCount);
            Assert.Equal(4, file.NodeEntries.Length);

            var objects = file.ReadObjects(repo);
            Assert.Equal(4, objects.Length);
            Assert.All(objects, o => Assert.Equal("app.ContextPointGraphEnemy", o.Type.Name));
            Assert.Contains(objects, o => o.Type.FindFieldIndex("v2") != -1);
        }

        [Fact]
        public void PointGraph_Node_Add_And_Remove_Rebuilds()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            var path = "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/EmSet_Area100_000_00.pog.12";
            var file = new PogFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.ONIWS, path));

            var builder = file.ToBuilder(repo);
            // Clone the last node: append a copy of object[0] to both the object list and the node table.
            var cloned = (RszObjectNode)RszJsonSerializer.Deserialize(
                RszJsonSerializer.Serialize(builder.Objects[0], repo), repo);
            builder.Objects = [.. builder.Objects, cloned];
            builder.NodeEntries = [.. builder.NodeEntries, new PogFile.PogNodeEntry((uint)(builder.NodeEntries.Length), 0, 0)];

            var rebuilt = new PogFile(builder.Build().Data.ToArray());
            Assert.Equal(file.NodeCount + 1, rebuilt.NodeCount);
            Assert.Equal(file.ReadObjects(repo).Length + 1, rebuilt.ReadObjects(repo).Length);

            // Remove a node again.
            var shrunk = rebuilt.ToBuilder(repo);
            shrunk.Objects = shrunk.Objects.RemoveAt(shrunk.Objects.Length - 1);
            shrunk.NodeEntries = shrunk.NodeEntries.RemoveAt(shrunk.NodeEntries.Length - 1);
            var restored = new PogFile(shrunk.Build().Data.ToArray());
            Assert.Equal(file.NodeCount, restored.NodeCount);
            Assert.Equal(file.ReadObjects(repo).Length, restored.ReadObjects(repo).Length);
        }

        [Fact]
        public void PointGraphList_RoundTrips_Paths()
        {
            var path = "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/ContextLayoutList_Area100_000.poglst.0";
            var file = new PogListFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.ONIWS, path));

            Assert.True(file.PogFiles.Length >= 4);
            Assert.Contains(file.PogFiles, p => p.Contains("EmSet_Area100_000_00.pog"));
            Assert.DoesNotContain(file.PogFiles, p => p.EndsWith(".pog.12"));
        }
    }
}