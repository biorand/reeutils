using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestPakPathFinder
    {
        [Fact]
        public void Find()
        {
            var path = @"G:\temp\notime4death\NT4DST-1.0-No_Time_4_Death_ST\nt4dst.pak";
            var pak = new PakFile(path);
            var finder = new PakPathFinder(GetTypeRepository(), pak);

            var pakList = PakList.FromFile(@"M:\git\reeutils\src\reeutils\data\paklist.re4.txt.gz");

            var totalHashes = pak.EntryCount;
            var unknownHashes = finder.GetUnknownHashes(pakList);
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
