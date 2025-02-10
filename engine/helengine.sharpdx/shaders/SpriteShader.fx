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
    
    // ps
    float4 color;
};

Texture2D spriteTexture : register(t0);
SamplerState samp : register(s0);

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;
    
    float4 dest = destRect;
    dest.x += dest.z / 2.0;
    dest.y += dest.w / 2.0;
    dest.y *= -1;
    
    float2 destPos = dest.xy + (input.pos.xy * dest.zw);
    
    output.pos = mul(float4(destPos, input.pos.z, 1.0), world);
    
    // Remap UV coordinates to the sourceRect area
    output.uv = sourceRect.xy + input.uv * sourceRect.zw;
    
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    return spriteTexture.Sample(samp, input.uv) * color;
}
