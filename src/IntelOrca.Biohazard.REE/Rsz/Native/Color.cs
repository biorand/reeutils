using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace via
{
    public struct Color : IEquatable<Color>
    {
        public uint rgba;

        public Color(uint rgba)
        {
            this.rgba = rgba;
        }

        public Color(byte r, byte g, byte b, byte a)
        {
            rgba = r + ((uint)g << 8) + ((uint)b << 16) + ((uint)a << 24);
        }

        [JsonIgnore]
        public int R { readonly get => (int)(rgba >> 0) & 0xff; set => rgba = (rgba & 0xffffff00) | ((uint)value & 0xff); }
        [JsonIgnore]
        public int G { readonly get => (int)(rgba >> 8) & 0xff; set => rgba = (rgba & 0xffff00ff) | (((uint)value & 0xff) << 8); }
        [JsonIgnore]
        public int B { readonly get => (int)(rgba >> 16) & 0xff; set => rgba = (rgba & 0xff00ffff) | (((uint)value & 0xff) << 16); }
        [JsonIgnore]
        public int A { readonly get => (int)(rgba >> 24) & 0xff; set => rgba = (rgba & 0x00ffffff) | (((uint)value & 0xff) << 24); }

        [JsonIgnore]
        public readonly int ARGB => A + (R << 8) + (G << 16) + (B << 24);
        [JsonIgnore]
        public readonly int ABGR => A + (B << 8) + (G << 16) + (R << 24);
        [JsonIgnore]
        public readonly int BGRA => B + (G << 8) + (R << 16) + (A << 24);

        public readonly string Hex()
        {
            return rgba.ToString("X8");
        }

        public readonly override string ToString()
        {
            return Hex();
        }

        public static Color Parse(string hex)
        {
            return new Color { rgba = uint.Parse(hex, NumberStyles.HexNumber) };
        }

        public static bool TryParse(string hex, out Color color)
        {
            if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
            {
                color = new Color { rgba = parsed };
                return true;
            }

            color = default;
            return false;
        }

        public readonly Vector4 ToVector4() => new Vector4(R / 255f, G / 255f, B / 255f, A / 255f);
        public readonly Color Inverse() => new Color((byte)(255 - R), (byte)(255 - G), (byte)(255 - B), (byte)A);

        public static Color FromVector4(Vector4 vec) => new Color((byte)Math.Round(vec.X * 255), (byte)Math.Round(vec.Y * 255), (byte)Math.Round(vec.Z * 255), (byte)Math.Round(vec.W * 255));

        public bool Equals(Color other) => other.rgba == rgba;
        public override bool Equals(object? obj) => obj is Color col && col.rgba == rgba;
        public static bool operator ==(Color left, Color right) => left.rgba == right.rgba;
        public static bool operator !=(Color left, Color right) => left.rgba != right.rgba;
        public override int GetHashCode() => rgba.GetHashCode();
    }
}
