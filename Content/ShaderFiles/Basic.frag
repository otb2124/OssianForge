#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;   // ← add this
out vec4 FragColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;
uniform vec3 uLightPos;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform float uLightRadius;

void main()
{
    vec3 normal = uHasNormalTexture == 1
        ? normalize(texture(uNormalTexture, vTexCoord).rgb * 2.0 - 1.0)
        : normalize(vNormal);

    vec3 lightDir = normalize(uLightPos - vFragPos);
    float dist    = length(uLightPos - vFragPos);
    float atten   = clamp(1.0 - (dist / uLightRadius), 0.0, 1.0);
    atten        *= atten;

    float diff  = max(dot(normal, lightDir), 0.0);
    vec3 light = (0.02 + diff * uLightIntensity * atten) * uLightColor;

    vec4 texColor = texture(uTexture, vTexCoord);
    FragColor     = vec4(texColor.rgb * light, texColor.a);
}