#version 330 core
layout (location = 0) in vec3 aPosition;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vTexCoord;
void main()
{
    vTexCoord   = aPosition; // use LOCAL position, not world position
    mat4 rotView = mat4(mat3(uView)); // discard translation, keep rotation
    vec4 pos = uProjection * rotView * vec4(aPosition, 1.0);
    gl_Position = pos.xyww;
}
