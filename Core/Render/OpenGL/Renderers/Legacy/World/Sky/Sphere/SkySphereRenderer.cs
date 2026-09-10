using System;
using Helion.Geometry;
using Helion.Geometry.Vectors;
using Helion.Render.OpenGL.Buffer.Array.Vertex;
using Helion.Render.OpenGL.Renderers.Legacy.World.Shader;
using Helion.Render.OpenGL.Shared;
using Helion.Render.OpenGL.Texture.Legacy;
using Helion.Resources;
using Helion.Resources.Archives.Collection;
using Helion.Resources.Definitions;
using Helion.Util.Configs.Components;
using OpenTK.Graphics.OpenGL;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Sky.Sphere;

public class SkySphereRenderer : IDisposable
{
    // Clip-space quad; the stencil buffer restricts it to visible sky surfaces.
    private static readonly SkySphereVertex[] ScreenPoints =
    [
        new(-1, -1, 0, -1, -1), new(1, -1, 0, 1, -1), new(1, 1, 0, 1, 1),
        new(-1, -1, 0, -1, -1), new(1, 1, 0, 1, 1), new(-1, 1, 0, -1, 1),
    ];

    private readonly VertexPipeline<SkySphereVertex> m_pipeline;
    private readonly SkySphereShader m_skyProgram;
    private readonly SkySphereForegroundShader m_foregroundProgram;
    private readonly SkySphereTexture m_texture;

    private SkyRenderMode m_mode;

    public SkySphereRenderer(ArchiveCollection archiveCollection, LegacyGLTextureManager textureManager, int textureHandle)
    {
        m_skyProgram = new();
        m_foregroundProgram = new();
        m_pipeline = new([m_skyProgram, m_foregroundProgram], new StaticVertexBuffer<SkySphereVertex>("Sky sphere", ScreenPoints.Length), "Sky sphere");
        m_texture = new(archiveCollection, textureManager, textureHandle);
        m_texture.LoadTextures();

        UploadScreenVertices();
    }

    ~SkySphereRenderer()
    {
        ReleaseUnmanagedResources();
    }

    public void Render(RenderInfo renderInfo, SkyOptions options, Vec2F offset)
    {
        m_mode = renderInfo.Config.SkyMode.Value;
        GL.ActiveTexture(BindTextures.BoundTexture);

        var skyTexture = m_texture.GetSkyTexture(out var skyTransform);
        skyTransform.Sky.Offset.X += offset.X;
        skyTransform.Sky.Offset.Y += offset.Y;

        m_skyProgram.Bind();
        SetSkyUniforms(m_skyProgram, renderInfo, options, m_mode, skyTexture, skyTexture, skyTransform.Sky);
        DrawSky(skyTexture.GlTexture);
        m_skyProgram.Unbind();

        if (skyTransform.Foreground == null)
            return;

        m_foregroundProgram.Bind();
        GL.ActiveTexture(BindTextures.BoundTexture);

        var foregroundTexture = m_texture.GetForegroundTexture(skyTransform.Foreground);
        SetSkyUniforms(m_foregroundProgram, renderInfo, SkyOptions.Flip, m_mode, foregroundTexture, skyTexture, skyTransform.Foreground);
        DrawSky(foregroundTexture.GlTexture);

        m_foregroundProgram.Unbind();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private void DrawSky(GLLegacyTexture texture)
    {
        texture.Bind();
        m_pipeline.Bind();
        m_pipeline.DrawArrays();
        m_pipeline.Unbind();
        texture.Unbind();
    }

    private void UploadScreenVertices()
    {
        m_pipeline.Vbo.Data.Data = ScreenPoints;
        m_pipeline.Vbo.Data.Length = ScreenPoints.Length;
        m_pipeline.Vbo.SetNotUploaded();
        m_pipeline.Vbo.UploadIfNeeded();
    }

    private static void SetSkyUniforms(SkySphereShader skyProgram, RenderInfo renderInfo, SkyOptions options, SkyRenderMode skyRenderMode,
        in SkyTexture skyTexture, in SkyTexture blendSkyTexture, SkyTransformTexture skyTransform)
    {
        bool invulnerability = false;
        if (renderInfo.ViewerEntity.PlayerObj != null)
            invulnerability = renderInfo.ViewerEntity.PlayerObj.DrawInvulnerableColorMap();

        var dimension = GetSkyTextureDimension(skyTexture);
        var scaleUV = SkySphereTexture.CalcScale(dimension, skyTransform);
        var offset = SkySphereTexture.CalcOffset(dimension, skyTransform, skyTransform.CurrentScroll, skyRenderMode, scaleUV, options);
        var prevOffset = SkySphereTexture.CalcOffset(dimension, skyTransform, skyTransform.PrevScroll, skyRenderMode, scaleUV, options);
        var skyHeight = SkySphereTexture.CalcSkyHeight(dimension.Height, skyRenderMode);

        skyProgram.BoundTexture(BindTextures.BoundTexture);
        skyProgram.ColormapTexture(BindTextures.Colormap);
        var fov = Renderer.GetFieldOfViewInfo(renderInfo);
        float tanHalfFovY = MathF.Tan(fov.FovY / 2);
        // At the default FOV, map the original 200 screen rows to 200 sky texels.
        // CalcScale uses 512 texels per unit of vertical sky coordinates.
        float verticalScale = (200f / 1024f) * tanHalfFovY / MathF.Tan(63.2f * MathF.PI / 360);
        skyProgram.ProjectionScale(new Vec2F(tanHalfFovY * fov.Width / fov.Height, verticalScale));
        skyProgram.CameraAngles(new Vec2F(renderInfo.Camera.YawRadians, renderInfo.Camera.PitchRadians));
        skyProgram.Scale(scaleUV);
        skyProgram.FlipU((options & SkyOptions.Flip) != 0);
        skyProgram.ColorMix(renderInfo.Uniforms.ColorMix.Sky);
        skyProgram.GammaCorrection(renderInfo.Uniforms.GammaCorrection);

        if (ShaderVars.PaletteColorMode)
        {
            skyProgram.TopColor(new Vec4F(blendSkyTexture.TopColorIndex / 255f, 0, 0, 0));
            skyProgram.BottomColor(new Vec4F(blendSkyTexture.BottomColorIndex / 255f, 0, 0, 0));
        }
        else
        {
            skyProgram.TopColor(blendSkyTexture.TopColor);
            skyProgram.BottomColor(blendSkyTexture.BottomColor);
        }

        skyProgram.HasInvulnerability(invulnerability);
        skyProgram.PaletteIndex((int)renderInfo.Uniforms.PaletteIndex);
        skyProgram.ColorMapIndex(renderInfo.Uniforms.ColorMapUniforms.SkyIndex);
        skyProgram.ScrollOffset(offset);
        skyProgram.PrevScrollOffset(prevOffset);
        skyProgram.SkyHeight(skyHeight);
        skyProgram.SkyMin(0.5f - skyHeight);
        skyProgram.SkyMax(0.5f + skyHeight);
        skyProgram.TimeFrac(renderInfo.TickFraction);
    }

    private static Dimension GetSkyTextureDimension(SkyTexture skyTexture)
    {
        // The sky fire is a special case where it's rendered at the standard dimenion but it's actual dimension differs.
        return skyTexture.IsFire ? SkySphereTexture.StandardDimension : skyTexture.GlTexture.Dimension;
    }

    private void ReleaseUnmanagedResources()
    {
        m_skyProgram.Dispose();
        m_foregroundProgram.Dispose();
        m_pipeline.Dispose();
        m_texture.Dispose();
    }
}
