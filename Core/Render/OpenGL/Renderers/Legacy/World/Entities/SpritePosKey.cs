using Helion.Geometry.Vectors;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Entities;

public readonly struct SpritePosKey(Vec2D pos, int sprite)
{
    public readonly Vec2D Pos = pos;
    public readonly int Sprite = sprite;

    public override int GetHashCode()
    {
        return HashCode.Combine(Pos.X, Pos.Y, Sprite);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        var key = (SpritePosKey)obj!;
        return Sprite == key.Sprite && key.Pos.X == Pos.X && key.Pos.Y == Pos.Y;
    }

    public static bool operator ==(SpritePosKey left, SpritePosKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SpritePosKey left, SpritePosKey right)
    {
        return !(left == right);
    }
}
