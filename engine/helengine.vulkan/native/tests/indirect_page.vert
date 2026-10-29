#version 450
layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=2) in vec2 uv;
layout(set=0,binding=0) uniform Transform { mat4 matrix; } transform;
layout(location=0) out vec3 color;
void main(){gl_Position=vec4(position,1)*transform.matrix;color=vec3(1,0.25+uv.x*0.25,normal.z);}
