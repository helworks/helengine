#include "../common/VfxCommon.hlsli"

// Built-in scene transitions, executed by DirectX11EffectExecutor with NormalizedTime = transition progress (0..1).
//
// Scene frames arrive in linear color with straight alpha; blending happens premultiplied and every pass returns
// straight alpha again, exactly like the compositor's crossfade.
//
// Shared parameter layout (see BuiltInVfxEffects transitions):
//   Params0.x  Easing kind (0=Linear, 1=EaseIn, 2=EaseOut, 3=EaseInOut)
//   Params0.y  Direction (0=Left, 1=Right, 2=Up, 3=Down) or Shape (0=Circle, 1=Diamond, 2=Square)
//   Params0.z  Amount: blur radius in output pixels, edge softness, zoom strength or intensity, per transition
//   Params0.w  Hold: fraction of the transition spent fully on the color (dip to color)
//   Params1    Linear RGBA color (dip to color, flash)

// Single-pass transitions and the first pass of multi-pass ones: t0 outgoing scene, t1 incoming scene.
// Later passes read their intermediate target at t0 (and the full-resolution mix at t1 for the blur dissolve).
Texture2D FromTexture : register(t0);
Texture2D ToTexture : register(t1);

static const float Pi = 3.14159265;
static const int MaxBlurTaps = 48;

// Eased transition progress.
float Progress()
{
    return ApplyEasing(saturate(NormalizedTime), Params0.x);
}

// Converts straight alpha to premultiplied alpha.
float4 Premultiply(float4 color)
{
    return float4(color.rgb * color.a, color.a);
}

// Converts premultiplied alpha back to the straight alpha the compositor expects.
float4 Unpremultiply(float4 color)
{
    return color.a > 0.0 ? float4(color.rgb / color.a, color.a) : float4(0.0, 0.0, 0.0, 0.0);
}

// Samples a scene as premultiplied color, transparent outside the frame so moving scenes leave clean edges.
float4 SampleScene(Texture2D scene, float2 uv)
{
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }
    return Premultiply(scene.SampleLevel(LinearClampSampler, uv, 0));
}

// Unit vector a scene travels along for the Direction parameter, in UV space (V grows downward).
float2 DirectionVector()
{
    int direction = (int)round(Params0.y);
    if (direction == 0) return float2(-1.0, 0.0);
    if (direction == 1) return float2(1.0, 0.0);
    if (direction == 2) return float2(0.0, -1.0);
    return float2(0.0, 1.0);
}

// Premultiplied color parameter.
float4 ColorParameter()
{
    return float4(Params1.rgb * Params1.a, Params1.a);
}

// Classic dissolve with an easing curve.
float4 DissolvePS(PSInput input) : SV_TARGET
{
    float4 from = SampleScene(FromTexture, input.UV);
    float4 to = SampleScene(ToTexture, input.UV);
    return Unpremultiply(lerp(from, to, Progress()));
}

// Fades the outgoing scene into a solid color, optionally holds it, then fades into the incoming scene.
float4 DipToColorPS(PSInput input) : SV_TARGET
{
    float progress = Progress();
    float hold = saturate(Params0.w);
    float halfSpan = max((1.0 - hold) * 0.5, 0.0001);
    float4 color = ColorParameter();
    float4 result;
    if (progress < halfSpan)
    {
        result = lerp(SampleScene(FromTexture, input.UV), color, progress / halfSpan);
    }
    else if (progress > 1.0 - halfSpan)
    {
        result = lerp(color, SampleScene(ToTexture, input.UV), (progress - (1.0 - halfSpan)) / halfSpan);
    }
    else
    {
        result = color;
    }
    return Unpremultiply(result);
}

// Pushes the outgoing scene out along the direction while the incoming scene follows it in.
float4 PushScenes(float2 uv, float progress)
{
    float2 direction = DirectionVector();
    float4 from = SampleScene(FromTexture, uv - direction * progress);
    float4 to = SampleScene(ToTexture, uv - direction * (progress - 1.0));
    return from + to * (1.0 - from.a);
}

// Slide/push transition.
float4 PushPS(PSInput input) : SV_TARGET
{
    return Unpremultiply(PushScenes(input.UV, Progress()));
}

// A soft-edged line sweeps across the frame in the chosen direction, revealing the incoming scene behind it.
float4 WipePS(PSInput input) : SV_TARGET
{
    int direction = (int)round(Params0.y);
    float position = direction == 0 ? 1.0 - input.UV.x : direction == 1 ? input.UV.x : direction == 2 ? 1.0 - input.UV.y : input.UV.y;
    float softness = max(Params0.z, 0.0001);
    float edge = Progress() * (1.0 + softness);
    float reveal = 1.0 - smoothstep(edge - softness, edge, position);
    return Unpremultiply(lerp(SampleScene(FromTexture, input.UV), SampleScene(ToTexture, input.UV), reveal));
}

// The outgoing scene zooms in and fades while the incoming scene settles from a zoomed-in start.
float4 ZoomPS(PSInput input) : SV_TARGET
{
    float progress = Progress();
    float strength = max(Params0.z, 0.0);
    float2 center = input.UV - 0.5;
    float4 from = SampleScene(FromTexture, center / (1.0 + strength * progress) + 0.5);
    float4 to = SampleScene(ToTexture, center / (1.0 + strength * (1.0 - progress)) + 0.5);
    return Unpremultiply(lerp(from, to, smoothstep(0.2, 0.8, progress)));
}

// Distance of a point from the frame center in the metric of the chosen shape, with aspect correction.
float ShapeDistance(float2 offset)
{
    int shape = (int)round(Params0.y);
    float2 magnitude = abs(offset);
    if (shape == 1) return magnitude.x + magnitude.y;
    if (shape == 2) return max(magnitude.x, magnitude.y);
    return length(offset);
}

// A circle, diamond or square grows from the center, revealing the incoming scene inside it.
float4 ShapeRevealPS(PSInput input) : SV_TARGET
{
    float aspect = Resolution.x / max(Resolution.y, 1.0);
    float2 scale = float2(aspect, 1.0);
    float corner = ShapeDistance(0.5 * scale);
    float softness = max(Params0.z, 0.0001) * corner;
    float radius = Progress() * (corner + softness);
    float reveal = 1.0 - smoothstep(radius - softness, radius, ShapeDistance((input.UV - 0.5) * scale));
    return Unpremultiply(lerp(SampleScene(FromTexture, input.UV), SampleScene(ToTexture, input.UV), reveal));
}

// Cuts at the midpoint under a bright burst of the chosen color.
float4 FlashPS(PSInput input) : SV_TARGET
{
    float progress = Progress();
    float4 scene = lerp(SampleScene(FromTexture, input.UV), SampleScene(ToTexture, input.UV), smoothstep(0.45, 0.55, progress));
    float burst = exp(-pow((progress - 0.5) / 0.18, 2.0)) * max(Params0.z, 0.0);
    float4 color = ColorParameter() * burst;
    return Unpremultiply(float4(scene.rgb + color.rgb, saturate(scene.a + color.a)));
}

// Pseudo-random number in [0, 1) for a cell.
float Hash(float2 cell)
{
    return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
}

// Digital glitch: horizontal block displacement and RGB split peaking at the midpoint, with flickering scene swaps.
float4 GlitchPS(PSInput input) : SV_TARGET
{
    float progress = saturate(NormalizedTime);
    float strength = max(Params0.z, 0.0) * (1.0 - abs(2.0 * progress - 1.0));
    float frameStep = floor(progress * 30.0);
    float row = floor(input.UV.y * 28.0);
    float jitter = Hash(float2(row, frameStep));
    float2 uv = input.UV;
    if (jitter > 1.0 - strength * 0.6)
    {
        uv.x += (Hash(float2(frameStep, row + 7.0)) - 0.5) * 0.25 * strength;
    }
    // Displaced rows wrap around instead of exposing transparent edges.
    uv.x = frac(uv.x);
    bool incoming = progress > 0.5;
    if (Hash(float2(frameStep, 3.0)) < strength * 0.35)
    {
        incoming = !incoming;
    }
    float split = 0.012 * strength;
    float2 leftUv = float2(clamp(uv.x - split, 0.0, 1.0), uv.y);
    float2 rightUv = float2(clamp(uv.x + split, 0.0, 1.0), uv.y);
    float4 center = incoming ? SampleScene(ToTexture, uv) : SampleScene(FromTexture, uv);
    float4 left = incoming ? SampleScene(ToTexture, leftUv) : SampleScene(FromTexture, leftUv);
    float4 right = incoming ? SampleScene(ToTexture, rightUv) : SampleScene(FromTexture, rightUv);
    return Unpremultiply(float4(left.r, center.g, right.b, center.a));
}

// Blur radius in output pixels: zero at both ends, strongest at the midpoint.
float BlurRadius()
{
    return max(Params0.z, 0.0) * sin(Pi * saturate(NormalizedTime));
}

// Gaussian blur of premultiplied color along a direction given in output pixels.
float4 BlurAlong(Texture2D image, float2 uv, float2 direction, float radius)
{
    if (radius < 0.5)
    {
        return Premultiply(image.SampleLevel(LinearClampSampler, uv, 0));
    }
    float2 pixelStep = direction * MainTexelSize;
    int taps = (int)min(ceil(radius), (float)MaxBlurTaps);
    float spacing = radius / taps;
    float sigma = radius * 0.5;
    float4 total = 0.0;
    float weight = 0.0;
    [loop]
    for (int index = -taps; index <= taps; index++)
    {
        float distance = index * spacing;
        float tapWeight = exp(-0.5 * distance * distance / (sigma * sigma));
        total += tapWeight * Premultiply(image.SampleLevel(LinearClampSampler, uv + pixelStep * distance, 0));
        weight += tapWeight;
    }
    return total / weight;
}

// Blur dissolve, pass 1: eased dissolve of the two scenes at full resolution.
float4 BlurMixPS(PSInput input) : SV_TARGET
{
    return DissolvePS(input);
}

// Blur dissolve, pass 2: horizontal blur of the mix into a half-resolution target.
float4 BlurHorizontalPS(PSInput input) : SV_TARGET
{
    return Unpremultiply(BlurAlong(FromTexture, input.UV, float2(1.0, 0.0), BlurRadius()));
}

// Blur dissolve, pass 3: vertical blur, returning to the sharp mix as the radius vanishes at both ends.
float4 BlurFinalPS(PSInput input) : SV_TARGET
{
    float radius = BlurRadius();
    float4 blurred = BlurAlong(FromTexture, input.UV, float2(0.0, 1.0), radius);
    float4 sharp = Premultiply(ToTexture.SampleLevel(LinearClampSampler, input.UV, 0));
    return Unpremultiply(lerp(sharp, blurred, saturate(radius / 3.0)));
}

// Whip pan, pass 1: a fast eased push.
float4 WhipPushPS(PSInput input) : SV_TARGET
{
    return Unpremultiply(PushScenes(input.UV, ApplyEasing(saturate(NormalizedTime), 3.0)));
}

// Whip pan, pass 2: motion blur along the push direction, strongest mid-swing.
float4 WhipBlurPS(PSInput input) : SV_TARGET
{
    return Unpremultiply(BlurAlong(FromTexture, input.UV, DirectionVector(), BlurRadius()));
}
