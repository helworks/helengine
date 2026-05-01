cbuffer TransformBuffer : register(b0)
{
    float4x4 world;
    float4x4 worldViewProj;
    float4x4 normalMatrix;
    float4 cameraPosition;
};

struct VS_IN
{
    float3 pos : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 normal : TEXCOORD1;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    float4 worldPosition = mul(float4(input.pos, 1.0f), world);
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.worldPos = worldPosition.xyz;
    output.normal = mul(float4(input.normal, 0.0f), normalMatrix).xyz;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    float3 surfaceColor = float3(0.78f, 0.80f, 0.84f);
    float3 ambientColor = float3(0.12f, 0.13f, 0.15f);
    float3 lightColor = float3(1.00f, 0.97f, 0.92f);
    float3 lightDirection = normalize(float3(0.45f, 0.85f, 0.30f));
    float3 normal = normalize(input.normal);
    float3 viewDirection = normalize(cameraPosition.xyz - input.worldPos);
    float3 halfVector = normalize(lightDirection + viewDirection);
    float diffuse = saturate(dot(normal, lightDirection));
    float specular = pow(saturate(dot(normal, halfVector)), 32.0f);
    float3 color = (surfaceColor * (ambientColor + (diffuse * 0.90f))) + (lightColor * specular * 0.35f);
    return float4(saturate(color), 1.0f);
}
