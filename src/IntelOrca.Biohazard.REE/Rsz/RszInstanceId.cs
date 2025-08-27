using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RszInstanceId(int index)
    {
        public int Index => index;

        public override string ToString() => $"${index}";
    }
}
