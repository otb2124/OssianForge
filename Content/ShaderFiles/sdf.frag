#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec4 uTextColor;

void main()
{
    float dist  = texture(uTexture, vTexCoord).r;
    float alpha = smoothstep(0.47, 0.53, dist);
    FragColor   = vec4(uTextColor.rgb, uTextColor.a * alpha);
}