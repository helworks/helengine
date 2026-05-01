#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float2;

#include "runtime/native_equatable.hpp"
#include "runtime/native_string.hpp"
#include "float2.hpp"

class float3 : public IEquatable<::float3>
{
public:
    float3();

    static ::float3 get_One();

    static ::float3 get_Zero();

    float X;

    float Y;

    float Z;

    static void Cross(::float3& vector1, ::float3& vector2, ::float3& result);

    static ::float3 Cross(::float3 vector1, ::float3 vector2);

    static void Dot(::float3& value1, ::float3& value2, float& result);

    static float Dot(::float3 value1, ::float3 value2);

    bool Equals(void* obj);

    bool Equals(::float3 other);

    int32_t GetHashCode();

    float3(float x, float y, float z);

    float3(float value);

    float3(::float2 value, float z);

    static ::float3 Normalize(::float3 value);

    std::string ToString();

    friend bool operator!=(::float3 a, ::float3 b);

    friend ::float3 operator*(::float3 a, ::float3 b);

    friend ::float3 operator*(::float3 a, float scalar);

    friend ::float3 operator+(::float3 a, ::float3 b);

    friend ::float3 operator-(::float3 a, ::float3 b);

    friend ::float3 operator/(::float3 a, float scalar);

    friend ::float3 operator/(::float3 a, ::float3 b);

    friend bool operator==(::float3 a, ::float3 b);
private:
    static ::float3 one;

    static ::float3 zero;
};
