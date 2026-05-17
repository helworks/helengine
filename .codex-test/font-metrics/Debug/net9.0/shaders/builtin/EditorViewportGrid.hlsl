cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
};

static const float GridHalfExtent = 5.0f;
static const float GridLineWidth = 0.035f;
static const float CenterLineWidth = 0.055f;
static const float3 GridLineColor = float3(0.56f, 0.56f, 0.56f);
static const float3 CenterLineColor = float3(0.0f, 0.0f, 0.0f);

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

float ComputeRepeatingLineMask(float value, float width)
{
    float distanceToLine = abs(frac(value + 0.5f) - 0.5f);
    return saturate((width - distanceToLine) / width);
}

float ComputeCenterLineMask(float value, float width)
{
    return saturate((width - abs(value)) / width);
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
    clip(GridHalfExtent - abs(localPos.x));
    clip(GridHalfExtent - abs(localPos.y));

    float gridMask = max(
        ComputeRepeatingLineMask(localPos.x, GridLineWidth),
        ComputeRepeatingLineMask(localPos.y, GridLineWidth));
    float centerMask = max(
        ComputeCenterLineMask(localPos.x, CenterLineWidth),
        ComputeCenterLineMask(localPos.y, CenterLineWidth));
    float alpha = max(gridMask, centerMask);
    clip(alpha - 0.01f);

    float3 color = lerp(GridLineColor, CenterLineColor, saturate(centerMask));
    return float4(color, alpha);
}
