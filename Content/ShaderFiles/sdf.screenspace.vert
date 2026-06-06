#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uWorldPosition;
uniform float uScale;

out vec2 vTexCoord;

void main()
{
    vec3 right = vec3(uView[0][0], uView[1][0], uView[2][0]);
    vec3 up    = vec3(uView[0][1], uView[1][1], uView[2][1]);

    vec3 worldPos = uWorldPosition
        + right * aPosition.x * uScale
        + up    * aPosition.y * uScale;

    vTexCoord   = aTexCoord;
    gl_Position = uProjection * uView * vec4(worldPos, 1.0);
}