#version 450
layout(location=0) in vec2 position;
layout(location=1) in vec2 uv;
layout(location=2) in vec4 color;
layout(push_constant) uniform Target { vec2 size; vec2 shapeSize; float radius; float borderThickness; uint corners; uint mode; vec4 borderColor; } target;
layout(location=0) out vec2 fragmentUv;
layout(location=1) out vec4 fragmentColor;
void main() {
    gl_Position = vec4(position / target.size * 2.0 - 1.0, 0.0, 1.0);
    fragmentUv = uv;
    fragmentColor = color;
}
