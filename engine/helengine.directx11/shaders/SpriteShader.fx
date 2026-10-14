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
    
    // destRect.xy is the sprite's transformed top-left origin in Y-down screen pixels and the sprite rotates
    // around it, matching the PS2 corner geometry: origin, origin + right * w, origin + right * w + down * h and
    // origin + down * h, with right = (cos r, sin r) and down = (-sin r, cos r) in Y-down screen space.
    float4 dest = destRect;
    // The shared quad spans -0.5..0.5 with +y at its top; convert to 0..1 factors measured from the top-left, y down.
    float2 cornerFactor = float2(input.pos.x + 0.5, 0.5 - input.pos.y);
    float rotation = spriteTransform.x;
    float rotationSin = sin(rotation);
    float rotationCos = cos(rotation);
    float2 right = float2(rotationCos, rotationSin) * (dest.z * cornerFactor.x);
    float2 down = float2(-rotationSin, rotationCos) * (dest.w * cornerFactor.y);
    float2 screenPos = dest.xy + right + down;
    // The 2D projection maps screen y to negative world y.
    float2 destPos = float2(screenPos.x, -screenPos.y);
    
    output.pos = mul(float4(destPos, input.pos.z, 1.0), world);
    
    // Remap UV coordinates to the sourceRect area
    output.uv = sourceRect.xy + input.uv * sourceRect.zw;
    
    return output;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    return spriteTexture.Sample(samp, input.uv) * color;
}

// A scaled glyph or an intermediate surface cannot preserve physical RGB subpixel positions.
float4 FontGrayscalePS(PS_INPUT input) : SV_TARGET
{
    return float4(color.rgb, spriteTexture.Sample(samp, input.uv).a * color.a);
}

// Dual-source blending composites each coverage channel against the actual background.
struct FontBlendOutput
{
    float4 foreground : SV_Target0;
    float4 coverage : SV_Target1;
};

FontBlendOutput FontClearTypePS(PS_INPUT input)
{
    float4 mask = spriteTexture.Sample(samp, input.uv) * color.a;
    FontBlendOutput output;
    output.foreground = float4(color.rgb * mask.rgb, mask.a);
    output.coverage = mask;
    return output;
}
