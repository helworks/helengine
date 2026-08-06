#include "../common/VfxCommon.hlsli"

// Bound by DirectX11VfxEffectRunner in DepthCompositeEffect.InputRoles order: Subject, RenderColor,
// RenderDepth.
Texture2D SubjectTexture : register(t0);
Texture2D RenderColorTexture : register(t1);
Texture2D RenderDepthTexture : register(t2);

// Params0 // x: DepthThreshold, yzw: unused
// Params1 // unused
// Params2 // unused
// Params3 // unused

float4 DepthCompositePS(PSInput input) : SV_TARGET
{
    float depthThreshold = Params0.x;

    float4 subject = SubjectTexture.Sample(LinearClampSampler, input.UV);
    float3 renderColor = RenderColorTexture.Sample(LinearClampSampler, input.UV).rgb;
    float renderDepth = RenderDepthTexture.Sample(LinearClampSampler, input.UV).r;

    if (renderDepth <= depthThreshold)
    {
        // The render is nearer than the subject's depth plane here: it fully occludes the subject,
        // regardless of the subject's own alpha.
        return float4(renderColor, 1.0);
    }

    // The render is farther than the subject's depth plane here: composite the subject over it using
    // its own alpha. That alpha is not just 0 or 1 but feathered at soft edges (e.g. hair), so the
    // blend has to use the real sampled value rather than treating the mask as a hard cutout.
    float3 finalColor = lerp(renderColor, subject.rgb, subject.a);
    return float4(finalColor, 1.0);
}
