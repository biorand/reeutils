using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GameObjectRefInfo
    {
        public readonly int ObjectId;
        public readonly int PropertyId;
        public readonly int ArrayIndex;
        public readonly int TargetId;
    }
}
