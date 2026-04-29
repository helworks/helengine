struct VS_INPUT
{
    float3 pos : POSITION;
    float2 uv  : TEXCOORD;
};

struct PS_INPUT
{
    float4 pos      : SV_POSITION;
    float2 localPos : TEXCOORD0; // local position in pixels relative to rect center
};

cbuffer ShapeBuffer : register(b0)
{
    matrix world;
    float4 destRect;   // x,y,w,h (pixels)
    float4 params1;    // radius, border, aa, _
    float4 fillColor;
    float4 borderColor;
};

PS_INPUT VS(VS_INPUT input)
{
    PS_INPUT output;

    float4 dest = destRect;
    // center to match existing sprite transform
    dest.x += dest.z * 0.5;
    dest.y += dest.w * 0.5;
    dest.y *= -1.0; // match coordinate system in existing pipeline

    float2 destPos = dest.xy + (input.pos.xy * dest.zw);
    output.pos = mul(float4(destPos, input.pos.z, 1.0), world);

    // local position in pixels relative to rect center
    output.localPos = input.pos.xy * destRect.zw;
    
    return output;
}

float sdRoundRect(float2 p, float2 b, float r)
{
    // distance from rounded rectangle of half-size b and radius r
    float2 q = abs(p) - (b - r);
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float4 PS(PS_INPUT input) : SV_TARGET
{
    float radius = params1.x;
    float border = max(params1.y, 0.0);
    float aa = max(params1.z, 0.5);

    float2 halfSize = 0.5 * destRect.zw;

    float dOuter = sdRoundRect(input.localPos, halfSize, radius);
    float alphaFill = 1.0 - smoothstep(-aa, aa, dOuter); // inside outer shape

    // Border: difference between outer and inner shapes
    float innerRadius = max(radius - border, 0.0);
    float2 innerHalf = float2(max(halfSize.x - border, 0.0), max(halfSize.y - border, 0.0));
    float dInner = sdRoundRect(input.localPos, innerHalf, innerRadius);
    float alphaInner = 1.0 - smoothstep(-aa, aa, dInner);
    float alphaBorder = saturate(alphaFill - alphaInner);

    float4 colFill = fillColor * alphaFill;
    float4 colBorder = borderColor * alphaBorder;

    // Composite border over fill
    float a = colFill.a + colBorder.a * (1.0 - colFill.a);
    float3 rgb = 0.0.xxx;
    if (a > 0.0001)
    {
        rgb = (colBorder.rgb * colBorder.a + colFill.rgb * colFill.a * (1.0 - colBorder.a)) / a;
    }

    return float4(rgb, a);
}

