#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float3;

#include "runtime/native_string.hpp"
#include "float3.hpp"
#include "runtime/native_exceptions.hpp"
#include "float3.hpp"

class float4
{
public:
    float4();

    static ::float4 get_Identity();

    float W;

    float X;

    float Y;

    float Z;

    static void Concatenate(::float4& value1, ::float4& value2, ::float4& result);

    bool Contains(float x, float y);

    static void CreateFromAxisAngle(::float3& axis, float angle, ::float4& result);

    static void CreateFromYawPitchRoll(float yaw, float pitch, float roll, ::float4& result);

    static ::float4 Inverse(::float4 value);

    void Normalize();

    static ::float3 RotateVector(::float3 value, ::float4 rotation);

    std::string ToString();

    float4(float x, float y, float z, float w);

    friend ::float4 operator*(::float4 quaternion1, ::float4 quaternion2);
private:
    static ::float4 identity;
};
