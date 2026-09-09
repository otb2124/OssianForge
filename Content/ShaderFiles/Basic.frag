#version 330 core
in vec3 vNormal;
in vec2 vTexCoord;
in vec3 vFragPos;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalTexture;
uniform int uHasNormalTexture;

// Base Color uniform added from C# ApplyContext
uniform vec4 uBaseColor;

uniform mat4 uView;
uniform float uRimIntensity; 
uniform float uWrapLighting; 
uniform int   uTonemapMode;  

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

vec3 CalcHemisphereAmbient(vec3 normal)
{
    const vec3 skyColor    = vec3(0.024, 0.026, 0.032);
    const vec3 groundColor = vec3(0.020, 0.018, 0.014);
    float t = normal.y * 0.5 + 0.5;
    return mix(groundColor, skyColor, t);
}

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

float CalcSpecular(vec3 normal, vec3 lightDir, vec3 viewDir, float shininess, out float outFresnel)
{
    vec3  halfway    = normalize(lightDir + viewDir);
    float base       = pow(max(dot(normal, halfway), 0.0), shininess);
    float cosTheta   = max(dot(viewDir, halfway), 0.0);
    outFresnel       = FresnelSchlick(cosTheta, 0.04);
    return base * outFresnel;
}

float CalcDiffuseSpec(vec3 normal, vec3 lightDir, vec3 viewDir, float shininess)
{
    float normalVariation  = length(fwidth(normal));
    float shininessAA       = shininess / (1.0 + shininess * normalVariation * 4.0);

    float fresnel;
    float spec = CalcSpecular(normal, lightDir, viewDir, shininessAA, fresnel);

    float dotNL       = dot(normal, lightDir);
    float dotHard     = max(dotNL, 0.0);
    float dotWrapped  = dotNL * 0.5 + 0.5;
    float diff = mix(dotHard, dotWrapped, uWrapLighting) * (1.0 - fresnel);
    return diff + spec;
}

float CalcAttenuation(float dist, float radius)
{
    return 1.0 - smoothstep(0.0, radius, dist);
}

vec3 CalcPoint(Light l, vec3 normal, vec3 fragPos, vec3 viewDir)
{
    vec3  dir      = normalize(l.position - fragPos);
    float dist     = length(l.position - fragPos);
    float atten    = CalcAttenuation(dist, l.radius);
    float diffSpec = CalcDiffuseSpec(normal, dir, viewDir, 32.0);
    return diffSpec * l.intensity * atten * l.color;
}

vec3 CalcSun(Light l, vec3 normal, vec3 viewDir)
{
    vec3  dir      = normalize(-l.direction);
    float diffSpec = CalcDiffuseSpec(normal, dir, viewDir, 32.0);
    return diffSpec * l.intensity * l.color;
}

vec3 CalcSpot(Light l, vec3 normal, vec3 fragPos, vec3 viewDir)
{
    vec3  dir      = normalize(l.position - fragPos);
    float dist     = length(l.position - fragPos);
    float atten    = CalcAttenuation(dist, l.radius);
    float theta    = dot(dir, normalize(-l.direction));
    float epsilon  = max(l.innerCutoff - l.outerCutoff, 0.0001);
    float spotFade = clamp((theta - l.outerCutoff) / epsilon, 0.0, 1.0);
    float diffSpec = CalcDiffuseSpec(normal, dir, viewDir, 32.0);
    return diffSpec * l.intensity * atten * spotFade * l.color;
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

vec3 Uncharted2TonemapCurve(vec3 x)
{
    float A = 0.15;
    float B = 0.50;
    float C = 0.10;
    float D = 0.20;
    float E = 0.02;
    float F = 0.30;
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

vec3 Uncharted2Tonemap(vec3 x)
{
    const float whitePoint = 11.2;
    vec3 curved = Uncharted2TonemapCurve(x);
    vec3 whiteScale = 1.0 / Uncharted2TonemapCurve(vec3(whitePoint));
    return clamp(curved * whiteScale, 0.0, 1.0);
}

vec3 ApplyFog(vec3 color, vec3 fragPos, vec3 viewPos, vec3 viewDir)
{
    const float density       = 0.01;
    const vec3  fogColorSky    = vec3(0.55, 0.65, 0.85);
    const vec3  fogColorGround = vec3(0.78, 0.72, 0.60);
    const float groundHeight  = 7.86;
    const float heightFalloff = 0.05;

    float dist    = length(fragPos - viewPos);
    float distFog = 1.0 - exp2(-density * density * dist * dist);

    float heightAboveGround = max(fragPos.y - groundHeight, 0.0);
    float heightFog = exp2(-heightFalloff * heightAboveGround);

    float fogAmt = clamp(distFog * heightFog, 0.0, 1.0);

    float skyBlend = clamp(viewDir.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 fogColor = mix(fogColorGround, fogColorSky, skyBlend);

    return mix(color, fogColor, fogAmt);
}

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

    vec3 totalLight = CalcHemisphereAmbient(normal);

    bool  hasSun       = false;
    vec3  sunDir       = vec3(0.0);
    vec3  sunColor     = vec3(0.0);
    float sunIntensity = 0.0;

    for (int i = 0; i < uLightCount; i++)
    {
        if (uLights[i].type == LIGHT_POINT)
        {
            totalLight += CalcPoint(uLights[i], normal, vFragPos, viewDir);
        }
        else if (uLights[i].type == LIGHT_SUN)
        {
            totalLight += CalcSun(uLights[i], normal, viewDir);
            hasSun        = true;
            sunDir        = uLights[i].direction;
            sunColor      = uLights[i].color;
            sunIntensity  = uLights[i].intensity;
        }
        else if (uLights[i].type == LIGHT_SPOT)
        {
            totalLight += CalcSpot(uLights[i], normal, vFragPos, viewDir);
        }
    }

    float rimCosTheta = max(dot(viewDir, normal), 0.0);
    float rimFresnel  = FresnelSchlick(rimCosTheta, 0.04);

    float sceneLuminance = dot(totalLight, vec3(0.2126, 0.7152, 0.0722));
    float rimGate = clamp(sceneLuminance * 4.0, 0.0, 1.0);

    vec3 rimColor = vec3(uRimIntensity) * rimFresnel * rimGate;

    vec3 sunGlare = vec3(0.0);
    if (hasSun)
    {
        float sunAlignment = max(dot(viewDir, -normalize(sunDir)), 0.0);
        float glareShape    = pow(sunAlignment, 256.0);
        sunGlare = sunColor * sunIntensity * glareShape * 0.5;
    }

    // Multiply texture color with uBaseColor (if uTexture isn't bound, it will sample white by default, leaving uBaseColor intact)
    vec4 texColor = texture(uTexture, vTexCoord) * uBaseColor;
    vec3 hdrColor = texColor.rgb * totalLight + rimColor + sunGlare;

    vec3 mapped = ApplyFog(hdrColor, vFragPos, viewPos, viewDir);

    mapped = (uTonemapMode == 1) ? Uncharted2Tonemap(mapped) : ACESFilm(mapped);
    mapped = pow(mapped, vec3(1.0 / 2.2));

    {
        const vec3  gradeLift = vec3(0.004, 0.004, 0.006);
        const vec3  gradeGain = vec3(1.03, 1.0, 0.97);
        const float gradeSaturation = 0.95;

        mapped = mapped * gradeGain + gradeLift;

        float gradeLuma = dot(mapped, vec3(0.2126, 0.7152, 0.0722));
        mapped = mix(vec3(gradeLuma), mapped, gradeSaturation);
    }

    float noiseR = InterleavedGradientNoise(gl_FragCoord.xy);
    float noiseG = InterleavedGradientNoise(gl_FragCoord.xy + vec2(17.0, 5.0));
    float noiseB = InterleavedGradientNoise(gl_FragCoord.xy + vec2(5.0, 17.0));
    vec3 noise = vec3(noiseR, noiseG, noiseB);
    mapped += (noise - 0.5) / 128.0;

    FragColor = vec4(mapped, texColor.a);
}