#version 330 core
in vec2 vUV;
out vec4 FragColor;

uniform sampler2D uScreen;
uniform float uGamma;
uniform float uExposure;
uniform float uSaturation;   // 0=grayscale  1=normal  2+=boosted
uniform int   uInvert;
uniform float uVignette;     // 0=off  0.5=subtle  1=strong
uniform float uChroma;       // 0=off  0.003=subtle  0.01=strong
uniform vec3  uColorTint;    // (1,1,1)=neutral  (1,0.8,0.8)=warm  (0.8,0.8,1)=cool

vec3 ACESFilm(vec3 x) {
    float a=2.51, b=0.03, c=2.43, d=0.59, e=0.14;
    return clamp((x*(a*x+b))/(x*(c*x+d)+e), 0.0, 1.0);
}

void main() {
    vec3 color;

    // Chromatic aberration — strength driven by uChroma value
    if (uChroma > 0.0) {
        vec2 dir = vUV - 0.5;
        color.r = texture(uScreen, vUV + dir * uChroma * 1.0).r;
        color.g = texture(uScreen, vUV + dir * uChroma * 0.5).g;
        color.b = texture(uScreen, vUV - dir * uChroma * 1.0).b;
    } else {
        color = texture(uScreen, vUV).rgb;
    }

    // Exposure + ACES tonemap
    color *= uExposure;
    color  = ACESFilm(color);

    // Saturation — lerp between luminance (gray) and full color
    float lum = dot(color, vec3(0.299, 0.587, 0.114));
    color = mix(vec3(lum), color, uSaturation);

    // Color tint
    color *= uColorTint;

    // Invert
    if (uInvert == 1)
        color = 1.0 - color;

    // Vignette
    if (uVignette > 0.0) {
        vec2 uv = vUV - 0.5;
        color *= clamp(1.0 - dot(uv, uv) * uVignette * 4.0, 0.0, 1.0);
    }

    // Gamma correction
    color = pow(max(color, 0.0), vec3(1.0 / uGamma));

    FragColor = vec4(color, 1.0);
}