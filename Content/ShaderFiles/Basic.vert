#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;
layout (location = 3) in vec4 aBoneIndices;
layout (location = 4) in vec4 aBoneWeights;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform mat4 uBones[100];
uniform int  uSkinned;

out vec3 vNormal;
out vec2 vTexCoord;
out vec3 vFragPos;

void main()
{
    mat4 skinMatrix;
    if (uSkinned == 1)
    {
        skinMatrix =
            uBones[int(aBoneIndices.x)] * aBoneWeights.x +
            uBones[int(aBoneIndices.y)] * aBoneWeights.y +
            uBones[int(aBoneIndices.z)] * aBoneWeights.z +
            uBones[int(aBoneIndices.w)] * aBoneWeights.w;
    }
    else
    {
        skinMatrix = mat4(1.0);
    }

    vec4 skinnedPos    = skinMatrix * vec4(aPosition, 1.0);
    vec3 skinnedNormal = mat3(skinMatrix) * aNormal;

    vFragPos    = vec3(uModel * skinnedPos);
    vNormal     = mat3(transpose(inverse(uModel))) * skinnedNormal;
    vTexCoord   = aTexCoord;
    gl_Position = uProjection * uView * uModel * skinnedPos;
}