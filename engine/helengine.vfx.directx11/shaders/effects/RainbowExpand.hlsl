#include "../common/VfxCommon.hlsli"

// Params0 // x: HueCyclesPerClip, y: StartScale, z: EndScale, w: Easing kind (0=Linear,1=EaseIn,2=EaseOut,3=EaseInOut)
// Params1 // xyz: BackgroundColor, w: unused
// Params2 // unused
// Params3 // unused

float3 HueRotate(float3 color, float hueDegrees)
{
    float angle = radians(hueDegrees);
    float cosA = cos(angle);
    float sinA = sin(angle);

    float3x3 rotation = float3x3(
        0.299 + (0.701 * cosA) + (0.168 * sinA), 0.587 - (0.587 * cosA) + (0.330 * sinA), 0.114 - (0.114 * cosA) - (0.497 * sinA),
        0.299 - (0.299 * cosA) - (0.328 * sinA), 0.587 + (0.413 * cosA) + (0.035 * sinA), 0.114 - (0.114 * cosA) + (0.292 * sinA),
        0.299 - (0.300 * cosA) + (1.250 * sinA), 0.587 - (0.588 * cosA) - (1.050 * sinA), 0.114 + (0.886 * cosA) - (0.203 * sinA));

    return mul(rotation, color);
}

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
