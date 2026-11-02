#version 450
layout(set=0,binding=0) uniform sampler2D sourceTexture;
layout(location=0) in vec2 fragmentUv;
layout(location=1) in vec4 fragmentColor;
layout(location=0) out vec4 outputColor;
layout(push_constant) uniform Target { vec2 size; vec2 shapeSize; float radius; float borderThickness; uint corners; uint mode; vec4 borderColor; } target;
void main() {
    if (target.mode == 0) { outputColor = texture(sourceTexture, fragmentUv) * fragmentColor; return; }
    uint corner = fragmentUv.y < 0.5 ? (fragmentUv.x < 0.5 ? 1 : 2) : (fragmentUv.x < 0.5 ? 4 : 8);
    float radius = (target.corners & corner) != 0 ? target.radius : 0.0;
    vec2 point = abs(fragmentUv * target.shapeSize - target.shapeSize * 0.5) - (target.shapeSize * 0.5 - radius);
    float distance = length(max(point,vec2(0.0))) + min(max(point.x,point.y),0.0) - radius;
    float coverage = 1.0 - smoothstep(-0.5,0.5,distance);
    float inner = target.borderThickness > 0.0 ? 1.0 - smoothstep(-0.5,0.5,distance + target.borderThickness) : 1.0;
    outputColor = mix(target.borderColor,fragmentColor,inner);
    outputColor.a *= coverage;
}
