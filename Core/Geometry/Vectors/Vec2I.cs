// THIS FILE WAS AUTO-GENERATED.
// CHANGES WILL NOT BE PROPAGATED.
// ----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using Helion.Geometry.Boxes;
using Helion.Util.Extensions;

namespace Helion.Geometry.Vectors
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Vec2I(int x, int y)
    {
        public static readonly Vec2I Zero = new(0, 0);
        public static readonly Vec2I One = new(1, 1);

        public int X = x;
        public int Y = y;

        public readonly Vec2F Float => new((float)X, (float)Y);
        public readonly Vec2D Double => new((double)X, (double)Y);
        public readonly Vec2Fixed FixedPoint => new(Fixed.From(X), Fixed.From(Y));
        public readonly Box2I Box => new((0, 0), (X, Y));

        public static implicit operator Vec2I(ValueTuple<int, int> tuple)
        {
            return new(tuple.Item1, tuple.Item2);
        }

        
        public static Vec2I operator -(Vec2I self) => new(-self.X, -self.Y);
        public static Vec2I operator +(Vec2I self, Vec2I other) => new(self.X + other.X, self.Y + other.Y);
        public static Vec2I operator +(Vec2I self, Vector2I other) => new(self.X + other.X, self.Y + other.Y);
        public static Vec2I operator -(Vec2I self, Vec2I other) => new(self.X - other.X, self.Y - other.Y);
        public static Vec2I operator -(Vec2I self, Vector2I other) => new(self.X - other.X, self.Y - other.Y);
        public static Vec2I operator *(Vec2I self, Vec2I other) => new(self.X * other.X, self.Y * other.Y);
        public static Vec2I operator *(Vec2I self, Vector2I other) => new(self.X * other.X, self.Y * other.Y);
        public static Vec2I operator *(Vec2I self, int value) => new(self.X * value, self.Y * value);
        public static Vec2I operator *(int value, Vec2I self) => new(self.X * value, self.Y * value);
        public static Vec2I operator /(Vec2I self, Vec2I other) => new(self.X / other.X, self.Y / other.Y);
        public static Vec2I operator /(Vec2I self, Vector2I other) => new(self.X / other.X, self.Y / other.Y);
        public static Vec2I operator /(Vec2I self, int value) => new(self.X / value, self.Y / value);
        public static bool operator ==(Vec2I self, Vec2I other) => self.X == other.X && self.Y == other.Y;
        public static bool operator !=(Vec2I self, Vec2I other) => !(self == other);

        public readonly Vec2I WithX(int x) => new(x, Y);
        public readonly Vec2I WithY(int y) => new(X, y);
        public readonly Vec3I To3D(int z) => new(X, Y, z);

        public readonly Vec2I Abs() => new(X.Abs(), Y.Abs());
        public readonly int Dot(Vec2I other) => (X * other.X) + (Y * other.Y);
        public readonly int Dot(Vector2I other) => (X * other.X) + (Y * other.Y);

        public override readonly string ToString() => $"{X}, {Y}";
        public override readonly bool Equals(object? obj) => obj is Vec2I v && X == v.X && Y == v.Y;
        public override readonly int GetHashCode() => HashCode.Combine(X, Y);
    }
}
