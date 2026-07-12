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
    float3 normal = normalize(input.normal);
    float3 lightDirection0 = normalize(float3(0.45f, 0.85f, -0.30f));
    float3 lightDirection1 = normalize(float3(-0.60f, 0.55f, 0.65f));
    float diffuse0 = saturate(dot(normal, lightDirection0));
    float diffuse1 = saturate(dot(normal, lightDirection1));
    float lighting = 0.22f + diffuse0 * 0.72f + diffuse1 * 0.28f;
    float3 handleColor = DecodeHandleColor(input.marker);
    return float4(handleColor * lighting, 1.0f);
}
