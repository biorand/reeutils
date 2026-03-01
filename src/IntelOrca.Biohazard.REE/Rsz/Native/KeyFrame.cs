using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct KeyFrame
    {
        public readonly float value;
        public readonly uint time_type;
        public readonly uint inNormal;
        public readonly uint outNormal;
    }
}
