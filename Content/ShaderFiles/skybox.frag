#version 330 core
in vec3 vTexCoord;
out vec4 FragColor;
uniform samplerCube uSkybox;

// --- Fog color constants, duplicated from ApplyFog() in lit.frag ---
// SOURCE OF TRUTH: lit.frag, ApplyFog() function, fogColorSky/fogColorGround.
// These three values must be hand-kept in sync with that copy. This
// duplication exists because there's currently no shared uniform for fog
// color between the two shaders — if you add one later (uFogColorSky /
// uFogColorGround set from one place in C#), delete this block and the
// horizon-fog section below should read from the uniforms instead.
const vec3 kFogColorSky    = vec3(0.55, 0.65, 0.85);
const vec3 kFogColorGround = vec3(0.78, 0.72, 0.60);
const float kHorizonFogStart = -0.05; // vTexCoord.y where fog starts blending in
const float kHorizonFogEnd   =  0.15; // vTexCoord.y where fog is fully gone (clear sky)

// Cheap hash for the star field. Not a texture lookup — pure function of
// direction, same "cost nothing, just math" spirit as InterleavedGradientNoise
// in lit.frag, but this one only needs to run once per pixel here (no dither
// pass), so a simpler single-hash is enough; no need to import the exact
// same magic constants.
float Hash(vec3 p)
{
    p = fract(p * vec3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return fract((p.x + p.y) * p.z);
}

// Stars as a sparse, thresholded hash field: sample a grid of cells along
// the view direction, and light up a pixel only if that cell's hash exceeds
// a high threshold — gives sparse points instead of continuous noise.
// starDensity controls how rare stars are (higher = fewer, dimmer overall);
// this is a look-dev constant, tune by eye.
float CalcStars(vec3 dir)
{
    const float cellSize    = 180.0; // higher = smaller/denser-looking star grid
    const float starDensity = 0.996; // threshold; raise toward 1.0 for fewer stars

    vec3 cell = floor(dir * cellSize);
    float h = Hash(cell);

    float star = step(starDensity, h);

    // Vary brightness per star instead of a flat on/off, using a second
    // hash so brightness doesn't correlate with the presence threshold.
    float brightness = Hash(cell + 7.0);
    return star * brightness;
}

void main()
{
    vec3 dir = normalize(vTexCoord);
    vec3 color = texture(uSkybox, dir).rgb;

    // Vertical gradient tint: subtle brightening toward the zenith (straight
    // up) and darkening toward nadir (straight down), independent of what's
    // actually baked into the cubemap texture. Same "cheap proxy without a
    // texture" spirit as the hemisphere ambient added in lit.frag. Kept
    // subtle (±8%) so it nudges a flat-looking bake rather than visibly
    // recoloring it.
    float verticalTint = dir.y * 0.08;
    color *= (1.0 + verticalTint);

    // Procedural stars: only meant to show where the sky is already dark,
    // so gate by the sky's own luminance — bright daytime sky or a sunlit
    // texture region won't show stars poking through. This uses the actual
    // sampled color's brightness rather than a separate day/night uniform,
    // since none exists yet in this shader (see CalcStars comment above and
    // the day/night cubemap blend option noted last time for the proper
    // long-term fix).
    float skyLuminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    float starVisibility = 1.0 - smoothstep(0.0, 0.15, skyLuminance);
    float stars = CalcStars(dir) * starVisibility;
    color += vec3(stars);

    // Horizon fog blend: ties the skybox into the same ground-fog look
    // lit.frag applies to scene geometry, so the horizon line doesn't read
    // as a hard seam between "foggy ground" and "crisp sky" on hazy scenes.
    // Blend factor is purely a function of look angle (dir.y near 0 = looking
    // at the horizon), not distance or density — the skybox has no notion of
    // distance to begin with, so this only approximates the *color* match,
    // not the actual fog density falloff lit.frag computes per-fragment.
    float horizonBlend = 1.0 - smoothstep(kHorizonFogStart, kHorizonFogEnd, dir.y);
    vec3 fogColor = mix(kFogColorGround, kFogColorSky, clamp(dir.y * 0.5 + 0.5, 0.0, 1.0));
    color = mix(color, fogColor, horizonBlend * 0.6); // 0.6 caps max blend so sky doesn't fully disappear into fog color

    FragColor = vec4(color, 1.0);
}
