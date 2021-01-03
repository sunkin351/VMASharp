#version 450
#extension GL_ARB_separate_shader_objects : enable
//#extension GL_EXT_scalar_block_layout : enable

layout(binding=0, set=0) uniform Uniforms
{
    //Matrices generated in C#'s Matrix4x4 are row_major, Transpose them to column_major for speed
    layout(row_major) mat4 MVP;
    layout(row_major) mat4 Model;
};

//Vertex Attributes
layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;

//Instance attributes
layout(location = 2) in vec3 instancePosition;

layout(location = 0) out vec3 fragColor;

void main()
{
    gl_Position = vec4(position + instancePosition, 1) * Model;

    gl_Position = gl_Position * MVP;

    fragColor = color;
}
