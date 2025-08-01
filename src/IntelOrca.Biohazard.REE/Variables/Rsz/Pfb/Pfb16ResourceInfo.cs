using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.REE.Variables.Rsz.Pfb
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Pfb16ResourceInfo
    {
        public string StringValue;
        public uint Reserved;
    }
}
