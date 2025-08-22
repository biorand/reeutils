using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Range
    {
        public readonly float r;
        public readonly float s;
    }
}
