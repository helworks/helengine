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
};

float4x4 worldViewProj;

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.normal = input.normal; // Pass through normal data
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    // Visualize normals by mapping (-1 to 1) range to (0-1) color range
    float3 displayNormal = normalize(input.normal) * 0.5 + 0.5;
    return float4(displayNormal, 1.0);
}