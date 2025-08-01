using System;
using System.Text;

namespace IntelOrca.Biohazard.REE.Utils
{
    internal static class Hex
    {
        public static int Align(int offset, int alignment = 16)
        {
            int r = offset % alignment;
            return r == 0 ? offset : offset + (alignment - r);
        }

        public static bool Available(byte[] data, int offset, int size)
        {
            return offset + size <= data.Length;
        }

        public static Tuple<string, int, int> ReadNullTerminatedWString(byte[] data, int offset, int maxChars = 65535)
        {
            var chars = new StringBuilder();
            int pos = offset;
            int count = 0;
            while (count < maxChars && Available(data, pos, 2))
            {
                ushort val = BitConverter.ToUInt16(data, pos);
                pos += 2;
                count++;
                if (val == 0)
                    break;
                chars.Append((char)val);
            }
            return Tuple.Create(chars.ToString(), pos, count);
        }

        public static Tuple<string, uint> ReadWString(byte[] data, uint offset, int maxWChars)
        {
            uint pos = offset;

            // Skip BOM if present
            if (pos + 1 < data.Length && data[pos] == 0xFF && data[pos + 1] == 0xFE)
                pos += 2;

            uint end = pos;
            while (end + 1 < data.Length && !(data[end] == 0 && data[end + 1] == 0))
            {
                end += 2;
                if ((end - pos) / 2 >= maxWChars)
                    break;
            }

            string str = Encoding.Unicode.GetString(data, (int)pos, (int)(end - pos));
            return Tuple.Create(str, (uint)(end + 2));
        }

        public static string GuidLeToStr(byte[] guidBytes)
        {
            if (guidBytes == null || guidBytes.Length != 16)
                return "00000000-0000-0000-0000-000000000000";

            try
            {
                // .NET Guid expects little-endian for first 3 fields, rest as-is
                int a = BitConverter.ToInt32(guidBytes, 0);
                short b = BitConverter.ToInt16(guidBytes, 4);
                short c = BitConverter.ToInt16(guidBytes, 6);
                byte[] d = new byte[8];
                Array.Copy(guidBytes, 8, d, 0, 8);
                var guid = new Guid(a, b, c, d);
                return guid.ToString();
            }
            catch
            {
                return "00000000-0000-0000-0000-000000000000";
            }
        }

        public static string SanitizeGuidStr(string guidStr)
        {
            if (string.IsNullOrEmpty(guidStr))
                return "00000000-0000-0000-0000-000000000000";

            try
            {
                var guid = new Guid(guidStr);
                return guid.ToString();
            }
            catch
            {
                var clean = new StringBuilder();
                foreach (char c in guidStr)
                {
                    if ("0123456789abcdefABCDEF-".IndexOf(c) >= 0)
                        clean.Append(c);
                }
                try
                {
                    var guid = new Guid(clean.ToString());
                    return guid.ToString();
                }
                catch
                {
                    return "00000000-0000-0000-0000-000000000000";
                }
            }
        }

        public static bool IsNullGuid(byte[] guidBytes, string? guidStr = null)
        {
            var NULL_GUID = new byte[16];
            const string NULL_GUID_STR = "00000000-0000-0000-0000-000000000000";

            if (guidBytes != null && guidBytes.Length == 16)
            {
                bool allZero = true;
                for (int i = 0; i < 16; i++)
                {
                    if (guidBytes[i] != 0)
                    {
                        allZero = false;
                        break;
                    }
                }
                if (allZero)
                    return true;
            }
            if (!string.IsNullOrEmpty(guidStr) && guidStr == NULL_GUID_STR)
                return true;
            return false;
        }
    }
}
