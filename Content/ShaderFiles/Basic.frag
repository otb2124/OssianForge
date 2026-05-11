#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;

#define MAX_LIGHTS 16
struct Light {
    vec3 position;
    vec3 color;
    float intensity;
    float radius;
};
uniform Light uLights[MAX_LIGHTS];
uniform int uLightCount;

void main()
{
    vec3 normal = uHasNormalTexture == 1
        ? normalize(texture(uNormalTexture, vTexCoord).rgb * 2.0 - 1.0)
        : normalize(vNormal);

    vec3 totalLight = vec3(0.02); // ambient

    for (int i = 0; i < uLightCount; i++) {
        vec3  lightDir = normalize(uLights[i].position - vFragPos);
        float dist     = length(uLights[i].position - vFragPos);
        float atten    = clamp(1.0 - (dist / uLights[i].radius), 0.0, 1.0);
        atten         *= atten;
        float diff     = max(dot(normal, lightDir), 0.0);
        totalLight    += diff * uLights[i].intensity * atten * uLights[i].color;
    }

    vec4 texColor = texture(uTexture, vTexCoord);
    FragColor     = vec4(texColor.rgb * totalLight, texColor.a);
}