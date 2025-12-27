#include "Includes/ShaderCommon.hlsl"

struct VS_INPUT {
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct PS_INPUT {
    float4 Position : HEL_POSITION_SEMANTIC;
    float2 TexCoord : TEXCOORD0;
};

HEL_CBUFFER_BEGIN(Frame, 0, 0)
    float4x4 WorldViewProj;
    float4 Tint;
HEL_CBUFFER_END(Frame, 0, 0);

HEL_TEXTURE2D(AlbedoTexture, 0, 1);
HEL_SAMPLER(LinearSampler, 0, 1);

PS_INPUT VSMain(VS_INPUT input) {
    PS_INPUT output;
    output.Position = mul(float4(input.Position, 1.0), WorldViewProj);
    output.TexCoord = input.TexCoord;
    return output;
}

float4 PSMain(PS_INPUT input) : HEL_TARGET_SEMANTIC {
    float4 texel = HEL_TEXTURE2D_SAMPLE(AlbedoTexture, LinearSampler, input.TexCoord);
    return texel * Tint;
}
