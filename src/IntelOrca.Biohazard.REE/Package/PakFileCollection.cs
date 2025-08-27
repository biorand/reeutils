using System;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Package
{
    public sealed class PakFileCollection : IPakFile, IDisposable
    {
        private ImmutableArray<IPakFile> _files;
        private ImmutableArray<ulong> _fileHashses;
        private bool _disposed;

        public PakFileCollection(ImmutableArray<IPakFile> files)
        {
            _files = files;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            var files = _files;
            _files = [];
            foreach (var f in files)
            {
                f.Dispose();
            }
            _disposed = true;
        }

        public ImmutableArray<ulong> FileHashes
        {
            get
            {
                ThrowIfDisposed();

                if (_fileHashses.IsDefault)
                {
                    _fileHashses = _files
                        .SelectMany(x => x.FileHashes)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToImmutableArray();
                }
                return _fileHashses;
            }
        }

        public byte[]? GetEntryData(ulong hash)
        {
            ThrowIfDisposed();

            for (var i = _files.Length - 1; i >= 0; i--)
            {
                var data = _files[i].GetEntryData(hash);
                if (data != null)
                {
                    return data;
                }
            }
            return null;
        }

        public byte[]? GetEntryData(string path)
        {
            ThrowIfDisposed();

            for (var i = _files.Length - 1; i >= 0; i--)
            {
                var data = _files[i].GetEntryData(path);
                if (data != null)
                {
                    return data;
                }
            }
            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PakFileCollection));
            }
        }
    }
}
