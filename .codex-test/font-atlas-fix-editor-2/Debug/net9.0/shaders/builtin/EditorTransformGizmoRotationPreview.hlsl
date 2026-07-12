cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
};

static const float PreviewRadius = 0.92f;

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
    float snapDegrees : TEXCOORD1;
};

float ComputeLine(float distanceToLine, float lineWidth)
{
    return saturate((lineWidth - distanceToLine) / lineWidth);
}

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.localPos = input.pos.xy;
    output.snapDegrees = input.texCoord.x;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float2 localPos = input.localPos;
    float radius = length(localPos);
    float normalizedRadius = radius / PreviewRadius;
    float edgeFade = saturate(1.0f - normalizedRadius);
    edgeFade *= edgeFade;
    float outerRing = ComputeLine(abs(normalizedRadius - 0.96f), 0.08f);
    float centerGlow = saturate(1.0f - (radius / (PreviewRadius * 0.58f)));
    centerGlow *= centerGlow;
    float snapRadians = max(radians(max(input.snapDegrees, 1.0f)), 0.0001f);
    float angle = atan2(localPos.y, localPos.x);
    float spokePhase = frac((angle + 3.14159265f) / snapRadians);
    float spokeDistance = abs(spokePhase - 0.5f);
    float spokeMask = ComputeLine(spokeDistance, 0.035f);
    float spokeRadialFade = saturate((normalizedRadius - 0.12f) / 0.18f) * saturate((1.0f - normalizedRadius) / 0.05f);
    spokeMask *= spokeRadialFade;
    float brightness = saturate((spokeMask * 0.52f) + (centerGlow * 0.78f) + (outerRing * 0.26f));
    float3 color = lerp(float3(0.72f, 0.72f, 0.76f), float3(1.0f, 1.0f, 1.0f), brightness);
    float alpha = edgeFade * ((spokeMask * 0.18f) + (centerGlow * 0.24f) + (outerRing * 0.10f) + 0.03f);
    clip(alpha - 0.01f);
    return float4(color, alpha);
}
