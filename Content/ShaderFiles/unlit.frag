#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;

void main()
{
    vec3 normal = uHasNormalTexture == 1
        ? normalize(texture(uNormalTexture, vTexCoord).rgb * 2.0 - 1.0)
        : normalize(vNormal);

    vec3 totalLight = vec3(1);

    vec4 texColor = texture(uTexture, vTexCoord);
    FragColor     = vec4(texColor.rgb * totalLight, texColor.a);
}