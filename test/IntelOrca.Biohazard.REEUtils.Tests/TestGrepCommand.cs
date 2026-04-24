using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REEUtils.Commands;
using Xunit;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public class TestGrepCommand
    {
        [Fact]
        public async Task Grep_Finds_Raw_Text_Entry()
        {
            using var temp = new TempFolder();
            var pakPath = temp.GetSubPath("test.pak");
            var pakListPath = temp.GetSubPath("paklist.txt");

            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/leveldesign/chapter/chap3_01/test.txt", Encoding.UTF8.GetBytes("This contains LevelFlow string."));
            builder.Save(pakPath);

            File.WriteAllText(pakListPath, "natives/stm/leveldesign/chapter/chap3_01/test.txt");

            var settings = new GrepCommand.Settings
            {
                Pak = pakPath,
                PakListPath = pakListPath,
                Pattern = "LevelFlow",
                Paths = new string[] { "natives/stm/leveldesign/chapter/chap3_01" }
            };

            var sw = new StringWriter();
            var oldOut = Console.Out;
            try
            {
                Console.SetOut(sw);
                var cmd = new GrepCommand();
                var result = await cmd.ExecuteAsync(null!, settings);
            }
            finally
            {
                Console.SetOut(oldOut);
            }

            var output = sw.ToString();
            Assert.Contains("LevelFlow", output);
            Assert.Contains("natives/stm/leveldesign/chapter/chap3_01/test.txt", output);
        }
    }
}
