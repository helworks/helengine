cbuffer TransformBuffer : register(b0)
{
    float4x4 world;
    float4x4 worldViewProj;
    float4x4 normalMatrix;
    float4 cameraPosition;
    float4 materialFlags;
};

cbuffer ForwardLightBuffer : register(b1)
{
    float4 ambientLightColor;
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

cbuffer BaseColorBuffer : register(b3)
{
    float4 baseColor;
};

cbuffer RoughnessBuffer : register(b4)
{
    float4 roughnessValue;
};

cbuffer MetallicBuffer : register(b5)
{
    float4 metallicValue;
};

cbuffer SpecularBuffer : register(b6)
{
    float4 specularValue;
};

Texture2D shadowAtlasTexture : register(t1);
SamplerState shadowAtlasSampler : register(s1);
TextureCube pointShadowTexture0 : register(t2);
TextureCube pointShadowTexture1 : register(t3);
TextureCube pointShadowTexture2 : register(t4);
TextureCube pointShadowTexture3 : register(t5);
SamplerState pointShadowSampler : register(s2);
Texture2D DiffuseTexture : register(t0);
SamplerState DiffuseTextureSampler : register(s0);
Texture2D RoughnessTexture : register(t6);
SamplerState RoughnessTextureSampler : register(s6);

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
    float2 texCoord : TEXCOORD2;
};

PS_IN VS(VS_IN input)
{
    PS_IN output;
    float4 worldPosition = mul(float4(input.pos, 1.0f), world);
    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);
    output.worldPos = worldPosition.xyz;
    output.normal = mul(float4(input.normal, 0.0f), normalMatrix).xyz;
    output.texCoord = input.texCoord;
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

float DistributionGgx(float3 normal, float3 halfVector, float roughness)
{
    float alpha = max(roughness * roughness, 0.001f);
    float alphaSquared = alpha * alpha;
    float normalDotHalf = saturate(dot(normal, halfVector));
    float normalDotHalfSquared = normalDotHalf * normalDotHalf;
    float denominator = (normalDotHalfSquared * (alphaSquared - 1.0f)) + 1.0f;
    return alphaSquared / max(3.14159265f * denominator * denominator, 0.0001f);
}

float GeometrySchlickGgx(float normalDotDirection, float roughness)
{
    float visibility = roughness + 1.0f;
    float k = (visibility * visibility) * 0.125f;
    return normalDotDirection / max((normalDotDirection * (1.0f - k)) + k, 0.0001f);
}

float GeometrySmith(float3 normal, float3 viewDirection, float3 lightDirection, float roughness)
{
    float normalDotView = saturate(dot(normal, viewDirection));
    float normalDotLight = saturate(dot(normal, lightDirection));
    return GeometrySchlickGgx(normalDotView, roughness) * GeometrySchlickGgx(normalDotLight, roughness);
}

float3 FresnelSchlick(float cosineTheta, float3 reflectanceAtNormalIncidence)
{
    return reflectanceAtNormalIncidence + (1.0f - reflectanceAtNormalIncidence) * pow(1.0f - cosineTheta, 5.0f);
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
    float3 viewDirection,
    float roughness,
    float metallic,
    float specular)
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

    if (materialFlags.x > 0.5f)
    {
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
                float3 sampleDirection = lightToSurface / distanceToSurface;
                float currentDepth = saturate(distanceToSurface / positionAndRange.w);
                float sampledDepth = SamplePointShadowTexture(pointShadowTextureIndex, sampleDirection);
                float shadowBias = 0.01f;
                float shadowVisibility = (currentDepth - shadowBias) <= sampledDepth ? 1.0f : 0.0f;
                attenuation *= lerp(1.0f, shadowVisibility, shadowSlotMetadata.y);
            }
        }
    }

    float diffuse = saturate(dot(normal, lightDirection));
    if (diffuse <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 halfVector = normalize(lightDirection + viewDirection);
    float resolvedRoughness = max(roughness, 0.045f);
    float dielectricF0 = saturate(specular) * 0.08f;
    float3 dielectricReflectance = float3(dielectricF0, dielectricF0, dielectricF0);
    float3 reflectanceAtNormalIncidence = lerp(dielectricReflectance, surfaceColor, metallic);
    float3 fresnel = FresnelSchlick(saturate(dot(halfVector, viewDirection)), reflectanceAtNormalIncidence);
    float distribution = DistributionGgx(normal, halfVector, resolvedRoughness);
    float geometry = GeometrySmith(normal, viewDirection, lightDirection, resolvedRoughness);
    float normalDotView = saturate(dot(normal, viewDirection));
    float specularDenominator = max(4.0f * normalDotView * diffuse, 0.0001f);
    float3 specularColor = (distribution * geometry * fresnel / specularDenominator) * radiance * diffuse * attenuation;
    float3 diffuseWeight = (1.0f - fresnel) * (1.0f - metallic);
    float3 diffuseColor = (surfaceColor / 3.14159265f) * diffuseWeight * radiance * diffuse * attenuation;

    return diffuseColor + specularColor;
}

float4 PS(PS_IN input) : SV_Target
{
    float4 sampledBaseColor = DiffuseTexture.Sample(DiffuseTextureSampler, input.texCoord) * baseColor;
    float roughness = saturate(RoughnessTexture.Sample(RoughnessTextureSampler, input.texCoord).r * roughnessValue.x);
    float metallic = saturate(metallicValue.x);
    float specular = saturate(specularValue.x);
    float3 surfaceColor = sampledBaseColor.rgb;
    float3 normal = normalize(input.normal);
    float3 viewDirection = normalize(cameraPosition.xyz - input.worldPos);
    float3 color = surfaceColor * ambientLightColor.rgb;
    int activeLightCount = (int)(lightMetadata.x + 0.5f);

    if (activeLightCount > 0)
    {
        color += EvaluateForwardLight(light0ColorAndType, light0DirectionAndShadow, light0PositionAndRange, light0SpotAngles, shadowLight0AtlasRect, shadowLight0Metadata, shadowLight0WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection, roughness, metallic, specular);
    }

    if (activeLightCount > 1)
    {
        color += EvaluateForwardLight(light1ColorAndType, light1DirectionAndShadow, light1PositionAndRange, light1SpotAngles, shadowLight1AtlasRect, shadowLight1Metadata, shadowLight1WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection, roughness, metallic, specular);
    }

    if (activeLightCount > 2)
    {
        color += EvaluateForwardLight(light2ColorAndType, light2DirectionAndShadow, light2PositionAndRange, light2SpotAngles, shadowLight2AtlasRect, shadowLight2Metadata, shadowLight2WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection, roughness, metallic, specular);
    }

    if (activeLightCount > 3)
    {
        color += EvaluateForwardLight(light3ColorAndType, light3DirectionAndShadow, light3PositionAndRange, light3SpotAngles, shadowLight3AtlasRect, shadowLight3Metadata, shadowLight3WorldToShadowClip, surfaceColor, input.worldPos, normal, viewDirection, roughness, metallic, specular);
    }

    return float4(saturate(color), sampledBaseColor.a);
}
