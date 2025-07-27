using System;
using System.Collections;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.REE.Variables.Rsz.Data
{
    public class ArrayData<T>
    {
        public List<T> Values { get; }
        public Type ElementType { get; }
        public string OriginalType { get; }

        public ArrayData(Type? elementType = null, string originalType = "")
        {
            Values = new List<T>();
            ElementType = elementType ?? typeof(T);
            OriginalType = originalType;
        }

        public int AddElement(T element)
        {
            if (ElementType != null && !ElementType.IsInstanceOfType(element))
                throw new ArgumentException($"Expected {ElementType?.Name ?? "unknown"}, got {element?.GetType()?.Name ?? "unknown"}");
            Values.Add(element);
            return Values.Count - 1;
        }
    }

    public class StructData
    {
        public List<Dictionary<string, object>> Values { get; }
        public string OriginalType { get; }

        public StructData(string originalType = "")
        {
            Values = new List<Dictionary<string, object>>();
            OriginalType = originalType;
        }

        public int AddElement(Dictionary<string, object> element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            Values.Add(element);
            return Values.Count - 1;
        }
    }

    public class ObjectData
    {
        public int Value { get; set; }
        public string OriginalType { get; set; }

        public ObjectData(int value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class ResourceData
    {
        public string Value { get; set; }
        public string OriginalType { get; set; }

        public ResourceData(string value = "", string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class UserDataData
    {
        public int Value { get; set; }
        public string String { get; set; }
        public string OriginalType { get; set; }

        public UserDataData(int value = 0, string str = "", string originalType = "")
        {
            Value = value;
            String = str;
            OriginalType = originalType;
        }
    }

    public class BoolData
    {
        public bool Value { get; set; }
        public string OriginalType { get; set; }

        public BoolData(bool value = false, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class S8Data
    {
        public sbyte Value { get; set; }
        public string OriginalType { get; set; }

        public S8Data(sbyte value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class U8Data
    {
        public byte Value { get; set; }
        public string OriginalType { get; set; }

        public U8Data(byte value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class S16Data
    {
        public short Value { get; set; }
        public string OriginalType { get; set; }

        public S16Data(short value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class U16Data
    {
        public ushort Value { get; set; }
        public string OriginalType { get; set; }

        public U16Data(ushort value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class S32Data
    {
        public int Value { get; set; }
        public string OriginalType { get; set; }

        public S32Data(int value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class U32Data
    {
        public uint Value { get; set; }
        public string OriginalType { get; set; }

        public U32Data(uint value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class S64Data
    {
        public long Value { get; set; }
        public string OriginalType { get; set; }

        public S64Data(long value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class U64Data
    {
        public ulong Value { get; set; }
        public string OriginalType { get; set; }

        public U64Data(ulong value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class F32Data
    {
        public float Value { get; set; }
        public string OriginalType { get; set; }

        public F32Data(float value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class F64Data
    {
        public double Value { get; set; }
        public string OriginalType { get; set; }

        public F64Data(double value = 0, string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class StringData
    {
        public string Value { get; set; }
        public string OriginalType { get; set; }

        public StringData(string value = "", string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class Uint2Data
    {
        public uint X { get; set; }
        public uint Y { get; set; }
        public string OriginalType { get; set; }

        public Uint2Data(uint x = 0, uint y = 0, string originalType = "")
        {
            X = x;
            Y = y;
            OriginalType = originalType;
        }
    }

    public class Uint3Data
    {
        public uint X { get; set; }
        public uint Y { get; set; }
        public uint Z { get; set; }
        public string OriginalType { get; set; }

        public Uint3Data(uint x = 0, uint y = 0, uint z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class Int2Data
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string OriginalType { get; set; }

        public Int2Data(int x = 0, int y = 0, string originalType = "")
        {
            X = x;
            Y = y;
            OriginalType = originalType;
        }
    }

    public class Int3Data
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string OriginalType { get; set; }

        public Int3Data(int x = 0, int y = 0, int z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class Int4Data
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int W { get; set; }
        public string OriginalType { get; set; }

        public Int4Data(int x = 0, int y = 0, int z = 0, int w = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            OriginalType = originalType;
        }
    }

    public class Float2Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public string OriginalType { get; set; }

        public Float2Data(float x = 0, float y = 0, string originalType = "")
        {
            X = x;
            Y = y;
            OriginalType = originalType;
        }
    }

    public class Float3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string OriginalType { get; set; }

        public Float3Data(float x = 0, float y = 0, float z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class Float4Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
        public string OriginalType { get; set; }

        public Float4Data(float x = 0, float y = 0, float z = 0, float w = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            OriginalType = originalType;
        }
    }

    public class Mat4Data : IEnumerable<float>
    {
        public List<float> Values { get; }
        public string OriginalType { get; set; }

        public Mat4Data(IEnumerable<float>? values = null, string originalType = "")
        {
            Values = new List<float>(16);
            if (values != null)
            {
                foreach (var v in values)
                    Values.Add(v);
            }
            while (Values.Count < 16)
                Values.Add(0.0f);
            OriginalType = originalType;
        }

        public IEnumerator<float> GetEnumerator() => Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => Values.Count;
        public float this[int idx] => Values[idx];

        public override string ToString() => $"MAT4({string.Join(", ", Values)})";
    }

    public class Vec2Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public string OriginalType { get; set; }

        public Vec2Data(float x = 0, float y = 0, string originalType = "")
        {
            X = x;
            Y = y;
            OriginalType = originalType;
        }
    }

    public class Vec3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string OriginalType { get; set; }

        public Vec3Data(float x = 0, float y = 0, float z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class Vec3ColorData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string OriginalType { get; set; }

        public Vec3ColorData(float x = 0, float y = 0, float z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class Vec4Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
        public string OriginalType { get; set; }

        public Vec4Data(float x = 0, float y = 0, float z = 0, float w = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            OriginalType = originalType;
        }
    }

    public class QuaternionData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
        public string OriginalType { get; set; }

        public QuaternionData(float x = 0, float y = 0, float z = 0, float w = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
            OriginalType = originalType;
        }
    }

    public class GuidData
    {
        public string GuidString { get; set; }
        public byte[] RawBytes { get; set; }
        public string OriginalType { get; set; }

        public GuidData(string? guidString = null, byte[]? rawBytes = null, string originalType = "")
        {
            GuidString = guidString ?? "00000000-0000-0000-0000-000000000000";
            RawBytes = rawBytes ?? new byte[16];
            OriginalType = originalType;
        }
    }

    public class ColorData
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public int A { get; set; }
        public string OriginalType { get; set; }

        public ColorData(int r = 0, int g = 0, int b = 0, int a = 0, string originalType = "")
        {
            R = r;
            G = g;
            B = b;
            A = a;
            OriginalType = originalType;
        }
    }

    public class AABBData
    {
        public Vec3Data Min { get; set; }
        public Vec3Data Max { get; set; }
        public string OriginalType { get; set; }

        public AABBData(float minX = 0, float minY = 0, float minZ = 0, float maxX = 0, float maxY = 0, float maxZ = 0, string originalType = "")
        {
            Min = new Vec3Data(minX, minY, minZ, originalType);
            Max = new Vec3Data(maxX, maxY, maxZ, originalType);
            OriginalType = originalType;
        }
    }

    public class CapsuleData
    {
        public Vec3Data Start { get; set; }
        public Vec3Data End { get; set; }
        public float Radius { get; set; }
        public string OriginalType { get; set; }

        public CapsuleData(Vec3Data? start = null, Vec3Data? end = null, float radius = 0, string originalType = "")
        {
            Start = start ?? new Vec3Data(0, 0, 0, originalType);
            End = end ?? new Vec3Data(0, 0, 0, originalType);
            Radius = radius;
            OriginalType = originalType;
        }
    }

    public class AreaData
    {
        public Float2Data P0 { get; set; }
        public Float2Data P1 { get; set; }
        public Float2Data P2 { get; set; }
        public Float2Data P3 { get; set; }
        public float Height { get; set; }
        public float Bottom { get; set; }
        public string OriginalType { get; set; }

        public AreaData(Float2Data? p0 = null, Float2Data? p1 = null, Float2Data? p2 = null, Float2Data? p3 = null, float height = 0, float bottom = 0, string originalType = "")
        {
            P0 = p0 ?? new Float2Data(0, 0, originalType);
            P1 = p1 ?? new Float2Data(0, 0, originalType);
            P2 = p2 ?? new Float2Data(0, 0, originalType);
            P3 = p3 ?? new Float2Data(0, 0, originalType);
            Height = height;
            Bottom = bottom;
            OriginalType = originalType;
        }
    }

    public class ConeData
    {
        public Vec3Data Position { get; set; }
        public Vec3Data Direction { get; set; }
        public float Angle { get; set; }
        public float Distance { get; set; }
        public string OriginalType { get; set; }

        public ConeData(Vec3Data? position = null, Vec3Data? direction = null, float angle = 0, float distance = 0, string originalType = "")
        {
            Position = position ?? new Vec3Data(0, 0, 0, originalType);
            Direction = direction ?? new Vec3Data(0, 0, 0, originalType);
            Angle = angle;
            Distance = distance;
            OriginalType = originalType;
        }
    }

    public class LineSegmentData
    {
        public Vec3Data Start { get; set; }
        public Vec3Data End { get; set; }
        public string OriginalType { get; set; }

        public LineSegmentData(Vec3Data? start = null, Vec3Data? end = null, string originalType = "")
        {
            Start = start ?? new Vec3Data(0, 0, 0, originalType);
            End = end ?? new Vec3Data(0, 0, 0, originalType);
            OriginalType = originalType;
        }
    }

    public class OBBData : IEnumerable<float>
    {
        public List<float> Values { get; }
        public string OriginalType { get; set; }

        public OBBData(IEnumerable<float>? values = null, string originalType = "")
        {
            Values = new List<float>(20);
            if (values != null)
            {
                foreach (var v in values)
                    Values.Add(v);
            }
            while (Values.Count < 20)
                Values.Add(0.0f);
            OriginalType = originalType;
        }

        public IEnumerator<float> GetEnumerator() => Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => Values.Count;
        public float this[int idx] => Values[idx];

        public override string ToString() => $"OBB({string.Join(", ", Values)})";
    }

    public class PointData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string OriginalType { get; set; }

        public PointData(float x = 0, float y = 0, float z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class PositionData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string OriginalType { get; set; }

        public PositionData(float x = 0, float y = 0, float z = 0, string originalType = "")
        {
            X = x;
            Y = y;
            Z = z;
            OriginalType = originalType;
        }
    }

    public class RangeData
    {
        public float Min { get; set; }
        public float Max { get; set; }
        public string OriginalType { get; set; }

        public RangeData(float min = 0, float max = 0, string originalType = "")
        {
            Min = min;
            Max = max;
            OriginalType = originalType;
        }
    }

    public class RangeIData
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public string OriginalType { get; set; }

        public RangeIData(int min = 0, int max = 0, string originalType = "")
        {
            Min = min;
            Max = max;
            OriginalType = originalType;
        }
    }

    public class SizeData
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public string OriginalType { get; set; }

        public SizeData(float width = 0, float height = 0, string originalType = "")
        {
            Width = width;
            Height = height;
            OriginalType = originalType;
        }
    }

    public class SphereData
    {
        public Vec3Data Center { get; set; }
        public float Radius { get; set; }
        public string OriginalType { get; set; }

        public SphereData(Vec3Data? center = null, float radius = 0, string originalType = "")
        {
            Center = center ?? new Vec3Data(0, 0, 0, originalType);
            Radius = radius;
            OriginalType = originalType;
        }
    }

    public class CylinderData
    {
        public Vec3Data Center { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public string OriginalType { get; set; }

        public CylinderData(Vec3Data? center = null, float radius = 0, float height = 0, string originalType = "")
        {
            Center = center ?? new Vec3Data(0, 0, 0, originalType);
            Radius = radius;
            Height = height;
            OriginalType = originalType;
        }
    }

    public class RectData
    {
        public float MinX { get; set; }
        public float MinY { get; set; }
        public float MaxX { get; set; }
        public float MaxY { get; set; }
        public string OriginalType { get; set; }

        public RectData(float minX = 0, float minY = 0, float maxX = 0, float maxY = 0, string originalType = "")
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            OriginalType = originalType;
        }
    }

    public class GameObjectRefData
    {
        public string GuidString { get; set; }
        public byte[] RawBytes { get; set; }
        public string OriginalType { get; set; }

        public GameObjectRefData(string? guidString = null, byte[]? rawBytes = null, string originalType = "")
        {
            GuidString = guidString ?? "00000000-0000-0000-0000-000000000000";
            RawBytes = rawBytes ?? new byte[16];
            OriginalType = originalType;
        }
    }

    public class RuntimeTypeData
    {
        public string Value { get; set; }
        public string OriginalType { get; set; }

        public RuntimeTypeData(string value = "", string originalType = "")
        {
            Value = value;
            OriginalType = originalType;
        }
    }

    public class MaybeObject
    {
        public string OriginalType { get; set; }

        public MaybeObject(string originalType = "")
        {
            OriginalType = originalType;
        }
    }

    public class RawBytesData
    {
        public byte[] RawBytes { get; set; }
        public int FieldSize { get; set; }
        public string OriginalType { get; set; }

        public RawBytesData(byte[]? rawBytes = null, int fieldSize = 1, string originalType = "")
        {
            RawBytes = rawBytes ?? Array.Empty<byte>();
            FieldSize = fieldSize;
            OriginalType = originalType;
        }
    }

    public static class DataTypeMapping
    {
        private static readonly Dictionary<string, Type> TypeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "aabb", typeof(AABBData) },
            { "area", typeof(AreaData) },
            { "bool", typeof(BoolData) },
            { "capsule", typeof(CapsuleData) },
            { "color", typeof(ColorData) },
            { "cone", typeof(ConeData) },
            { "cylinder", typeof(CylinderData) },
            { "data", typeof(RawBytesData) },
            { "enum", typeof(S32Data) },
            { "f32", typeof(F32Data) },
            { "f64", typeof(F64Data) },
            { "float", typeof(F32Data) },
            { "float2", typeof(Float2Data) },
            { "float3", typeof(Float3Data) },
            { "float4", typeof(Float4Data) },
            { "gameobjectref", typeof(GameObjectRefData) },
            { "guid", typeof(GuidData) },
            { "int2", typeof(Int2Data) },
            { "int3", typeof(Int3Data) },
            { "int4", typeof(Int4Data) },
            { "int", typeof(S32Data) },
            { "keyframe", typeof(Vec4Data) },
            { "linesegment", typeof(LineSegmentData) },
            { "mat4", typeof(Mat4Data) },
            { "object", typeof(ObjectData) },
            { "obb", typeof(OBBData) },
            { "point", typeof(PointData) },
            { "position", typeof(PositionData) },
            { "quaternion", typeof(QuaternionData) },
            { "range", typeof(RangeData) },
            { "rangei", typeof(RangeIData) },
            { "rect", typeof(RectData) },
            { "resource", typeof(ResourceData) },
            { "runtimetype", typeof(RuntimeTypeData) },
            { "s16", typeof(S16Data) },
            { "s32", typeof(S32Data) },
            { "s64", typeof(S64Data) },
            { "s8", typeof(S8Data) },
            { "size", typeof(SizeData) },
            { "sphere", typeof(SphereData) },
            { "string", typeof(StringData) },
            { "struct", typeof(StructData) },
            { "u16", typeof(U16Data) },
            { "u32", typeof(U32Data) },
            { "u64", typeof(U64Data) },
            { "u8", typeof(U8Data) },
            { "uint", typeof(U32Data) },
            { "uint2", typeof(Uint2Data) },
            { "uint3", typeof(Uint3Data) },
            { "userdata", typeof(UserDataData) },
            { "vec2", typeof(Vec2Data) },
            { "vec3", typeof(Vec3Data) },
            { "vec4", typeof(Vec4Data) },
        };

        public static Type GetTypeClass(
            string fieldType,
            int fieldSize = 4,
            bool isNative = false,
            bool isArray = false,
            int align = 4,
            string originalType = "",
            string fieldName = "")
        {
            if (fieldType == "data")
            {
                if (fieldSize == 16)
                {
                    if (align == 8 && isNative)
                        return typeof(GuidData);
                    else
                        return typeof(Vec4Data);
                }
                else if (fieldSize == 80)
                    return typeof(OBBData);
                else if (fieldSize == 64 && align == 16)
                    return typeof(Mat4Data);
                else if (fieldSize == 4 && isNative)
                    return typeof(MaybeObject);
                else if (fieldSize == 1)
                    return typeof(U8Data);
            }

            if (fieldType == "obb" && fieldSize == 16)
                return typeof(Vec4Data);

            if (fieldType == "uri" && originalType.Contains("GameObjectRef"))
                return typeof(GameObjectRefData);

            if (fieldType == "point" && originalType.Contains("Range"))
                return typeof(RangeData);

            if (fieldType == "vec3" && fieldName.ToLowerInvariant().Contains("color"))
                return typeof(Vec3ColorData);

            if (isArray && isNative && fieldSize == 4 && (fieldType == "s32" || fieldType == "u32"))
                return typeof(MaybeObject);

            return TypeMap.TryGetValue(fieldType, out var t) ? t : typeof(RawBytesData);
        }
    }
}
