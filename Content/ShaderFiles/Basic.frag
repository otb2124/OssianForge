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
uniform float uWrapLighting; // 0.0 = hard diffuse falloff (default, matches old behavior), 1.0 = full wrap. Set per-material from C#; keep at 0.0 for hard surfaces, try 0.3-0.6 for skin/cloth/foliage.
uniform int   uTonemapMode;  // 0 = ACES (default, matches old behavior), 1 = Uncharted2 filmic. Swap to compare highlight rolloff.
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

// Hemisphere ambient: lerps between a "sky" color (facing up) and a
// "ground" color (facing down) based on normal.y, instead of one flat
// ambient constant for every surface orientation. Upward-facing surfaces
// pick up sky-tint bounce light, downward-facing surfaces pick up
// ground-tint bounce light — cheap proxy for indirect lighting with no
// extra texture samples.
//
// Average brightness matches the old flat vec3(0.02) so this is a shape
// change, not a global exposure change.
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

// Combines diffuse + specular the energy-conserving way: whatever fraction
// of light the fresnel term reflects specularly is subtracted from the
// diffuse term first, instead of just adding diff+spec unconditionally.
// At grazing angles (high fresnel) this pulls diffuse down as spec goes up,
// so the surface reads as "goes mirror-like" rather than "gets extra bright
// in both terms at once."
//
// shininess is passed through per-call so this stays usable for a future
// roughness map (lower shininess = wider, dimmer highlight) without needing
// a second function.
float CalcDiffuseSpec(vec3 normal, vec3 lightDir, vec3 viewDir, float shininess)
{
    // Specular anti-aliasing: widen (lower) the effective shininess where
    // the normal is changing fast across screen-space pixels — i.e. tight
    // curvature or a noisy normal map viewed from far away. Without this,
    // a sub-pixel-width highlight flickers/sparkles as it moves less than
    // one pixel per frame, because the pow(.., shininess) term is being
    // point-sampled at a frequency the screen can't resolve.
    //
    // fwidth(normal) approximates how much the normal varies over one
    // pixel; scaling shininess down in proportion trades a bit of highlight
    // sharpness for stability. Only kicks in where derivatives are large,
    // so flat/smooth surfaces keep the full sharp highlight.
    float normalVariation  = length(fwidth(normal));
    float shininessAA       = shininess / (1.0 + shininess * normalVariation * 4.0);

    float fresnel;
    float spec = CalcSpecular(normal, lightDir, viewDir, shininessAA, fresnel);

    // Wrap lighting: blends between the standard hard diffuse falloff
    // (max(dot,0), terminates sharply at the normal's horizon) and a
    // wrapped version (dot*0.5+0.5, never fully zero) based on uWrapLighting.
    // At uWrapLighting = 0.0 this is bit-for-bit the old hard falloff. Only
    // turn this up per-material for things light should appear to pass
    // through/around a little: skin, cloth, thin foliage. Hard surfaces
    // (metal, stone, plastic) should stay at 0.0, since wrap lighting on a
    // hard edge looks like it's glowing from behind rather than catching
    // light correctly.
    float dotNL       = dot(normal, lightDir);
    float dotHard     = max(dotNL, 0.0);
    float dotWrapped  = dotNL * 0.5 + 0.5;
    float diff = mix(dotHard, dotWrapped, uWrapLighting) * (1.0 - fresnel);
    return diff + spec;
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

// Uncharted2 filmic curve (Hable). Rolls off highlights more gradually
// than ACES — ACES tends to crush bright specular/emissive content faster,
// so this is here as an A/B alternative for scenes with a lot of bright
// content, not a replacement. Requires dividing by the curve's own
// response to a fixed white point (11.2) to normalize output range — that
// normalization is folded in here so this function is drop-in comparable
// to ACESFilm(), not a partial implementation that needs an extra step
// wherever it's called.
vec3 Uncharted2TonemapCurve(vec3 x)
{
    float A = 0.15; // shoulder strength
    float B = 0.50; // linear strength
    float C = 0.10; // linear angle
    float D = 0.20; // toe strength
    float E = 0.02; // toe numerator
    float F = 0.30; // toe denominator
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
    const vec3  fogColorSky    = vec3(0.55, 0.65, 0.85); // fog tint looking toward horizon/sky
    const vec3  fogColorGround = vec3(0.78, 0.72, 0.60); // original flat tint, now the "looking down" end
    const float groundHeight  = 7.86;
    const float heightFalloff = 0.05;

    float dist    = length(fragPos - viewPos);
    float distFog = 1.0 - exp2(-density * density * dist * dist);

    float heightAboveGround = max(fragPos.y - groundHeight, 0.0);
    float heightFog = exp2(-heightFalloff * heightAboveGround);

    float fogAmt = clamp(distFog * heightFog, 0.0, 1.0);

    // Sky-gradient fog color: instead of one flat tint regardless of look
    // direction, blend toward a cooler/bluer tint when looking up toward
    // the horizon/sky and the original warm tint when looking down. Uses
    // the same "normal.y-style" up-vector remap already used for hemisphere
    // ambient, applied to viewDir instead of the surface normal — sells
    // atmospheric perspective (things toward the horizon look hazier/bluer)
    // instead of every fogged pixel getting the identical color.
    float skyBlend = clamp(viewDir.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 fogColor = mix(fogColorGround, fogColorSky, skyBlend);

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

    vec3 totalLight = CalcHemisphereAmbient(normal);

    // Tracked during the loop below so the sun-glare haze term after it
    // doesn't need a second pass over uLights[]. If multiple LIGHT_SUN
    // entries exist, the last one in the array wins — same "one sun"
    // assumption CalcSun already makes per-light, just noting it here
    // since this is the first place that assumption becomes visible
    // outside a single light's own calculation.
    bool  hasSun      = false;
    vec3  sunDir      = vec3(0.0);
    vec3  sunColor    = vec3(0.0);
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

    // Sun-glare haze: a tight, high-power cone around the anti-sun-direction
    // in view space, scaled by sun color/intensity. This is not a real
    // volumetric scattering simulation — no shadow test, no ray march —
    // it's a fake "looking near the sun through haze" glow. It reads
    // correctly for sun near screen center but will glow through solid
    // geometry the same as the existing rim term does, since neither has
    // occlusion information available in this shader.
    vec3 sunGlare = vec3(0.0);
    if (hasSun)
    {
        float sunAlignment = max(dot(viewDir, -normalize(sunDir)), 0.0);
        float glareShape    = pow(sunAlignment, 256.0);
        sunGlare = sunColor * sunIntensity * glareShape * 0.5;
    }

    vec4 texColor = texture(uTexture, vTexCoord);
    vec3 hdrColor = texColor.rgb * totalLight + rimColor + sunGlare;

    // Fog moved before tonemap + gamma. Fog is a linear-space phenomenon
    // (light scattering in atmosphere happens before any display-referred
    // encoding), and fogColor above is specified as a linear constant, so
    // blending it against an already-gamma-encoded color was mixing spaces.
    vec3 mapped = ApplyFog(hdrColor, vFragPos, viewPos, viewDir);

    // uTonemapMode: 0 = ACES (default, matches old behavior), 1 = Uncharted2.
    // Kept as a runtime branch rather than two separate shader variants so
    // you can A/B without recompiling — set the uniform from C# and compare.
    mapped = (uTonemapMode == 1) ? Uncharted2Tonemap(mapped) : ACESFilm(mapped);
    mapped = pow(mapped, vec3(1.0 / 2.2));

    // Post-tonemap color grade: lift/gain plus a saturation adjustment via
    // luminance lerp. These are a mild starting-point nudge (slightly warm
    // gain, tiny shadow lift, very slightly desaturated) — not a finished
    // look, just no longer the exact-neutral placeholder from before. Treat
    // these three lines as the ones to keep tweaking by eye.
    {
        const vec3  gradeLift = vec3(0.004, 0.004, 0.006);
        const vec3  gradeGain = vec3(1.03, 1.0, 0.97);
        const float gradeSaturation = 0.95;

        mapped = mapped * gradeGain + gradeLift;

        float gradeLuma = dot(mapped, vec3(0.2126, 0.7152, 0.0722));
        mapped = mix(vec3(gradeLuma), mapped, gradeSaturation);
    }

    // Per-channel dither: sampling the noise function at three slightly
    // offset coordinates instead of once shared across R/G/B. A single
    // shared dither offset moves all three channels together, which still
    // breaks up banding in luminance but does nothing extra for banding in
    // saturated color gradients (where R, G, B are quantizing at different
    // rates). Small per-channel offsets decorrelate the three channels'
    // dither pattern from each other.
    float noiseR = InterleavedGradientNoise(gl_FragCoord.xy);
    float noiseG = InterleavedGradientNoise(gl_FragCoord.xy + vec2(17.0, 5.0));
    float noiseB = InterleavedGradientNoise(gl_FragCoord.xy + vec2(5.0, 17.0));
    vec3 noise = vec3(noiseR, noiseG, noiseB);
    mapped += (noise - 0.5) / 128.0;

    FragColor = vec4(mapped, texColor.a);
}