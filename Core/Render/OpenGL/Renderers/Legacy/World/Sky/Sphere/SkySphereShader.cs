using Helion.Geometry.Vectors;
using Helion.Render.OpenGL.Renderers.Legacy.World.Shader;
using Helion.Render.OpenGL.Shader;
using OpenTK.Graphics.OpenGL;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Sky.Sphere;

public class SkySphereShader : RenderProgram
{
    private readonly int m_boundTextureLocation;
    private readonly int m_colormapTextureLocation;
    private readonly int m_projectionScaleLocation;
    private readonly int m_cameraAnglesLocation;
    private readonly int m_hasInvulnerabilityLocation;
    private readonly int m_scaleLocation;
    private readonly int m_flipULocation;
    private readonly int m_paletteIndexLocation;
    private readonly int m_colorMapIndexLocation;
    private readonly int m_scrollOffsetLocation;
    private readonly int m_prevScrollOffsetLocation;
    private readonly int m_topColorLocation;
    private readonly int m_bottomColorLocation;
    private readonly int m_skyHeightLocation;
    private readonly int m_skyMin;
    private readonly int m_skyMax;
    private readonly int m_colorMixLocation;
    private readonly int m_gammaCorrectionLocation;
    private readonly int m_timeFracLocation;

    public SkySphereShader(string? name = null) : base(name ?? "Sky sphere")
    {
        m_boundTextureLocation = Uniforms.GetLocation("boundTexture");
        m_colormapTextureLocation = Uniforms.GetLocation("colormapTexture");
        m_projectionScaleLocation = Uniforms.GetLocation("projectionScale");
        m_cameraAnglesLocation = Uniforms.GetLocation("cameraAngles");
        m_hasInvulnerabilityLocation = Uniforms.GetLocation("hasInvulnerability");
        m_scaleLocation = Uniforms.GetLocation("scale");
        m_flipULocation = Uniforms.GetLocation("flipU");
        m_paletteIndexLocation = Uniforms.GetLocation("paletteIndex");
        m_colorMapIndexLocation = Uniforms.GetLocation("colormapIndex");
        m_scrollOffsetLocation = Uniforms.GetLocation("scrollOffset");
        m_prevScrollOffsetLocation = Uniforms.GetLocation("prevScrollOffset");
        m_topColorLocation = Uniforms.GetLocation("topColor");
        m_bottomColorLocation = Uniforms.GetLocation("bottomColor");
        m_skyHeightLocation = Uniforms.GetLocation("skyHeight");
        m_skyMin = Uniforms.GetLocation("skyMin");
        m_skyMax = Uniforms.GetLocation("skyMax");
        m_colorMixLocation = Uniforms.GetLocation("colorMix");
        m_gammaCorrectionLocation = Uniforms.GetLocation("gammaCorrection");
        m_timeFracLocation = Uniforms.GetLocation("timeFrac");
    }

    public void BoundTexture(TextureUnit unit) => ProgramUniforms.Set(unit, m_boundTextureLocation);
    public void ColormapTexture(TextureUnit unit) => ProgramUniforms.Set(unit, m_colormapTextureLocation);
    public void HasInvulnerability(bool invul) => ProgramUniforms.Set(invul, m_hasInvulnerabilityLocation);
    public void ProjectionScale(Vec2F value) => ProgramUniforms.Set(value, m_projectionScaleLocation);
    public void CameraAngles(Vec2F value) => ProgramUniforms.Set(value, m_cameraAnglesLocation);
    public void Scale(Vec2F v) => ProgramUniforms.Set(v, m_scaleLocation);
    public void FlipU(bool flip) => ProgramUniforms.Set(flip, m_flipULocation);
    public void PaletteIndex(int index) => ProgramUniforms.Set(index, m_paletteIndexLocation);
    public void ColorMapIndex(int index) => ProgramUniforms.Set(index, m_colorMapIndexLocation);
    public void ScrollOffset(Vec2F offset) => ProgramUniforms.Set(offset, m_scrollOffsetLocation);
    public void PrevScrollOffset(Vec2F offset) => ProgramUniforms.Set(offset, m_prevScrollOffsetLocation);
    public void TopColor(Vec4F topColor) => ProgramUniforms.Set(topColor, m_topColorLocation);
    public void BottomColor(Vec4F bottomColor) => ProgramUniforms.Set(bottomColor, m_bottomColorLocation);
    public void SkyHeight(float height) => ProgramUniforms.Set(height, m_skyHeightLocation);
    public void SkyMin(float value) => ProgramUniforms.Set(value, m_skyMin);
    public void SkyMax(float value) => ProgramUniforms.Set(value, m_skyMax);
    public void ColorMix(Vec3F value) => ProgramUniforms.Set(value, m_colorMixLocation);
    public void GammaCorrection(float value) => ProgramUniforms.Set(value, m_gammaCorrectionLocation);
    public void TimeFrac(float value) => ProgramUniforms.Set(value, m_timeFracLocation);

    protected override string VertexShader() => @"
        #version 330

        layout(location = 0) in vec3 pos;
        layout(location = 1) in vec2 uv;

        out vec2 screenPosition;

        void main() {
            screenPosition = uv;
            gl_Position = vec4(pos, 1.0);
        }
    ";

    protected static string SkyProjection => @"
        in vec2 screenPosition;
        uniform vec2 projectionScale;
        uniform vec2 cameraAngles;
        uniform int flipU;
        vec2 uvFrag;

        vec2 skyUV() {
            const float pi = 3.141592653589793;
            // Doom chooses a column by viewing angle, but uses a constant row
            // step across the screen. Never divide the vertical coordinate by
            // the distance to a cylinder (or use a spherical latitude).
            float u = (atan(screenPosition.x * projectionScale.x) - cameraAngles.x) / (2.0 * pi);
            if (flipU == 1)
                u = -u;
            // Vanilla's skytexturemid is row 100. Pitch extends this mapping
            // by translating rows, keeping horizontal lines straight.
            float v = 0.25 + 100.0 / 512.0 - screenPosition.y * projectionScale.y - cameraAngles.y / pi;
            return vec2(u, v);
        }
    ";

    public static string FetchTopBottomColors =>
        ShaderVars.PaletteColorMode ?
@"
int topTexIndex = lightLevelOffset + (useColormap * colormapSize) + (usePalette * paletteSize + int(topColor.r * 255.0));
vec4 topFetchColor = vec4(texelFetch(colormapTexture, topTexIndex).rgb, 1);
int bottomTexIndex = lightLevelOffset + (useColormap * colormapSize) + (usePalette * paletteSize + int(bottomColor.r * 255.0));
vec4 bottomFetchColor = vec4(texelFetch(colormapTexture, bottomTexIndex).rgb, 1);"
:
@"
vec4 topFetchColor = topColor;
vec4 bottomFetchColor = bottomColor;
";

    protected override string FragmentShader() => @"
        #version 330

        ${SkyProjection}

        out vec4 fragColor;

        uniform vec2 scale;
        uniform sampler2D boundTexture;
        uniform samplerBuffer colormapTexture;
        uniform int hasInvulnerability;
        uniform int paletteIndex;
        uniform int colormapIndex;
        uniform float skyHeight;
        uniform float skyMin;
        uniform float skyMax;
        uniform vec3 colorMix;
        uniform float gammaCorrection;
        uniform vec2 scrollOffset;
        uniform vec2 prevScrollOffset;
        uniform float timeFrac;

        uniform vec4 topColor;
        uniform vec4 bottomColor;

        vec4 blendSky(vec4 fragColor, vec4 topBlendColor, vec4 bottomBlendColor) {
            float blendAmount = skyHeight / 4.6;
            if (uvFrag.y < skyMax && uvFrag.y > skyMax - blendAmount)
                fragColor = vec4(mix(bottomBlendColor.rgb, fragColor.rgb, (skyMax - uvFrag.y) / blendAmount), 1);
            if (uvFrag.y > skyMin && uvFrag.y < skyMin + blendAmount)
                fragColor = vec4(mix(topBlendColor.rgb, fragColor.rgb, ((uvFrag.y - skyMin) / blendAmount)), 1);
            return fragColor;
        }

        void main() {
            uvFrag = skyUV();
            if (uvFrag.y < skyMin) {
                fragColor = topColor;
            }
            else if (uvFrag.y > skyMax) {
                fragColor = bottomColor;
            }
            else {
                vec2 textureUV = uvFrag - skyMin;
                vec2 offset = mix(prevScrollOffset, scrollOffset, timeFrac);
                fragColor = texture(boundTexture, textureUV / scale + offset);
            }

            ${ColorMapFetch}
            ${FetchTopBottomColors}
            if (uvFrag.y < skyMin) {
                fragColor = topFetchColor;
            }
            else if (uvFrag.y > skyMax) {
                fragColor = bottomFetchColor;
            }
            fragColor.a = 1;

            fragColor = blendSky(fragColor, topFetchColor, bottomFetchColor);
            fragColor.xyz *= min(colorMix, 1);
            ${InvulnerabilityFragColor}
            ${GammaCorrection}
        }
    "
    .Replace("${SkyProjection}", SkyProjection)
    .Replace("${FetchTopBottomColors}", FetchTopBottomColors)
    .Replace("${InvulnerabilityFragColor}", FragFunction.InvulnerabilityFragColor)
    .Replace("${ColorMapFetch}", FragFunction.ColorMapFetch(false, ColorMapFetchContext.Default))
    .Replace("${GammaCorrection}", FragFunction.GammaCorrection());
}
