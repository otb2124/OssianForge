#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec3 uColor;
uniform float uAlpha;

void main()
{
    vec4 tex = texture(uTexture, vTexCoord);
    FragColor = vec4(tex.rgb * uColor, tex.a * uAlpha);
}