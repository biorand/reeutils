using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal static class ValueTypeUtils
    {
        public static void CheckIndex(int index, int length)
        {
            if (index < 0 || index >= length) throw new IndexOutOfRangeException($"Index must be 0..{length - 1}, got {index}");
        }

        public static IndexOutOfRangeException IndexError(int index, int length)
        {
            return new IndexOutOfRangeException($"Index must be 0..{length - 1}, got {index}");
        }
    }
}
