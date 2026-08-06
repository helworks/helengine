#ifndef HELENGINE_VFX_COMMON_HLSLI
#define HELENGINE_VFX_COMMON_HLSLI

// Shared declarations for every VFX effect shader. Effects #include this file and only add their
// own pixel entry point; do not copy these declarations into an effect file, because a divergence
// between this layout and helengine.vfx.VfxFrameConstants is a silent correctness bug.
//
// Byte layout must match helengine.vfx.VfxFrameConstants.Build:
//   float  NormalizedTime  (offset 0)
//   float2 Resolution      (offset 4)
//   float  Reserved        (offset 12)
//   float4 Params0..3      (offsets 16, 32, 48, 64) = VfxFrameConstants.ParamSlotCount floats.
cbuffer VfxFrameConstants : register(b0)
{
    float NormalizedTime;
    float2 Resolution;
    float Reserved;
    float4 Params0;
    float4 Params1;
    float4 Params2;
    float4 Params3;
};

// Shared sampler for every input texture. Effects declare their own Texture2D inputs (register t0,
// t1, ...) directly in their own shader file, in the same order as their IVfxEffect.InputRoles, since
// different effects need different numbers and kinds of input textures; do not reintroduce a
// hardcoded shared set of texture declarations here.
SamplerState LinearClampSampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

// Big-triangle fullscreen technique: 3 vertices, no vertex buffer, clipped to the viewport.
//
// IMPORTANT: the vertices this generates are emitted in clockwise winding in clip space, which
// Direct3D's default rasterizer state treats as back-facing and culls, producing an all-black
// render target. DirectX11VfxEffectRunner therefore builds its rasterizer state with
// CullMode.None. Do not "clean up" either side of that pairing independently: changing the
// vertex order here without changing the cull mode (or vice versa) silently reintroduces the
// all-black-output bug.
PSInput FullscreenVS(uint vertexId : SV_VertexID)
{
    PSInput output;
    float2 ndc = float2((vertexId << 1) & 2, vertexId & 2) * 2.0 - 1.0;
    output.Position = float4(ndc, 0, 1);
    // FloatImageAsset stores the top row first, so V is flipped relative to NDC Y.
    output.UV = float2((ndc.x + 1.0) * 0.5, 0.5 - (ndc.y * 0.5));
    return output;
}

// Rotates a linear RGB color's hue by the given angle using a YIQ-style rotation matrix. Shared by
// every effect that hue-shifts its subject; do not copy this into an effect file.
//
// This matrix is not gamut-preserving: on HDR input (values above 1.0) some hue angles produce
// negative channel values. Callers must clamp the result to non-negative before using it further,
// otherwise negative values get written straight into the exported EXR.
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

// Pushes a color away from (multiplier > 1) or toward (multiplier < 1) its own luma, i.e. a
// saturation multiplier. 1.0 leaves the color unchanged; 0.0 collapses it to grayscale.
float3 BoostSaturation(float3 color, float saturationMultiplier)
{
    float luma = dot(color, float3(0.299, 0.587, 0.114));
    return luma + ((color - luma) * saturationMultiplier);
}

// Must stay in sync with helengine.vfx.VfxEasing.Apply, including the numeric easing kind order
// declared by helengine.vfx.VfxEasingKind.
float ApplyEasing(float t, float easingKind)
{
    float clamped = saturate(t);
    if (easingKind < 0.5) // Linear
    {
        return clamped;
    }
    if (easingKind < 1.5) // EaseIn
    {
        return clamped * clamped;
    }
    if (easingKind < 2.5) // EaseOut
    {
        return 1.0 - ((1.0 - clamped) * (1.0 - clamped));
    }
    // EaseInOut
    if (clamped < 0.5)
    {
        return 2.0 * clamped * clamped;
    }
    float inverted = (-2.0 * clamped) + 2.0;
    return 1.0 - ((inverted * inverted) / 2.0);
}

#endif // HELENGINE_VFX_COMMON_HLSLI
