#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float4;
class float4x4;

#include "float4.hpp"
#include "float4x4.hpp"

class StandardMeshShaderData
{
public:
    virtual ~StandardMeshShaderData() = default;

    StandardMeshShaderData();

    ::float4 CameraPosition;

    ::float4x4 NormalMatrix;

    ::float4x4 World;

    ::float4x4 WorldViewProj;
};
