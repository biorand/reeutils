using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestPakPathFinder
    {
        [Fact]
        public void Find()
        {
            var path = @"C:\Users\Ted\Downloads\re_chunk_000.pak.patch_005.pak";
            var pak = new PakFile(path);
            var finder = new PakPathFinder(GetTypeRepository(), pak);

            var pakList = PakList.FromFile(@"M:\git\reeutils\src\reeutils\data\paklist.re4.txt.gz");
            var foundPaths = finder.Find(pakList);
        }

        private static RszTypeRepository GetTypeRepository()
        {
            var jsonPath = @"G:\apps\reasy\rszre4.json";
            var json = File.ReadAllBytes(jsonPath);
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }
    }
}
