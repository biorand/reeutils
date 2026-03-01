using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Range
    {
        public readonly float r;
        public readonly float s;
    }
}
