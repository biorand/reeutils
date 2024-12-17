using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using IntelOrca.Biohazard.REE.Package;
using RszTool;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class EmbeddedData
    {
        public static PakList? GetPakList(string name)
        {
            var data = GetCompressedFile($"paklist.{name}.txt");
            if (data == null)
                return null;

            var content = Encoding.UTF8.GetString(data);
            return new PakList(content);
        }

        public static RszFileOption? CreateRszFileOptionBinary(string name)
        {
            if (!Enum.TryParse<GameName>(name, out var gameName))
                return null;

            var data = GetCompressedFile($"resz{name}.dat")!;
            var enumData = Encoding.UTF8.GetString(GetCompressedFile("re4_enum.json")!);
            var rszParser = RszBinarySerializer.Deserialize(data);
            Patch(rszParser);

            return new RszFileOption(gameName, GameVersion.re4, rszParser, EnumParser.FromJson(enumData));
        }

        private static void Patch(RszParser parser)
        {
            // TODO move these to patch JSON
            var colliders = parser.GetRSZClass("via.physics.Colliders")!;
            colliders.GetField("v2")!.type = RszFieldType.S32;
            var collider = parser.GetRSZClass("via.physics.Collider")!;
            collider.GetField("v2")!.type = RszFieldType.Object;
            collider.GetField("v2")!.original_type = "via.physics.BoxShape";
            collider.GetField("v3")!.type = RszFieldType.Object;
            collider.GetField("v3")!.original_type = "via.physics.FilterInfo";
            collider.GetField("v4")!.type = RszFieldType.Object;
            collider.GetField("v4")!.original_type = "chainsaw.collision.GimmickSensorUserData";
            var boxShape = parser.GetRSZClass("via.physics.BoxShape")!;
            boxShape.GetField("v0")!.type = RszFieldType.S32;
            var filterInfo = parser.GetRSZClass("via.physics.FilterInfo")!;
            filterInfo.GetField("v0")!.type = RszFieldType.S32;
            filterInfo.GetField("v1")!.type = RszFieldType.S32;
            filterInfo.GetField("v2")!.type = RszFieldType.S32;
            filterInfo.GetField("v3")!.type = RszFieldType.S32;
            filterInfo.GetField("v4")!.type = RszFieldType.S32;
            var gimmickSensorUserData = parser.GetRSZClass("chainsaw.collision.GimmickSensorUserData")!;
            gimmickSensorUserData.GetField("v1")!.original_type = "via.physics.UserData";
            var sphereShape = parser.GetRSZClass("via.physics.SphereShape")!;
            sphereShape.GetField("v0")!.type = RszFieldType.S32;
            var mesh = parser.GetRSZClass("via.render.Mesh")!;
            mesh.GetField("v0")!.type = RszFieldType.S32;
            mesh.GetField("v5")!.type = RszFieldType.F32;
            mesh.GetField("v8")!.type = RszFieldType.F32;
            mesh.GetField("v9")!.type = RszFieldType.S32;
            mesh.GetField("v10")!.type = RszFieldType.S32;
            mesh.GetField("v34")!.type = RszFieldType.S32;
            mesh.GetField("v36")!.type = RszFieldType.S32;
            mesh.GetField("v37")!.type = RszFieldType.S32;
            mesh.GetField("v38")!.type = RszFieldType.S32;
            mesh.GetField("v40")!.type = RszFieldType.Object;
            mesh.GetField("v43")!.type = RszFieldType.S32;
            mesh.GetField("v45")!.type = RszFieldType.S32;
            mesh.GetField("v47")!.type = RszFieldType.S32;
            mesh.GetField("v50")!.type = RszFieldType.S32;
            mesh.GetField("v51")!.type = RszFieldType.S32;
            mesh.GetField("v55")!.type = RszFieldType.S32;
            mesh.GetField("v56")!.type = RszFieldType.S32;
            mesh.GetField("v59")!.type = RszFieldType.S32;
            mesh.GetField("v65")!.type = RszFieldType.S32;
            var aiMap = parser.GetRSZClass("via.navigation.AIMap")!;
            aiMap.GetField("v0")!.type = RszFieldType.Object;
            aiMap.GetField("v2")!.type = RszFieldType.S32;
            var mapHandle = parser.GetRSZClass("via.navigation.MapHandle")!;
            mapHandle.GetField("v9")!.type = RszFieldType.S32;
            var dynamicMotionBank = parser.GetRSZClass("via.motion.DynamicMotionBank")!;
            dynamicMotionBank.GetField("v0")!.type = RszFieldType.S32;
            dynamicMotionBank.GetField("v2")!.type = RszFieldType.S32;
            dynamicMotionBank.GetField("v4")!.type = RszFieldType.S32;
            dynamicMotionBank.GetField("v6")!.type = RszFieldType.S32;
            var motion = parser.GetRSZClass("via.motion.Motion")!;
            motion.GetField("v1")!.type = RszFieldType.S32;
            motion.GetField("v2")!.type = RszFieldType.F32;
            motion.GetField("v6")!.type = RszFieldType.S32;
            motion.GetField("v8")!.type = RszFieldType.S32;
            motion.GetField("v15")!.type = RszFieldType.S32;
            motion.GetField("v21")!.type = RszFieldType.S32;
            motion.GetField("v22")!.type = RszFieldType.S32;
            motion.GetField("v25")!.type = RszFieldType.S32;
            motion.GetField("v26")!.type = RszFieldType.S32;
            motion.GetField("v27")!.type = RszFieldType.S32;
            motion.GetField("v28")!.type = RszFieldType.S32;
            var treeLayer = parser.GetRSZClass("via.motion.TreeLayer")!;
            treeLayer.GetField("v1")!.type = RszFieldType.S32;
            treeLayer.GetField("v2")!.type = RszFieldType.S32;
            treeLayer.GetField("v3")!.type = RszFieldType.F32;
            treeLayer.GetField("v6")!.type = RszFieldType.S32;
            treeLayer.GetField("v7")!.type = RszFieldType.S32;
            treeLayer.GetField("v8")!.type = RszFieldType.S32;
            treeLayer.GetField("v10")!.type = RszFieldType.S32;
            treeLayer.GetField("v11")!.type = RszFieldType.S32;
            treeLayer.GetField("v15")!.type = RszFieldType.S32;
            treeLayer.GetField("v16")!.type = RszFieldType.F32;
            treeLayer.GetField("v21")!.type = RszFieldType.S32;
            treeLayer.GetField("v22")!.type = RszFieldType.S32;
            treeLayer.GetField("v23")!.type = RszFieldType.S32;
            treeLayer.GetField("v24")!.type = RszFieldType.S32;
            var motionFsm2 = parser.GetRSZClass("via.motion.MotionFsm2")!;
            motionFsm2.GetField("v3")!.type = RszFieldType.S32;
            motionFsm2.GetField("v4")!.type = RszFieldType.S32;
            motionFsm2.GetField("v8")!.type = RszFieldType.S32;
            var motionFsm2Layer = parser.GetRSZClass("via.motion.MotionFsm2Layer")!;
            motionFsm2Layer.GetField("v3")!.type = RszFieldType.S32;
            motionFsm2Layer.GetField("v6")!.type = RszFieldType.S32;
            motionFsm2Layer.GetField("v8")!.type = RszFieldType.S32;
            motionFsm2Layer.GetField("v9")!.type = RszFieldType.S32;
            motionFsm2Layer.GetField("v11")!.type = RszFieldType.S32;
            motionFsm2Layer.GetField("v13")!.type = RszFieldType.S32;
            motionFsm2Layer.GetField("v15")!.type = RszFieldType.F32;

            var materialParam = parser.GetRSZClass("via.render.MaterialParam")!;
            materialParam.GetField("v4")!.type = RszFieldType.S32;
        }

        public static RszFileOption? CreateRszFileOption(string name)
        {
            if (!Enum.TryParse<GameName>(name, out var gameName))
                return null;

            var dataFile = Path.Combine($"rsz{gameName}.json");
            var enumFile = Path.Combine($"Enums/{gameName}_enum.json");
            var pathFile = Path.Combine($"RszPatch/rsz{gameName}_patch.json");

            var tempPath = Path.Combine(Path.GetTempPath(), "re4rr");
            Directory.CreateDirectory(tempPath);
            ExportFile("rszre4.json", Path.Combine(tempPath, dataFile));
            ExportFile("re4_enum.json", Path.Combine(tempPath, "Data", enumFile));
            ExportFile("rszre4_patch.json", Path.Combine(tempPath, "Data", pathFile));
            var cwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempPath);
                return new RszFileOption(gameName);
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
            }
        }

        private static void ExportFile(string name, string path)
        {
            var data = GetCompressedFile(name);
            if (data == null)
                throw new Exception("Resource not found.");

            File.WriteAllBytes(path, data);
        }

        public static byte[]? GetCompressedFile(string name)
        {
            var assembly = Assembly.GetExecutingAssembly()!;
            var resourceName = $"IntelOrca.Biohazard.REEUtils.data.{name}.gz";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gzipStream.CopyTo(ms);
            return ms.ToArray();
        }

        public static byte[]? GetFile(string name)
        {
            var assembly = Assembly.GetExecutingAssembly()!;
            var resourceName = $"IntelOrca.Biohazard.REEUtils.data.{name}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
