struct VS_IN
{
    float3 pos : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 normal : NORMAL;
    float2 marker : TEXCOORD0;
};

float4x4 worldViewProj;

float3 DecodeHandleColor(float2 marker)
{
    if (marker.x > 0.85f && marker.y > 0.85f)
    {
        return float3(1.00f, 0.90f, 0.20f);
    }

    if (marker.x > 0.45f && marker.x < 0.55f && marker.y > 0.85f)
    {
        return float3(1.00f, 0.35f, 0.95f);
    }

    if (marker.x > 0.85f && marker.y > 0.45f && marker.y < 0.55f)
    {
        return float3(0.25f, 0.95f, 0.95f);
    }

    if (marker.y > 0.5f)
    {
        return float3(0.20f, 0.50f, 1.00f);
    }

    if (marker.x > 0.5f)
    {
        return float3(0.20f, 0.95f, 0.35f);
    }

    return float3(1.00f, 0.30f, 0.30f);
}

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.normal = input.normal;
    output.marker = input.texCoord;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    return float4(1.0f, 1.0f, 1.0f, 1.0f);
}
