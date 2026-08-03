cbuffer VfxFrameConstants : register(b0)
{
    float NormalizedTime;
    float2 Resolution;
    float Reserved;
    float4 Params0; // x: HueCyclesPerClip, y: StartScale, z: EndScale, w: Easing kind (0=Linear,1=EaseIn,2=EaseOut,3=EaseInOut)
    float4 Params1; // xyz: BackgroundColor, w: unused
    float4 Params2; // unused
    float4 Params3; // unused
};

Texture2D SourceTexture : register(t0);
Texture2D MaskTexture : register(t1);
SamplerState LinearClampSampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

// Big-triangle fullscreen technique: 3 vertices, no vertex buffer, clipped to the viewport.
PSInput FullscreenVS(uint vertexId : SV_VertexID)
{
    PSInput output;
    float2 ndc = float2((vertexId << 1) & 2, vertexId & 2) * 2.0 - 1.0;
    output.Position = float4(ndc, 0, 1);
    output.UV = float2((ndc.x + 1.0) * 0.5, 0.5 - (ndc.y * 0.5));
    return output;
}

// Must stay in sync with helengine.vfx.VfxEasing.Apply.
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
    float3 huedColor = HueRotate(sourceColor, 360.0 * hueCyclesPerClip * t);

    float3 finalColor = lerp(backgroundColor, huedColor, alpha);
    return float4(finalColor, 1.0);
}
