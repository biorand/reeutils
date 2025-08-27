using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Package
{
    public interface IPakFile : IDisposable
    {
        ImmutableArray<ulong> FileHashes { get; }

        byte[]? GetEntryData(ulong hash);
        byte[]? GetEntryData(string path);
    }
}
