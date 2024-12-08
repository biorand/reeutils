using System.IO;
using RszTool;

namespace IntelOrca.Biohazard.REEUtils
{
    public static class RszExtensions
    {
        public static byte[] ToByteArray(this BaseRszFile scnFile)
        {
            var ms = new MemoryStream();
            var fileHandler = new FileHandler(ms);
            scnFile.WriteTo(fileHandler);
            return ms.ToArray();
        }
    }
}
