using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REEUtils.GDeflate;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal readonly record struct GDeflatePage(nint Data, int Size);

internal static partial class NativeMethods
{
    private const string LibName = "GDeflate";
    private static readonly nint LoadedHandle;

    static NativeMethods()
    {
        LoadedHandle = ResolveLibraryHandle(LibName, Assembly.GetExecutingAssembly(), DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory);
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
    }

    internal static void EnsureLoaded()
    {
        if (LoadedHandle == nint.Zero)
            throw new DllNotFoundException($"Unable to locate the vendored {LibName} runtime library.");
    }

    private static nint DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == LibName && LoadedHandle != nint.Zero)
            return LoadedHandle;

        return ResolveLibraryHandle(libraryName, assembly, searchPath);
    }

    private static nint ResolveLibraryHandle(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
            return handle;

        if (searchPath != null && !searchPath.Value.HasFlag(DllImportSearchPath.AssemblyDirectory))
            return nint.Zero;

        var name = Path.GetFileNameWithoutExtension(libraryName);
        var roots = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetDirectoryName(assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory,
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        string ext;
        if (OperatingSystem.IsWindows())
        {
            ext = ".dll";
        }
        else if (OperatingSystem.IsLinux())
        {
            ext = ".so";
        }
        else if (OperatingSystem.IsMacOS())
        {
            ext = ".dylib";
        }
        else
        {
            return nint.Zero;
        }

        foreach (var root in roots)
        {
            foreach (var libName in new[] { name, "lib" + name, name + "-0", $"lib{name}-0" })
            {
                foreach (var target in EnumerateCandidatePaths(root, libName + ext))
                {
                    var ptr = NativeLibrary.Load(target);
                    if (ptr != nint.Zero)
                        return ptr;
                }
            }
        }

        return nint.Zero;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string root, string fileName)
    {
        var directPath = Path.Combine(root, fileName);
        if (File.Exists(directPath))
            yield return directPath;

        foreach (var runtimesDir in new[] { Path.Combine(root, "runtimes"), Path.Combine(root, "GDeflate", "runtimes") })
        {
            if (!Directory.Exists(runtimesDir))
                continue;

            foreach (var candidate in Directory.EnumerateFiles(runtimesDir, fileName, SearchOption.AllDirectories))
                yield return candidate;
        }
    }

    [LibraryImport(LibName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory)]
    internal static partial nint libdeflate_alloc_gdeflate_compressor(int level);

    [LibraryImport(LibName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory)]
    internal static unsafe partial nint libdeflate_gdeflate_compress(nint compressor, nint src, nint srcSize, GDeflatePage* pages, nint numPages);

    [LibraryImport(LibName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory)]
    internal static partial void libdeflate_free_gdeflate_compressor(nint compressor);

    [LibraryImport(LibName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory)]
    internal static partial nint libdeflate_alloc_gdeflate_decompressor();

    [LibraryImport(LibName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory)]
    internal static unsafe partial int libdeflate_gdeflate_decompress(nint compressor, GDeflatePage* pages, nint numPages, nint dst, nint dstSize, out nint bytes);

    [LibraryImport(LibName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory)]
    internal static partial void libdeflate_free_gdeflate_decompressor(nint compressor);
}
