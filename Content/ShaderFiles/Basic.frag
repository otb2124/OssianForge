#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;
uniform mat4 uView;
uniform float uRimIntensity; // was hardcoded "tune to taste" constant, now exposed
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

vec3 GetViewPos()
{
    mat3 rotT = transpose(mat3(uView));
    vec3 translation = vec3(uView[3]);
    return -rotT * translation;
}

float FresnelSchlick(float cosTheta, float F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

float CalcSpecular(vec3 normal, vec3 lightDir, vec3 viewDir, float shininess)
{
    vec3  halfway    = normalize(lightDir + viewDir);
    float base       = pow(max(dot(normal, halfway), 0.0), shininess);
    float cosTheta   = max(dot(viewDir, halfway), 0.0);
    float fresnel    = FresnelSchlick(cosTheta, 0.04);
    return base * fresnel;
}

// Falloff switched from (1 - d/r)^2 to smoothstep(0, r, d) inverted.
// Same "goes to zero at radius" behavior, but the curve is smoother
// through the middle instead of dropping off harder near the edge.
float CalcAttenuation(float dist, float radius)
{
    return 1.0 - smoothstep(0.0, radius, dist);
}

vec3 CalcPoint(Light l, vec3 normal, vec3 fragPos, vec3 viewDir)
{
    vec3  dir   = normalize(l.position - fragPos);
    float dist  = length(l.position - fragPos);
    float atten = CalcAttenuation(dist, l.radius);
    float diff  = max(dot(normal, dir), 0.0);
    float spec  = CalcSpecular(normal, dir, viewDir, 32.0);
    return (diff + spec) * l.intensity * atten * l.color;
}
vec3 CalcSun(Light l, vec3 normal, vec3 viewDir)
{
    vec3  dir  = normalize(-l.direction);
    float diff = max(dot(normal, dir), 0.0);
    float spec = CalcSpecular(normal, dir, viewDir, 32.0);
    return (diff + spec) * l.intensity * l.color;
}
vec3 CalcSpot(Light l, vec3 normal, vec3 fragPos, vec3 viewDir)
{
    vec3  dir      = normalize(l.position - fragPos);
    float dist     = length(l.position - fragPos);
    float atten    = CalcAttenuation(dist, l.radius);
    float theta    = dot(dir, normalize(-l.direction));
    float epsilon  = max(l.innerCutoff - l.outerCutoff, 0.0001);
    float spotFade = clamp((theta - l.outerCutoff) / epsilon, 0.0, 1.0);
    float diff     = max(dot(normal, dir), 0.0);
    float spec     = CalcSpecular(normal, dir, viewDir, 32.0);
    return (diff + spec) * l.intensity * atten * spotFade * l.color;
}

mat3 CalcTBN(vec3 N, vec3 fragPos, vec2 texCoord)
{
    vec3 dp1 = dFdx(fragPos);
    vec3 dp2 = dFdy(fragPos);
    vec2 duv1 = dFdx(texCoord);
    vec2 duv2 = dFdy(texCoord);

    vec3 dp2perp = cross(dp2, N);
    vec3 dp1perp = cross(N, dp1);
    vec3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    vec3 B = dp2perp * duv1.y + dp1perp * duv2.y;

    float invMax = inversesqrt(max(dot(T, T), dot(B, B)));
    return mat3(T * invMax, B * invMax, N);
}

vec3 ACESFilm(vec3 x)
{
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}

vec3 ApplyFog(vec3 color, vec3 fragPos, vec3 viewPos)
{
    const float density      = 0.01;
    const vec3  fogColor     = vec3(0.78, 0.72, 0.60);
    const float groundHeight = 7.86;
    const float heightFalloff = 0.05;

    float dist    = length(fragPos - viewPos);
    float distFog = 1.0 - exp2(-density * density * dist * dist);

    float heightAboveGround = max(fragPos.y - groundHeight, 0.0);
    float heightFog = exp2(-heightFalloff * heightAboveGround);

    float fogAmt = clamp(distFog * heightFog, 0.0, 1.0);

    return mix(color, fogColor, fogAmt);
}

// Interleaved gradient noise (Jorge Jimenez). Cheap, screen-space, no
// texture lookup needed — just a function of pixel coordinates. Produces
// a fine, high-frequency noise pattern that's specifically designed to
// dither well against TV/monitor quantization rather than looking like
// random static.
float InterleavedGradientNoise(vec2 pixelCoord)
{
    const vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(magic.z * fract(dot(pixelCoord, magic.xy)));
}

void main()
{
    vec3 geoNormal = normalize(vNormal);
    vec3 normal = geoNormal;

    if (uHasNormalTexture == 1)
    {
        vec3 sampledNormal = normalize(texture(uNormalTexture, vTexCoord).rgb * 2.0 - 1.0);
        mat3 TBN = CalcTBN(geoNormal, vFragPos, vTexCoord);
        normal = normalize(TBN * sampledNormal);
    }

    vec3 viewPos = GetViewPos();
    vec3 viewDir = normalize(viewPos - vFragPos);

    vec3 totalLight = vec3(0.02); // ambient
    for (int i = 0; i < uLightCount; i++)
    {
        if      (uLights[i].type == LIGHT_POINT) totalLight += CalcPoint(uLights[i], normal, vFragPos, viewDir);
        else if (uLights[i].type == LIGHT_SUN)   totalLight += CalcSun(uLights[i], normal, viewDir);
        else if (uLights[i].type == LIGHT_SPOT)  totalLight += CalcSpot(uLights[i], normal, vFragPos, viewDir);
    }

    // Rim reuses the same Schlick fresnel curve as specular (F0 = 0.04),
    // instead of an independently-tuned pow(x, 3.0). Grazing-angle rim glow
    // and grazing-angle specular move together under one constant.
    float rimCosTheta = max(dot(viewDir, normal), 0.0);
    float rimFresnel  = FresnelSchlick(rimCosTheta, 0.04);

    // Gate rim by scene brightness: without this, rim glows even in total
    // darkness, since it was purely a function of view/normal angle with no
    // tie to whether the surface is actually lit. luminance() gives a cheap
    // brightness estimate from totalLight to scale rim by.
    float sceneLuminance = dot(totalLight, vec3(0.2126, 0.7152, 0.0722));
    float rimGate = clamp(sceneLuminance * 4.0, 0.0, 1.0); // tune multiplier to taste

    vec3 rimColor = vec3(uRimIntensity) * rimFresnel * rimGate;

    vec4 texColor = texture(uTexture, vTexCoord);
    vec3 hdrColor = texColor.rgb * totalLight + rimColor;

    // Fog moved before tonemap + gamma. Fog is a linear-space phenomenon
    // (light scattering in atmosphere happens before any display-referred
    // encoding), and fogColor above is specified as a linear constant, so
    // blending it against an already-gamma-encoded color was mixing spaces.
    vec3 mapped = ApplyFog(hdrColor, vFragPos, viewPos);

    mapped = ACESFilm(mapped);
    mapped = pow(mapped, vec3(1.0 / 2.2));

    float noise = InterleavedGradientNoise(gl_FragCoord.xy);
    mapped += (noise - 0.5) / 128.0;

    FragColor = vec4(mapped, texColor.a);
}
