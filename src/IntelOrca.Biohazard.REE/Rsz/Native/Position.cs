using System.Numerics;
using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Position : IEquatable<Position>
    {
        public double x;
        public double y;
        public double z;

        public double this[int index]
        {
            readonly get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                _ => throw ValueTypeUtils.IndexError(index, 3)
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw ValueTypeUtils.IndexError(index, 3);
                }
            }
        }

        public Vector3 AsVector3 => new Vector3((float)x, (float)y, (float)z);

        public bool Equals(Position other) => other == this;
        public static bool operator ==(Position p1, Position p2) => p1.x == p2.x && p1.y == p2.y && p1.z == p2.z;
        public static bool operator !=(Position p1, Position p2) => p1.x == p2.x && p1.y == p2.y && p1.z == p2.z;

        public readonly override string ToString() => $"Position({x}, {y}, {z})";

        public override bool Equals(object? obj) => obj is Position position && position == this;
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }
}
