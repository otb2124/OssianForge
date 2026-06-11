#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform vec4 uTextColor;
uniform float uDistanceRange;

float median(float r, float g, float b)
{
    return max(min(r, g), min(max(r, g), b));
}

void main()
{
    vec3 msd = texture(uTexture, vTexCoord).rgb;
    float dist = median(msd.r, msd.g, msd.b);

    vec2 unitRange = vec2(uDistanceRange) / vec2(textureSize(uTexture, 0));
    float screenPxRange = max(0.5 * dot(unitRange, vec2(1.0)), 1.0);

    float alpha = smoothstep(0.5 - (0.5 / screenPxRange), 0.5 + (0.5 / screenPxRange), dist);
    FragColor = vec4(uTextColor.rgb, uTextColor.a * alpha);
}