namespace IntelOrca.Biohazard.REE.Tests
{
    internal sealed class TempFolder : IDisposable
    {
        private bool _disposed;

        public string Path { get; }

        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public string GetSubPath(string relative)
        {
            return System.IO.Path.Combine(Path, relative);
        }

        ~TempFolder()
        {
            if (!_disposed)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
