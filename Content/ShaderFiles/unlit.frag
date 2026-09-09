#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;

// Added base color uniform (defaults to vec4(1.0) from C# for plain textures)
uniform vec4 uBaseColor;

void main()
{
    vec3 normal = uHasNormalTexture == 1
        ? normalize(texture(uNormalTexture, vTexCoord).rgb * 2.0 - 1.0)
        : normalize(vNormal);

    vec3 totalLight = vec3(1);

    // Multiplies sampled texture color (or default white bound texture) by base color
    vec4 texColor = texture(uTexture, vTexCoord) * uBaseColor;
    FragColor     = vec4(texColor.rgb * totalLight, texColor.a);
}