using Helion.Render.OpenGL.Renderers.Legacy.World.Shader;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.Sky.Sphere;

internal sealed class SkySphereForegroundShader : SkySphereShader
{
    public SkySphereForegroundShader() : base("Sky foreground texture")
    {

    }

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

        float textureHeight = skyHeight;
        float textureStart = 0.5;

        vec4 blendSky(vec4 fragColor, vec4 topBlendColor, vec4 bottomBlendColor) {
            float blendAmount = skyHeight / 4.6;
            if (uvFrag.y < skyMax && uvFrag.y > skyMax - blendAmount && bottomColor.a > 0)
                fragColor = vec4(mix(bottomBlendColor.rgb, fragColor.rgb, (skyMax - uvFrag.y) / blendAmount), 1);
            if (uvFrag.y > skyMin && uvFrag.y < skyMin + blendAmount && topBlendColor.a > 0)
                fragColor = vec4(mix(topBlendColor.rgb, fragColor.rgb, ((uvFrag.y - skyMin) / blendAmount)), 1);
            return fragColor;
        }

        void main() {
            uvFrag = skyUV();
            if (uvFrag.y < skyMin || uvFrag.y > skyMax)
                discard;
            
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

            if (fragColor.a == 0)
                discard;

            ${ColorMapFetch}
            ${FetchTopBottomColors}
            if (uvFrag.y < skyMin) {
                fragColor = topFetchColor;
            }
            else if (uvFrag.y > skyMax) {
                fragColor = bottomFetchColor;
            }
            fragColor = blendSky(fragColor, topFetchColor, bottomFetchColor);
            fragColor.xyz *= min(colorMix, 1);
            ${InvulnerabilityFragColor}
        }
    "
    .Replace("${SkyProjection}", SkyProjection)
    .Replace("${FetchTopBottomColors}", SkySphereShader.FetchTopBottomColors)
    .Replace("${InvulnerabilityFragColor}", FragFunction.InvulnerabilityFragColor)
    .Replace("${ColorMapFetch}", FragFunction.ColorMapFetch(false, ColorMapFetchContext.Default))
    .Replace("${GammaCorrection}", FragFunction.GammaCorrection());
}
