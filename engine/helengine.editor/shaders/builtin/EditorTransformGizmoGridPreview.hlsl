cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
};

static const float PreviewHalfExtent = 24.0f;

struct VS_IN
{
    float3 pos : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float2 localPos : TEXCOORD0;
};

float ComputeLine(float value)
{
    float distanceToLine = abs(frac(value + 0.5f) - 0.5f);
    float lineWidth = 0.06f;
    return saturate((lineWidth - distanceToLine) / lineWidth);
}

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.localPos = input.pos.xy;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float2 localPos = input.localPos;
    float radial = saturate(length(localPos) / PreviewHalfExtent);
    float edgeFade = 1.0f - radial;
    edgeFade *= edgeFade;
    float lineMask = max(ComputeLine(localPos.x), ComputeLine(localPos.y));
    float centerGlow = saturate(1.0f - (length(localPos) / 2.5f));
    centerGlow *= centerGlow;
    float axisGlow = max(
        saturate((0.10f - abs(localPos.x)) / 0.10f),
        saturate((0.10f - abs(localPos.y)) / 0.10f));
    float brightness = saturate((lineMask * 0.45f) + (centerGlow * 0.75f) + (axisGlow * 0.35f));
    float3 color = lerp(float3(0.72f, 0.72f, 0.76f), float3(1.0f, 1.0f, 1.0f), brightness);
    float alpha = edgeFade * ((lineMask * 0.24f) + (centerGlow * 0.28f) + (axisGlow * 0.14f) + 0.04f);
    clip(alpha - 0.01f);
    return float4(color, alpha);
}
