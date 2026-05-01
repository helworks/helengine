#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class float3;
class float4;

#include "float3.hpp"
#include "float3.hpp"
#include "runtime/native_exceptions.hpp"
#include "float4.hpp"
#include "float4.hpp"

class float4x4
{
public:
    float4x4();

    static ::float4x4 get_Identity();

    float M11;

    float M12;

    float M13;

    float M14;

    float M21;

    float M22;

    float M23;

    float M24;

    float M31;

    float M32;

    float M33;

    float M34;

    float M41;

    float M42;

    float M43;

    float M44;

    static void CreateFromQuaternion(::float4& quaternion, ::float4x4& result);

    static void CreateFromYawPitchRoll(float yaw, float pitch, float roll, ::float4x4& result);

    static void CreateLookAt(::float3& cameraPosition, ::float3& cameraTarget, ::float3& cameraUpVector, ::float4x4& result);

    static void CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNearPlane, float zFarPlane, ::float4x4& result);

    static void CreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance, ::float4x4& result);

    static void CreateScale(float scale, ::float4x4& result);

    static void CreateScale(float xScale, float yScale, float zScale, ::float4x4& result);

    static void CreateTranslation(float x, float y, float z, ::float4x4& result);

    static void CreateTranslation(::float3& position, ::float4x4& result);

    static void Multiply(::float4x4& matrix1, ::float4x4& matrix2, ::float4x4& result);

    static void Transpose(::float4x4& matrix, ::float4x4& result);

    float4x4(float m11, float m12, float m13, float m14, float m21, float m22, float m23, float m24, float m31, float m32, float m33, float m34, float m41, float m42, float m43, float m44);
private:
    static ::float4x4 identity;
};
