cbuffer TransformBuffer : register(b0)
{
    float4x4 worldViewProj;
};

Texture2D LabelTexture : register(t0);
SamplerState LabelSampler : register(s0);

struct VS_IN
{
    float3 pos : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PS_IN
{
    float4 pos : SV_POSITION;
    float2 texCoord : TEXCOORD0;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.texCoord = input.texCoord;
    return output;
}

float4 PS(PS_IN input) : SV_Target
{
    uint textureWidth;
    uint textureHeight;
    LabelTexture.GetDimensions(textureWidth, textureHeight);
    float2 texelSize = float2(1.0f / textureWidth, 1.0f / textureHeight);
    float4 color = LabelTexture.Sample(LabelSampler, input.texCoord);
    float outlineAlpha = 0.0f;
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(-texelSize.x, 0.0f)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(texelSize.x, 0.0f)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(0.0f, -texelSize.y)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(0.0f, texelSize.y)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(-texelSize.x, -texelSize.y)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(texelSize.x, -texelSize.y)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(-texelSize.x, texelSize.y)).a);
    outlineAlpha = max(outlineAlpha, LabelTexture.Sample(LabelSampler, input.texCoord + float2(texelSize.x, texelSize.y)).a);
    if (color.a > 0.01f)
    {
        return color;
    }

    clip(outlineAlpha - 0.01f);
    return float4(0.0f, 0.0f, 0.0f, 1.0f);
}
