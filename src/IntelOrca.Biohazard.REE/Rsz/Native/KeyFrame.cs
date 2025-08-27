using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz.Native
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
