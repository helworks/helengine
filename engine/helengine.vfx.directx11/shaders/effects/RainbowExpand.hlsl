#include "../common/VfxCommon.hlsli"

// Bound by DirectX11VfxEffectRunner in RainbowExpandEffect.InputRoles order: Source then Mask.
Texture2D SourceTexture : register(t0);
Texture2D MaskTexture : register(t1);

// Params0 // x: HueCyclesPerClip, y: StartScale, z: EndScale, w: Easing kind (0=Linear,1=EaseIn,2=EaseOut,3=EaseInOut)
// Params1 // xyz: BackgroundColor, w: unused
// Params2 // unused
// Params3 // unused

float4 RainbowExpandPS(PSInput input) : SV_TARGET
{
    float hueCyclesPerClip = Params0.x;
    float startScale = Params0.y;
    float endScale = Params0.z;
    float easingKind = Params0.w;
    float3 backgroundColor = Params1.xyz;

    float t = ApplyEasing(NormalizedTime, easingKind);
    float scale = lerp(startScale, endScale, t);

    float2 centeredUV = (input.UV - 0.5) / max(scale, 0.0001);
    float2 sampleUV = centeredUV + 0.5;

    bool inBounds = all(sampleUV >= 0.0) && all(sampleUV <= 1.0);
    if (!inBounds)
    {
        return float4(backgroundColor, 1.0);
    }

    float alpha = MaskTexture.Sample(LinearClampSampler, sampleUV).a;
    float3 sourceColor = SourceTexture.Sample(LinearClampSampler, sampleUV).rgb;
    // The YIQ-style rotation matrix is not gamut-preserving: on linear HDR input (values above 1.0)
    // some hue angles produce negative channel values, which would otherwise be written straight
    // into the exported EXR. Clamp to a physically meaningful non-negative radiance.
    float3 huedColor = max(HueRotate(sourceColor, 360.0 * hueCyclesPerClip * t), 0.0);

    float3 finalColor = lerp(backgroundColor, huedColor, alpha);
    return float4(finalColor, 1.0);
}
