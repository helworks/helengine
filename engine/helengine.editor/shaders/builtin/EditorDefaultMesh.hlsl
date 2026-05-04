cbuffer TransformBuffer : register(b0)
{
    float4x4 world;
    float4x4 worldViewProj;
    float4x4 normalMatrix;
    float4 cameraPosition;
};

cbuffer ForwardLightBuffer : register(b1)
{
    float4 lightMetadata;
    float4 light0ColorAndType;
    float4 light0DirectionAndShadow;
    float4 light0PositionAndRange;
    float4 light0SpotAngles;
    float4 light1ColorAndType;
    float4 light1DirectionAndShadow;
    float4 light1PositionAndRange;
    float4 light1SpotAngles;
    float4 light2ColorAndType;
    float4 light2DirectionAndShadow;
    float4 light2PositionAndRange;
    float4 light2SpotAngles;
    float4 light3ColorAndType;
    float4 light3DirectionAndShadow;
    float4 light3PositionAndRange;
    float4 light3SpotAngles;
};

cbuffer ShadowBuffer : register(b2)
{
    float4 shadowMetadata;
    float4 shadowLight0AtlasRect;
    float4 shadowLight0Metadata;
    float4x4 shadowLight0WorldToShadowClip;
    float4 shadowLight1AtlasRect;
    float4 shadowLight1Metadata;
    float4x4 shadowLight1WorldToShadowClip;
    float4 shadowLight2AtlasRect;
    float4 shadowLight2Metadata;
    float4x4 shadowLight2WorldToShadowClip;
    float4 shadowLight3AtlasRect;
    float4 shadowLight3Metadata;
    float4x4 shadowLight3WorldToShadowClip;
};

Texture2D shadowAtlasTexture : register(t1);
SamplerState shadowAtlasSampler : register(s1);
TextureCube pointShadowTexture0 : register(t2);
TextureCube pointShadowTexture1 : register(t3);
TextureCube pointShadowTexture2 : register(t4);
TextureCube pointShadowTexture3 : register(t5);
SamplerState pointShadowSampler : register(s2);

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

float SamplePointShadowTexture(int textureIndex, float3 sampleDirection)
{
    if (textureIndex == 0)
    {
        return pointShadowTexture0.Sample(pointShadowSampler, sampleDirection).r;
    }

    if (textureIndex == 1)
    {
        return pointShadowTexture1.Sample(pointShadowSampler, sampleDirection).r;
    }

    if (textureIndex == 2)
    {
        return pointShadowTexture2.Sample(pointShadowSampler, sampleDirection).r;
    }

    return pointShadowTexture3.Sample(pointShadowSampler, sampleDirection).r;
}

float3 EvaluateForwardLight(
    float4 colorAndType,
    float4 directionAndShadow,
    float4 positionAndRange,
    float4 spotAngles,
    float4 shadowAtlasRect,
    float4 shadowSlotMetadata,
    float4x4 worldToShadowClip,
    float3 surfaceColor,
    float3 worldPos,
    float3 normal,
    float3 viewDirection)
{
    int lightType = (int)(colorAndType.w + 0.5f);
    float3 radiance = colorAndType.xyz;
    float3 lightDirection = float3(0.0f, 0.0f, 0.0f);
    float attenuation = 1.0f;

    if (lightType == 0)
    {
        lightDirection = normalize(-directionAndShadow.xyz);
    }
    else
    {
        float3 toLight = positionAndRange.xyz - worldPos;
        float distanceToLight = length(toLight);
        if (distanceToLight <= 0.0001f || positionAndRange.w <= 0.0f)
        {
            return float3(0.0f, 0.0f, 0.0f);
        }

        lightDirection = toLight / distanceToLight;
        float normalizedDistance = saturate(distanceToLight / positionAndRange.w);
        float rangeAttenuation = 1.0f - (normalizedDistance * normalizedDistance);
        attenuation = rangeAttenuation * rangeAttenuation;

        if (lightType == 2)
        {
            float3 lightForward = normalize(directionAndShadow.xyz);
            float3 lightToSurface = normalize(worldPos - positionAndRange.xyz);
            float cone = dot(lightForward, lightToSurface);
            float coneRange = max(spotAngles.x - spotAngles.y, 0.0001f);
            float spotAttenuation = saturate((cone - spotAngles.y) / coneRange);
            attenuation *= spotAttenuation * spotAttenuation;
        }
    }

    if (attenuation <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    if (shadowSlotMetadata.x > 0.5f && shadowSlotMetadata.z < 1.5f && shadowMetadata.x > 0.5f)
    {
        float4 shadowClip = mul(float4(worldPos, 1.0f), worldToShadowClip);
        if (abs(shadowClip.w) > 0.0001f)
        {
            float3 shadowNdc = shadowClip.xyz / shadowClip.w;
            float2 shadowUv = float2((shadowNdc.x * 0.5f) + 0.5f, (-shadowNdc.y * 0.5f) + 0.5f);
            if (shadowUv.x >= 0.0f && shadowUv.x <= 1.0f && shadowUv.y >= 0.0f && shadowUv.y <= 1.0f && shadowNdc.z >= 0.0f && shadowNdc.z <= 1.0f)
            {
                float2 atlasUv = shadowAtlasRect.xy + (shadowUv * shadowAtlasRect.zw);
                float sampledDepth = shadowAtlasTexture.Sample(shadowAtlasSampler, atlasUv).r;
                float shadowBias = 0.0015f;
                float shadowVisibility = (shadowNdc.z - shadowBias) <= sampledDepth ? 1.0f : 0.0f;
                attenuation *= lerp(1.0f, shadowVisibility, shadowSlotMetadata.y);
            }
        }
    }
    else if (shadowSlotMetadata.x > 0.5f && shadowSlotMetadata.z > 1.5f && lightType == 1)
    {
        float3 lightToSurface = worldPos - positionAndRange.xyz;
        float distanceToSurface = length(lightToSurface);
        if (distanceToSurface > 0.0001f && positionAndRange.w > 0.0f)
        {
            int pointShadowTextureIndex = (int)(shadowSlotMetadata.w + 0.5f);
            float3 sampleDirection = float3(lightToSurface.x, lightToSurface.y, -lightToSurface.z) / distanceToSurface;
            float currentDepth = saturate(distanceToSurface / positionAndRange.w);
            float sampledDepth = SamplePointShadowTexture(pointShadowTextureIndex, sampleDirection);
            float shadowBias = 0.01f;
            float shadowVisibility = (currentDepth - shadowBias) <= sampledDepth ? 1.0f : 0.0f;
            attenuation *= lerp(1.0f, shadowVisibility, shadowSlotMetadata.y);
        }
    }

    float diffuse = saturate(dot(normal, lightDirection));
    if (diffuse <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 halfVector = normalize(lightDirection + viewDirection);
    float specular = pow(saturate(dot(normal, halfVector)), 32.0f);
    float3 diffuseColor = surfaceColor * radiance * diffuse * attenuation;
    float3 specularColor = radiance * specular * 0.35f * attenuation;
    return diffuseColor + specularColor;
}

float4 PS(PS_IN input) : SV_Target
{
    float3 surfaceColor = float3(0.78f, 0.80f, 0.84f);
    float3 ambientColor = float3(0.12f, 0.13f, 0.15f);
    float3 normal = normalize(input.normal);
    float3 viewDirection = normalize(cameraPosition.xyz - input.worldPos);
    float3 color = surfaceColor * ambientColor;
    int activeLightCount = (int)(lightMetadata.x + 0.5f);

    if (activeLightCount > 0)
    {
        color += EvaluateForwardLight(light0ColorAndType, light0DirectionAndShadow, light0PositionAndRange, light0SpotAngles, shadowLight0AtlasRect, shadowLight0Metadata, shadowLight0WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection);
    }

    if (activeLightCount > 1)
    {
        color += EvaluateForwardLight(light1ColorAndType, light1DirectionAndShadow, light1PositionAndRange, light1SpotAngles, shadowLight1AtlasRect, shadowLight1Metadata, shadowLight1WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection);
    }

    if (activeLightCount > 2)
    {
        color += EvaluateForwardLight(light2ColorAndType, light2DirectionAndShadow, light2PositionAndRange, light2SpotAngles, shadowLight2AtlasRect, shadowLight2Metadata, shadowLight2WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection);
    }

    if (activeLightCount > 3)
    {
        color += EvaluateForwardLight(light3ColorAndType, light3DirectionAndShadow, light3PositionAndRange, light3SpotAngles, shadowLight3AtlasRect, shadowLight3Metadata, shadowLight3WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection);
    }

    return float4(saturate(color), 1.0f);
}
