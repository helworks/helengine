#include "../common/VfxCommon.hlsli"

// Gaussian blur layer effect. Bound by DirectX11EffectExecutor in the pass Reads order of
// BuiltInVfxEffects.GaussianBlur: pass 1 reads Source (t0); pass 2 reads BlurX (t0) and Source (t1).
Texture2D FirstTexture : register(t0);
Texture2D SourceTexture : register(t1);

// Params0 // x: Radius in output pixels, yzw: unused

static const int MaxBlurTaps = 48;

// Converts straight alpha to premultiplied alpha.
float4 Premultiply(float4 color)
{
    return float4(color.rgb * color.a, color.a);
}

// Converts premultiplied alpha back to straight alpha.
float4 Unpremultiply(float4 color)
{
    return color.a > 0.0 ? float4(color.rgb / color.a, color.a) : float4(0.0, 0.0, 0.0, 0.0);
}

// Gaussian blur of premultiplied color along a direction. The step is a UV offset derived from the main input's
// texel size, so the radius stays in output pixels whatever the resolution of the texture being read; clamp-to-edge
// comes from the shared sampler.
float4 BlurAlong(Texture2D image, float2 uv, float2 direction, float radius)
{
    float2 pixelStep = direction * MainTexelSize;
    int taps = (int)min(ceil(radius), (float)MaxBlurTaps);
    float spacing = radius / taps;
    float sigma = radius * 0.5;
    float4 total = 0.0;
    float weight = 0.0;
    [loop]
    for (int index = -taps; index <= taps; index++)
    {
        float distance = index * spacing;
        float tapWeight = exp(-0.5 * distance * distance / (sigma * sigma));
        total += tapWeight * Premultiply(image.SampleLevel(LinearClampSampler, uv + pixelStep * distance, 0));
        weight += tapWeight;
    }
    return total / weight;
}

// Pass 1: horizontal blur of the source into the half-resolution BlurX target.
float4 GaussianBlurHorizontalPS(PSInput input) : SV_TARGET
{
    float radius = clamp(Params0.x, 0.0, 96.0);
    if (radius < 0.5)
    {
        return FirstTexture.SampleLevel(LinearClampSampler, input.UV, 0);
    }
    return Unpremultiply(BlurAlong(FirstTexture, input.UV, float2(1.0, 0.0), radius));
}

// Pass 2: vertical blur of BlurX to the output; a zero radius returns the untouched source.
float4 GaussianBlurVerticalPS(PSInput input) : SV_TARGET
{
    float radius = clamp(Params0.x, 0.0, 96.0);
    if (radius < 0.5)
    {
        return SourceTexture.SampleLevel(LinearClampSampler, input.UV, 0);
    }
    return Unpremultiply(BlurAlong(FirstTexture, input.UV, float2(0.0, 1.0), radius));
}
