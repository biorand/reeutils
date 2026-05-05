using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KeyFrame
    {
        public float value;
        public uint time_type;
        public uint inNormal;
        public uint outNormal;
    }
}
