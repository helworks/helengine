#include "../common/VfxCommon.hlsli"

// Bound by DirectX11VfxEffectRunner in RainbowAuraEffect.InputRoles order: Source then Mask.
Texture2D SourceTexture : register(t0);
Texture2D MaskTexture : register(t1);

// Params0 // x: RepetitionCount, y: StartScale, z: ScaleStep, w: HueSpreadDegrees
// Params1 // x: GrowWindow, y: HueCyclesPerClip, z: Easing kind (0=Linear,1=EaseIn,2=EaseOut,3=EaseInOut), w: SaturationBoost
// Params2 // unused
// Params3 // unused

float4 RainbowAuraPS(PSInput input) : SV_TARGET
{
    float repetitionCount = max(Params0.x, 1.0);
    float startScale = Params0.y;
    float scaleStep = Params0.z;
    float hueSpreadDegrees = Params0.w;
    float growWindow = max(Params1.x, 0.0001);
    float hueCyclesPerClip = Params1.y;
    float easingKind = Params1.z;
    float saturationBoost = Params1.w;

    float t = ApplyEasing(NormalizedTime, easingKind);
    int repetitions = (int)round(repetitionCount);

    float3 accumulatedColor = float3(0.0, 0.0, 0.0);
    float accumulatedAlpha = 0.0;

    // Repetition i is "born" at birthTime and ramps from invisible to fully grown over growWindow.
    // Subtracting growWindow shifts repetition 0's birth before frame 0, so the un-repeated subject is
    // already fully visible immediately instead of also fading in from nothing.
    //
    // Iterating from the largest repetition down to the smallest and alpha-compositing each one "over"
    // the accumulator (rather than summing colors additively) is deliberate: every repetition is scaled
    // around the same frame center, so for a subject that fills most of the frame, most screen pixels
    // fall inside every repetition's silhouette at once. Additive summing there piles up N overlapping
    // bright layers into a blown-out white wash. Compositing back-to-front instead leaves the smallest
    // (newest) repetition sharp on top, with each larger (older) one visible only as the ring where the
    // repetitions in front of it do not reach, producing distinct rainbow rings instead of a wash.
    [loop]
    for (int i = repetitions - 1; i >= 0; i--)
    {
        float indexFraction = (float)i / repetitionCount;
        float birthTime = (indexFraction * (1.0 - growWindow)) - growWindow;
        float local = saturate((t - birthTime) / growWindow);
        if (local <= 0.0)
        {
            continue;
        }

        float scale = lerp(startScale, startScale + ((float)i * scaleStep), local);
        float2 centeredUV = (input.UV - 0.5) / max(scale, 0.0001);
        float2 sampleUV = centeredUV + 0.5;

        if (any(sampleUV < 0.0) || any(sampleUV > 1.0))
        {
            continue;
        }

        float maskAlpha = MaskTexture.Sample(LinearClampSampler, sampleUV).a * local;
        if (maskAlpha <= 0.0)
        {
            continue;
        }

        float3 sourceColor = SourceTexture.Sample(LinearClampSampler, sampleUV).rgb;
        float hueDegrees = ((float)i * hueSpreadDegrees / repetitionCount) + (360.0 * hueCyclesPerClip * t);
        // Repetition 0 is the un-echoed subject, not an echo, so it keeps its natural saturation even
        // though every echo behind it gets boosted; only the echoes should read as artificially vivid.
        float saturationMultiplier = (i == 0) ? 1.0 : saturationBoost;
        // The hue rotation is clamped non-negative per HueRotate's contract, then the saturation boost
        // is applied on top: the source footage is fairly desaturated, so a boost of 1.0 (no change)
        // would keep every echo looking nearly gray instead of a visible rainbow.
        float3 huedColor = max(BoostSaturation(max(HueRotate(sourceColor, hueDegrees), 0.0), saturationMultiplier), 0.0);

        // Standard "over" compositing: this repetition's opaque core replaces whatever is already
        // accumulated; its partially transparent edge blends with it.
        accumulatedColor = (huedColor * maskAlpha) + (accumulatedColor * (1.0 - maskAlpha));
        accumulatedAlpha = maskAlpha + (accumulatedAlpha * (1.0 - maskAlpha));
    }

    return float4(accumulatedColor, saturate(accumulatedAlpha));
}
