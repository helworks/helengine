#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float3;

#include "float3.hpp"
#include "float3.hpp"

class GeometryUtils
{
public:
    virtual ~GeometryUtils() = default;

    static bool IsPointInsideRect(double x, double y, ::float3 origin, int32_t width, int32_t height);
};
