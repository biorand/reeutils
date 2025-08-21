namespace IntelOrca.Biohazard.REE
{
    public static class FileVersion
    {
        public static int FromPath(string path)
        {
            var fullstopIndex = path.LastIndexOf('.');
            return int.TryParse(path.Substring(fullstopIndex + 1), out var version) ? version : -1;
        }
    }
}
