struct VS_INPUT
{
    float3 pos : POSITION;
    float2 uv : TEXCOORD;
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD;
};

cbuffer TransformBuffer : register(b0)
{
    // vs
    matrix world;
    float4 sourceRect; // (x, y, width, height) in normalized texture coordinates
    float4 destRect;
    float4 spriteTransform;
    
    // ps
    float4 color;
};

Texture2D spriteTexture : register(t0);
SamplerState samp : register(s0);

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    
    float4 dest = destRect;
    float2 center = float2(dest.x + (dest.z * 0.5), -(dest.y + (dest.w * 0.5)));
    float2 localOffset = input.pos.xy * dest.zw;
    float rotation = spriteTransform.x;
    float rotationSin = sin(rotation);
    float rotationCos = cos(rotation);
    float2 rotatedOffset = float2(
        (localOffset.x * rotationCos) - (localOffset.y * rotationSin),
        (localOffset.x * rotationSin) + (localOffset.y * rotationCos));
    float2 destPos = center + rotatedOffset;
    
    output.pos = mul(float4(destPos, input.pos.z, 1.0), world);
    
    // Remap UV coordinates to the sourceRect area
    output.uv = sourceRect.xy + input.uv * sourceRect.zw;
    
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    return spriteTexture.Sample(samp, input.uv) * color;
}
