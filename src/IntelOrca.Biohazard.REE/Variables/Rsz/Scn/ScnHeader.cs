using System;
using System.Buffers.Binary;

internal class ScnHeaderBase
{
    public byte[] Signature = new byte[4];
    public uint InfoCount;
    public uint ResourceCount;
    public uint FolderCount;
    public uint UserdataCount;
    public uint PrefabCount;
    public ulong FolderTbl;
    public ulong ResourceInfoTbl;
    public ulong PrefabInfoTbl;
    public ulong DataOffset;

    public int Size { get; }
    public virtual void Parse(byte[] data)
    {
        if (data == null || data.Length < Size)
            throw new ArgumentException($"Invalid SCN file data: expected at least {Size} bytes, got {data?.Length ?? 0}");

        Array.Copy(data, 0, Signature, 0, 4);
        InfoCount = BitConverter.ToUInt32(data, 4);
        ResourceCount = BitConverter.ToUInt32(data, 8);
        FolderCount = BitConverter.ToUInt32(data, 12);
        UserdataCount = BitConverter.ToUInt32(data, 16);
        PrefabCount = BitConverter.ToUInt32(data, 20);
        FolderTbl = BitConverter.ToUInt64(data, 24);
        ResourceInfoTbl = BitConverter.ToUInt64(data, 32);
        PrefabInfoTbl = BitConverter.ToUInt64(data, 40);
        // DataOffset handled in derived
    }
}

internal class Scn18Header : ScnHeaderBase
{
    public int Size => 56;

    public override void Parse(byte[] data)
    {
        base.Parse(data);
        DataOffset = BitConverter.ToUInt64(data, 48);
    }
}

internal class Scn19Header : ScnHeaderBase
{
    public ulong UserdataInfoTbl;
    public int Size => 64;

    public override void Parse(byte[] data)
    {
        base.Parse(data);
        UserdataInfoTbl = BitConverter.ToUInt64(data, 48);
        DataOffset = BitConverter.ToUInt64(data, 56);
    }
}
