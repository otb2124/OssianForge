#version 330 core
in vec3 vTexCoord;
out vec4 FragColor;

uniform vec3 uTopColor;
uniform vec3 uBottomColor;

void main()
{
    float t   = clamp(vTexCoord.y + 0.5, 0.0, 1.0); // remap -0.5..0.5 to 0..1
    FragColor = vec4(mix(uBottomColor, uTopColor, t), 1.0);
}