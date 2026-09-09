using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REEUtils.Commands;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    /// <summary>
    /// Full-corpus Onimusha: Way of the Sword roundtrip tests. Every corpus file is exported
    /// to JSON, re-imported, and re-exported; the two JSON documents must be identical. This
    /// is the durable successor of the ad-hoc oniws-bulk harness: one command verifies that
    /// all .scn.21 / .pfb.18 / .user.3 / .msg.23 files can be viewed, edited, and written.
    /// Skips when the retail game pak is not installed.
    /// </summary>
    public class TestOniwsCorpus : IDisposable
    {
        private const string Game = "oniws";

        private static readonly string[] ScnCorpus =
        [
            "natives/STM/Art/Light/Event/evc0411/evc0411_50_00.scn.21",
            "natives/STM/GameDesign/Environment/Stage/Stage213/Area/Area213_000/AIMap/AIMap.scn.21",
            "natives/STM/GameDesign/Event/Cutscene/evc/evc0202/evc0202.scn.21",
            "natives/STM/GameDesign/Event/Cutscene/evc/evc1905/evc1905_50/light/evc1905_50_17.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage100/Area/Area100_100/Grid/DrawGrid/x11z27/SubScene/Sub_00.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage100/Area/Area100_204/Grid/DrawGrid/x11z24/GroupTerm/Area100_204_Group001_Term001_SM/SubScene/Sub_01.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage100/Area/Area100_700/UndividedScene/Foliage/Foliage.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage100/Grid/EcolGrid/x18z24/LowModel_x18z24.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage201/Area/Area201_002/Grid/DrawGrid/x27z19/HighModel_x27z19.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage204/Grid/DrawGrid/x0z11/LowModel_x0z11.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage212/Grid/Global/Global.scn.21",
            "natives/STM/GameDesign/System/Environment/Stage/Stage214/Area/Area214_009/Grid/DrawGrid/x17z26/HighModel_x17z26.scn.21",
        ];

        private static readonly string[] PfbCorpus =
        [
            "natives/STM/Art/VFX/EffectProvider/Common/epvr_common.pfb.18",
            "natives/STM/Art/VFX/EffectProvider/Stage/Stage100/Area100_100/Area100_104/epvr_Area100_104_rain_002.pfb.18",
            "natives/STM/GameDesign/Action/Enemy/Em507/Common/Shell/shl_e507_010/shl_e507_010.pfb.18",
            "natives/STM/GameDesign/Action/NPC/_Prefab/Npc306_03.pfb.18",
            "natives/STM/GameDesign/Environment/_Prefab/Navigation/AIMapEffector/Gm022_001_00.pfb.18",
            "natives/STM/GameDesign/Environment/RandomSet/Prefab/RandomSet_Area100_000_Set023.pfb.18",
            "natives/STM/GameDesign/Gimmick/_Prefab/Gm033_008_00.pfb.18",
            "natives/STM/GameDesign/Movie/Prefab/skilltree_movie_024_00.pfb.18",
            "natives/STM/GameDesign/Story/SideMissionCommon/Ms230600/Ob01/Ms230600_Ob01.pfb.18",
            "natives/STM/GUI/_Prefab/texture/Illust_05_01_D0.pfb.18",
        ];

        private static readonly string[] UserCorpus =
        [
            "natives/STM/Ace/Data/BTable/DefaultBTableOrderList.user.3",
            "natives/STM/Art/Model/Character/Montage/ColorVariation/em108_00/em108_00_MmiPresetData_08.user.3",
            "natives/STM/Art/Model/Character/Montage/PartsModelData/npc301_00/ch200_01_18_montageParts.user.3",
            "natives/STM/GameDesign/Action/Enemy/Em102/00/Btable/Em102_00_APPEAR_MAIN.user.3",
            "natives/STM/GameDesign/Action/Enemy/Em304/00/Data/Param/Em304_00_NaviData.user.3",
            "natives/STM/GameDesign/Action/Enemy/Em514/00/Data/Param/Em514_00_Setting.user.3",
            "natives/STM/GameDesign/DataExcel/Dialogue/Dia_Act_202_0003.user.3",
            "natives/STM/GameDesign/Event/Cutscene/evc/evc0005/Fsm/evc0005_RailCamera.user.3",
            "natives/STM/GameDesign/Gimmick/Gm037/Gm037_017_01/MiniComponent/Gm037_017_01_McParamFade.user.3",
            "natives/STM/GameDesign/Gimmick/Gm800/Gm800_204/MiniComponent/Gm800_204_MiniComponentHolder.user.3",
            "natives/STM/Motion/Enemy/Em503/00/Em503_00_Archive_mcb.user.3",
            "natives/STM/Sound/UserData/02_Container/Enemy/Em1XX/em105_00_SoundAttachSliced_ContainerListData.user.3",
            "natives/STM/Sound/UserData/11_TriggerInfoList/Gimmick/Gm800/gm800_017_00_TriggerInfoListData.user.3",
            "natives/STM/Sound/UserData/32_GimmickTrigger/Gm009/gm009_001_00_GimmickGeneratorData.user.3",
        ];

        private static readonly string[] MsgCorpus =
        [
            "natives/STM/Ace/Data/GUI/ACE_SAVE_MSG.msg.23",
            "natives/STM/GameDesign/Text/Export/Cutscene/evc1004Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Dialogue/Dia_Act_203_0002Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Dialogue/Dia_MS_000030_5055Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Dialogue/Dia_MS_000120_0010Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Dialogue/Dia_MS_100202_0040Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Dialogue/Dia_MS_215001_0010Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Dialogue/Dia_NPC003_00_0100Text.msg.23",
            "natives/STM/GameDesign/Text/Export/Enemy/Em501_00_BtlMsgInfoText.msg.23",
            "natives/STM/GameDesign/Text/Export/Mission/Mono_MS032000Text.msg.23",
            "natives/STM/GameDesign/Text/Manual/GUI/Dialog.msg.23",
            "natives/STM/GameDesign/Text/Manual/Mission/Ms000120.msg.23",
        ];

        private static readonly string[] PogCorpus =
        [
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/EmSet_Area100_000_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RandomEmSet_Area100_000_Set001.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RandomEmSet_Area100_000_Set014.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/GmSet_Area100_000_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/ItSet_Area100_000_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_202/Layout/EmSet_Area100_202_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_204/Layout/RandomEmSet_Area100_204_Set004.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage201/Area/Area201_002/Layout/EmSet_Area201_002_MS100200.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage201/Area/Area201_002/Layout/SetWave/Area201_002_MS100201_3.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage213/Area/Area213_000/Layout/SpnSet_Area213_000_00.pog.12",
            "natives/STM/GameDesign/Environment/Stage/Stage213/Area/Area213_004/Layout/EmSet_Area213_004_00.pog.12",
            "natives/STM/Sound/UserData/41_Env/Pos/Stage201/Stage201_Ochiba_EnvFoliage_PointGraph.pog.12",
        ];

        private static readonly string[] PoglstCorpus =
        [
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/ContextLayoutList_Area100_000.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RandomSet_Area100_000_Set001.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_001/Layout/RandomSet_Area100_001_Set005.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_102/Layout/RandomSet_Area100_102_Set000.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_204/Layout/SetWave/SpnSetList014.poglst.0",
            "natives/STM/GameDesign/Environment/Stage/Stage209/Area/Area209_011/Layout/SetWave/OgSetList010.poglst.0",
            "natives/STM/GameDesign/Event/Cutscene/evc/evc1103/Fsm/evc1103_PointGraphList_End.poglst.0",
            "natives/STM/GameDesign/Event/Cutscene/evc/evc1608/Fsm/evc1608_PointGraphList_Start.poglst.0",
        ];

        private static readonly string[] CsetCorpus =
        [
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_000/Layout/RestrictZone_Area100_000.cset.6",
            "natives/STM/GameDesign/Environment/Stage/Stage209/Area/Area209_003/Layout/UniqueActingZone_Area209_003.cset.6",
            "natives/STM/GameDesign/Environment/Stage/Stage100/Area/Area100_103/Layout/RespawnZone_Area100_103.cset.6",
            "natives/STM/GameDesign/Environment/Stage/Stage214/Area/Area214_009/Layout/GeographyZone_Area214_009.cset.6",
            "natives/STM/GameDesign/Story/MainMission/Ms000010/Ob25/_Zone/Ms000010_Ob25_Col.cset.6",
            "natives/STM/GameDesign/System/Environment/Stage/Stage209/Area/Area209_007/Layout/Gimmick/ColliderSet/GimmickZone_Area209_007_INVALID_Gimmick.cset.6",
        ];

        private static readonly string[] Fsmv2Corpus =
        [
            "natives/STM/GameDesign/Story/MainMission/Ms000100/Ob11/_Fsm/Ms000100_Ob11_Fsm01.fsmv2.42",
            "natives/STM/GameDesign/Story/MainMission/Ms032010/Ob00/_Fsm/Ms032010_Ob00_RestoreFsm.fsmv2.42",
            "natives/STM/GameDesign/Story/SideMissionStory/Ms100202/Ob06/_Fsm/Ms100202_Ob06_RestoreFsm.fsmv2.42",
            "natives/STM/GameDesign/Story/SideMissionStory/Ms100401/Ob05/_Fsm/Ms100401_Ob05_Fsm00.fsmv2.42",
            "natives/STM/GameDesign/Story/SideMissionStory/Ms105003/Ob00/_Fsm/Ms105003_Ob00_RestoreFsm.fsmv2.42",
            "natives/STM/GameDesign/Story/SideMissionStory/Ms105005/Ob01/_Fsm/Ms105005_Ob01_Fsm01.fsmv2.42",
        ];

        private readonly RePakCollection _pak;

        public TestOniwsCorpus()
        {
            _pak = GetVanillaPak();
        }

        public void Dispose()
        {
            _pak.Dispose();
        }

        [Fact]
        public async Task Scn_AllCorpus_RoundTrips()
        {
            await CheckCorpus(".scn.21", ScnCorpus);
        }

        [Fact]
        public async Task Pfb_AllCorpus_RoundTrips()
        {
            await CheckCorpus(".pfb.18", PfbCorpus);
        }

        [Fact]
        public async Task User_AllCorpus_RoundTrips()
        {
            await CheckCorpus(".user.3", UserCorpus);
        }

        [Fact]
        public async Task Msg_AllCorpus_RoundTrips()
        {
            await CheckCorpus(".msg.23", MsgCorpus);
        }

        [Fact]
        public async Task Pog_AllCorpus_RoundTrips()
        {
            await CheckByteIdentical(".pog.12", PogCorpus);
        }

        [Fact]
        public async Task Poglst_AllCorpus_RoundTrips()
        {
            await CheckByteIdentical(".poglst.0", PoglstCorpus);
        }

        [Fact]
        public async Task Cset_AllCorpus_RoundTrips()
        {
            await CheckByteIdentical(".cset.6", CsetCorpus);
        }

        [Fact]
        public async Task Fsmv2_AllCorpus_RoundTrips()
        {
            await CheckByteIdentical(".fsmv2.42", Fsmv2Corpus);
        }

        /// <summary>
        /// export -> import must reproduce the original bytes exactly (stronger than the JSON
        /// idempotency check used for the older formats). Used for the point-graph / collider-set /
        /// fsmv2 formats where the container is fully re-emitted.
        /// </summary>
        private async Task CheckByteIdentical(string extension, IReadOnlyList<string> corpus)
        {
            var failures = new List<string>();
            foreach (var path in corpus)
            {
                try
                {
                    var (_, _, importedBytes, originalBytes) = await RoundTripAsync(path, extension);
                    if (!importedBytes.AsSpan().SequenceEqual(originalBytes))
                    {
                        failures.Add($"{path}: import changed {importedBytes.Length} bytes -> {originalBytes.Length}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{path}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.True(failures.Count == 0,
                $"{failures.Count}/{corpus.Count} {extension} files failed byte-identical roundtrip:\n{string.Join("\n", failures)}");
        }

        private async Task CheckCorpus(string extension, IReadOnlyList<string> corpus)
        {
            var failures = new List<string>();
            var jsonMismatches = new List<string>();
            foreach (var path in corpus)
            {
                try
                {
                    var (jsonA, jsonB, importedBytes, originalBytes) = await RoundTripAsync(path, extension);
                    if (jsonA != jsonB)
                    {
                        jsonMismatches.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{path}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.True(jsonMismatches.Count == 0,
                $"{jsonMismatches.Count}/{corpus.Count} {extension} files produced non-idempotent JSON:\n{string.Join("\n", jsonMismatches)}");
            Assert.True(failures.Count == 0,
                $"{failures.Count}/{corpus.Count} {extension} files failed roundtrip:\n{string.Join("\n", failures)}");
        }

        private async Task<(string, string, byte[], byte[])> RoundTripAsync(string path, string extension)
        {
            using var tempFolder = new TempFolder();
            var entryData = _pak.GetEntryData(path) ?? throw new Exception($"'{path}' not found in vanilla pak.");
            var filePath = tempFolder.GetSubPath($"test{extension}");
            var jsonPath = tempFolder.GetSubPath("test.json");
            File.WriteAllBytes(filePath, entryData);

            var exportCommand = new ExportCommand();
            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = filePath,
                Game = Game,
                OutputPath = jsonPath
            });

            var jsonA = File.ReadAllText(jsonPath);

            var importCommand = new ImportCommand();
            await importCommand.ExecuteAsync(null!, new ImportCommand.Settings()
            {
                InputPath = jsonPath,
                Game = Game,
                OutputPath = filePath
            });

            var importedBytes = File.ReadAllBytes(filePath);

            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = filePath,
                Game = Game,
                OutputPath = jsonPath
            });
            var jsonB = File.ReadAllText(jsonPath);

            return (jsonA, jsonB, importedBytes, entryData);
        }

        private RePakCollection GetVanillaPak()
        {
            var dir = Environment.GetEnvironmentVariable("STEAM_DIR");
            var basePath = string.IsNullOrEmpty(dir)
                ? null
                : Path.Combine(dir, "OnimushaWotS");
            if (basePath == null || !Directory.Exists(basePath))
            {
                Assert.Skip("Skipping because a vanilla Onimusha: Way of the Sword install was not found (set STEAM_DIR=<steamapps/common>).");
            }
            return new RePakCollection(basePath!);
        }
    }
}