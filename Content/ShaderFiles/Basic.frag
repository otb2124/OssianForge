#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;

#define MAX_LIGHTS 16
#define LIGHT_POINT 0
#define LIGHT_SUN   1
#define LIGHT_SPOT  2

struct Light {
    int   type;
    vec3  position;
    vec3  direction;
    vec3  color;
    float intensity;
    float radius;
    float innerCutoff;
    float outerCutoff;
};

uniform Light uLights[MAX_LIGHTS];
uniform int   uLightCount;

vec3 CalcPoint(Light l, vec3 normal, vec3 fragPos)
{
    vec3  dir   = normalize(l.position - fragPos);
    float dist  = length(l.position - fragPos);
    float atten = clamp(1.0 - (dist / l.radius), 0.0, 1.0);
    atten      *= atten;
    float diff  = max(dot(normal, dir), 0.0);
    return diff * l.intensity * atten * l.color;
}

vec3 CalcSun(Light l, vec3 normal)
{
    vec3  dir  = normalize(-l.direction);
    float diff = max(dot(normal, dir), 0.0);
    return diff * l.intensity * l.color;
}

vec3 CalcSpot(Light l, vec3 normal, vec3 fragPos)
{
    vec3  dir      = normalize(l.position - fragPos);
    float dist     = length(l.position - fragPos);
    float atten    = clamp(1.0 - (dist / l.radius), 0.0, 1.0);
    atten         *= atten;
    float theta    = dot(dir, normalize(-l.direction));
    float epsilon  = l.innerCutoff - l.outerCutoff;
    float spotFade = clamp((theta - l.outerCutoff) / epsilon, 0.0, 1.0);
    float diff     = max(dot(normal, dir), 0.0);
    return diff * l.intensity * atten * spotFade * l.color;
}

void main()
{
    vec3 normal = uHasNormalTexture == 1
        ? normalize(texture(uNormalTexture, vTexCoord).rgb * 2.0 - 1.0)
        : normalize(vNormal);

    vec3 totalLight = vec3(0.02); // ambient

    for (int i = 0; i < uLightCount; i++)
    {
        if      (uLights[i].type == LIGHT_POINT) totalLight += CalcPoint(uLights[i], normal, vFragPos);
        else if (uLights[i].type == LIGHT_SUN)   totalLight += CalcSun(uLights[i], normal);
        else if (uLights[i].type == LIGHT_SPOT)  totalLight += CalcSpot(uLights[i], normal, vFragPos);
    }

    vec4 texColor = texture(uTexture, vTexCoord);
    FragColor     = vec4(texColor.rgb * totalLight, texColor.a);
}