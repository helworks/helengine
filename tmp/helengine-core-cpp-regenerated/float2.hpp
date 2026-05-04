#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float3;

#include "float3.hpp"

class float2
{
public:
    virtual ~float2() = default;

    float2();

    float X;

    float Y;

    bool Equals(void* obj);

    bool Equals(::float3 other);

    int32_t GetHashCode();

    float2(float x, float y);

    float2(float value);

    friend bool operator!=(::float2 a, ::float2 b);

    friend ::float2 operator*(::float2 a, ::float2 b);

    friend ::float2 operator*(::float2 a, float scalar);

    friend ::float2 operator/(::float2 a, ::float2 b);

    friend ::float2 operator/(::float2 a, float scalar);

    friend bool operator==(::float2 a, ::float2 b);
};
