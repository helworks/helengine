cbuffer BatchCameraBuffer : register(b0) {
    float4x4 projection;
};

struct BatchInput {
    float4 position : POSITION0;
    float4 texLocal : TEXCOORD0;
    float4 color : COLOR0;
    float4 shape : TEXCOORD1;
    float4 corners : TEXCOORD2;
    float4 borderColor : COLOR1;
};

struct BatchVertexOutput {
    float4 position : SV_POSITION;
#if HELENGINE_BATCH_ROUNDED
    float2 localPosition : TEXCOORD0;
    float4 color : COLOR0;
    float4 shape : TEXCOORD1;
    float4 corners : TEXCOORD2;
    float4 borderColor : COLOR1;
#else
    float2 texCoord : TEXCOORD0;
    float4 color : COLOR0;
#endif
};

#if !HELENGINE_BATCH_ROUNDED
Texture2D BatchTexture : register(t0);
SamplerState BatchSampler : register(s0);
#endif

BatchVertexOutput VS(BatchInput input) {
    BatchVertexOutput output;
    output.position = mul(input.position, projection);
#if HELENGINE_BATCH_ROUNDED
    output.localPosition = input.texLocal.zw;
    output.color = input.color;
    output.shape = input.shape;
    output.corners = input.corners;
    output.borderColor = input.borderColor;
#else
    output.texCoord = input.texLocal.xy;
    output.color = input.color;
#endif
    return output;
}

#if HELENGINE_BATCH_ROUNDED
float RoundedDistance(float2 localPosition, float2 halfSize, float radius, float4 corners) {
    bool isTop = localPosition.y < 0.0f;
    bool isLeft = localPosition.x < 0.0f;
    float cornerEnabled = isTop
        ? (isLeft ? corners.x : corners.y)
        : (isLeft ? corners.z : corners.w);
    float clampedRadius = cornerEnabled > 0.5f
        ? min(max(radius, 0.0f), min(halfSize.x, halfSize.y))
        : 0.0f;
    float2 distanceFromCorner = abs(localPosition) - halfSize + clampedRadius;
    return length(max(distanceFromCorner, 0.0f))
        + min(max(distanceFromCorner.x, distanceFromCorner.y), 0.0f)
        - clampedRadius;
}

float4 PS(BatchVertexOutput input) : SV_TARGET {
    float2 halfSize = max(input.shape.xy, 0.0f);
    float borderWidth = max(input.shape.w, 0.0f);
    float outerDistance = RoundedDistance(input.localPosition, halfSize, input.shape.z, input.corners);
    float outer = 1.0f - smoothstep(-0.5f, 0.5f, outerDistance);
    float inner = outer;
    if (borderWidth > 0.0f) {
        if (borderWidth >= min(halfSize.x, halfSize.y)) {
            inner = 0.0f;
        } else {
            float2 innerHalfSize = halfSize - borderWidth;
            float innerRadius = max(input.shape.z - borderWidth, 0.0f);
            float innerDistance = RoundedDistance(input.localPosition, innerHalfSize, innerRadius, input.corners);
            inner = min(outer, 1.0f - smoothstep(-0.5f, 0.5f, innerDistance));
        }
    }

    float fillAlpha = input.color.a * inner;
    float borderAlpha = input.borderColor.a * max(outer - inner, 0.0f);
    float alpha = fillAlpha + borderAlpha;
    float3 rgb = alpha > 0.0001f
        ? (input.color.rgb * fillAlpha + input.borderColor.rgb * borderAlpha) / alpha
        : 0.0f;
    return float4(rgb, alpha);
}
#else
float4 PS(BatchVertexOutput input) : SV_TARGET {
    return BatchTexture.Sample(BatchSampler, input.texCoord) * input.color;
}
#endif
