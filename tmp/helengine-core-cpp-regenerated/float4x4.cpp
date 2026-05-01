#ifdef DrawText
#undef DrawText
#endif
#include "float4x4.hpp"
#include "float3.hpp"
#include "runtime/native_exceptions.hpp"
#include "float4x4.hpp"
#include "float4.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/directory.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

float4x4::float4x4() : M11(), M12(), M13(), M14(), M21(), M22(), M23(), M24(), M31(), M32(), M33(), M34(), M41(), M42(), M43(), M44()
{
}

::float4x4 float4x4::get_Identity()
{
return identity;}

void float4x4::CreateFromQuaternion(::float4& quaternion, ::float4x4& result)
{
const float num9 = quaternion.X * quaternion.X;
const float num8 = quaternion.Y * quaternion.Y;
const float num7 = quaternion.Z * quaternion.Z;
const float num6 = quaternion.X * quaternion.Y;
const float num5 = quaternion.Z * quaternion.W;
const float num4 = quaternion.Z * quaternion.X;
const float num3 = quaternion.Y * quaternion.W;
const float num2 = quaternion.Y * quaternion.Z;
const float num = quaternion.X * quaternion.W;
result.M11 = 1.0f - (2.0f * (num8 + num7));
result.M12 = 2.0f * (num6 + num5);
result.M13 = 2.0f * (num4 - num3);
result.M14 = 0.0f;
result.M21 = 2.0f * (num6 - num5);
result.M22 = 1.0f - (2.0f * (num7 + num9));
result.M23 = 2.0f * (num2 + num);
result.M24 = 0.0f;
result.M31 = 2.0f * (num4 + num3);
result.M32 = 2.0f * (num2 - num);
result.M33 = 1.0f - (2.0f * (num8 + num9));
result.M34 = 0.0f;
result.M41 = 0.0f;
result.M42 = 0.0f;
result.M43 = 0.0f;
result.M44 = 1.0f;
}

void float4x4::CreateFromYawPitchRoll(float yaw, float pitch, float roll, ::float4x4& result)
{
::float4 quaternion;
float4::CreateFromYawPitchRoll(yaw, pitch, roll, quaternion);
CreateFromQuaternion(quaternion, result);
}

void float4x4::CreateLookAt(::float3& cameraPosition, ::float3& cameraTarget, ::float3& cameraUpVector, ::float4x4& result)
{
::float3 vector = float3::Normalize(cameraPosition - cameraTarget);
::float3 vector2 = float3::Normalize(float3::Cross(cameraUpVector, vector));
::float3 vector3 = float3::Cross(vector, vector2);
result.M11 = vector2.X;
result.M12 = vector3.X;
result.M13 = vector.X;
result.M14 = 0.0f;
result.M21 = vector2.Y;
result.M22 = vector3.Y;
result.M23 = vector.Y;
result.M24 = 0.0f;
result.M31 = vector2.Z;
result.M32 = vector3.Z;
result.M33 = vector.Z;
result.M34 = 0.0f;
result.M41 = -float3::Dot(vector2, cameraPosition);
result.M42 = -float3::Dot(vector3, cameraPosition);
result.M43 = -float3::Dot(vector, cameraPosition);
result.M44 = 1.0f;
}

void float4x4::CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNearPlane, float zFarPlane, ::float4x4& result)
{
result.M11 = static_cast<float>((2.0 / (static_cast<double>(right) - static_cast<double>(left))));
result.M12 = 0.0f;
result.M13 = 0.0f;
result.M14 = 0.0f;
result.M21 = 0.0f;
result.M22 = static_cast<float>((2.0 / (static_cast<double>(top) - static_cast<double>(bottom))));
result.M23 = 0.0f;
result.M24 = 0.0f;
result.M31 = 0.0f;
result.M32 = 0.0f;
result.M33 = static_cast<float>((1.0 / (static_cast<double>(zNearPlane) - static_cast<double>(zFarPlane))));
result.M34 = 0.0f;
result.M41 = static_cast<float>(((static_cast<double>(left) + static_cast<double>(right)) / (static_cast<double>(left) - static_cast<double>(right))));
result.M42 = static_cast<float>(((static_cast<double>(top) + static_cast<double>(bottom)) / (static_cast<double>(bottom) - static_cast<double>(top))));
result.M43 = static_cast<float>((static_cast<double>(zNearPlane) / (static_cast<double>(zNearPlane) - static_cast<double>(zFarPlane))));
result.M44 = 1.0f;
}

void float4x4::CreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance, ::float4x4& result)
{
    if ((fieldOfView <= 0.0f) || (fieldOfView >= 3.141593f))
    {
throw new ArgumentException("fieldOfView <= 0 or >= PI");
    }
    if (nearPlaneDistance <= 0.0f)
    {
throw new ArgumentException("nearPlaneDistance <= 0");
    }
    if (farPlaneDistance <= 0.0f)
    {
throw new ArgumentException("farPlaneDistance <= 0");
    }
    if (nearPlaneDistance >= farPlaneDistance)
    {
throw new ArgumentException("nearPlaneDistance >= farPlaneDistance");
    }
float yScale = 1.0f / static_cast<float>(Math::Tan(static_cast<double>(fieldOfView) * 0.5f));
float xScale = yScale / aspectRatio;
float negFarRange = Number::IsPositiveInfinity(farPlaneDistance) ? -1.0f : farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
result.M11 = xScale;
result.M12 = result.M13 = result.M14 = 0.0f;
result.M22 = yScale;
result.M21 = result.M23 = result.M24 = 0.0f;
result.M31 = result.M32 = 0.0f;
result.M33 = negFarRange;
result.M34 = -1.0f;
result.M41 = result.M42 = result.M44 = 0.0f;
result.M43 = nearPlaneDistance * negFarRange;
}

void float4x4::CreateScale(float scale, ::float4x4& result)
{
CreateScale(scale, scale, scale, result);
}

void float4x4::CreateScale(float xScale, float yScale, float zScale, ::float4x4& result)
{
result.M11 = xScale;
result.M12 = 0;
result.M13 = 0;
result.M14 = 0;
result.M21 = 0;
result.M22 = yScale;
result.M23 = 0;
result.M24 = 0;
result.M31 = 0;
result.M32 = 0;
result.M33 = zScale;
result.M34 = 0;
result.M41 = 0;
result.M42 = 0;
result.M43 = 0;
result.M44 = 1;
}

void float4x4::CreateTranslation(float x, float y, float z, ::float4x4& result)
{
result.M11 = 1;
result.M12 = 0;
result.M13 = 0;
result.M14 = 0;
result.M21 = 0;
result.M22 = 1;
result.M23 = 0;
result.M24 = 0;
result.M31 = 0;
result.M32 = 0;
result.M33 = 1;
result.M34 = 0;
result.M41 = x;
result.M42 = y;
result.M43 = z;
result.M44 = 1;
}

void float4x4::CreateTranslation(::float3& position, ::float4x4& result)
{
result.M11 = 1;
result.M12 = 0;
result.M13 = 0;
result.M14 = 0;
result.M21 = 0;
result.M22 = 1;
result.M23 = 0;
result.M24 = 0;
result.M31 = 0;
result.M32 = 0;
result.M33 = 1;
result.M34 = 0;
result.M41 = position.X;
result.M42 = position.Y;
result.M43 = position.Z;
result.M44 = 1;
}

void float4x4::Multiply(::float4x4& matrix1, ::float4x4& matrix2, ::float4x4& result)
{
float m11 = (((matrix1.M11 * matrix2.M11) + (matrix1.M12 * matrix2.M21)) + (matrix1.M13 * matrix2.M31)) + (matrix1.M14 * matrix2.M41);
float m12 = (((matrix1.M11 * matrix2.M12) + (matrix1.M12 * matrix2.M22)) + (matrix1.M13 * matrix2.M32)) + (matrix1.M14 * matrix2.M42);
float m13 = (((matrix1.M11 * matrix2.M13) + (matrix1.M12 * matrix2.M23)) + (matrix1.M13 * matrix2.M33)) + (matrix1.M14 * matrix2.M43);
float m14 = (((matrix1.M11 * matrix2.M14) + (matrix1.M12 * matrix2.M24)) + (matrix1.M13 * matrix2.M34)) + (matrix1.M14 * matrix2.M44);
float m21 = (((matrix1.M21 * matrix2.M11) + (matrix1.M22 * matrix2.M21)) + (matrix1.M23 * matrix2.M31)) + (matrix1.M24 * matrix2.M41);
float m22 = (((matrix1.M21 * matrix2.M12) + (matrix1.M22 * matrix2.M22)) + (matrix1.M23 * matrix2.M32)) + (matrix1.M24 * matrix2.M42);
float m23 = (((matrix1.M21 * matrix2.M13) + (matrix1.M22 * matrix2.M23)) + (matrix1.M23 * matrix2.M33)) + (matrix1.M24 * matrix2.M43);
float m24 = (((matrix1.M21 * matrix2.M14) + (matrix1.M22 * matrix2.M24)) + (matrix1.M23 * matrix2.M34)) + (matrix1.M24 * matrix2.M44);
float m31 = (((matrix1.M31 * matrix2.M11) + (matrix1.M32 * matrix2.M21)) + (matrix1.M33 * matrix2.M31)) + (matrix1.M34 * matrix2.M41);
float m32 = (((matrix1.M31 * matrix2.M12) + (matrix1.M32 * matrix2.M22)) + (matrix1.M33 * matrix2.M32)) + (matrix1.M34 * matrix2.M42);
float m33 = (((matrix1.M31 * matrix2.M13) + (matrix1.M32 * matrix2.M23)) + (matrix1.M33 * matrix2.M33)) + (matrix1.M34 * matrix2.M43);
float m34 = (((matrix1.M31 * matrix2.M14) + (matrix1.M32 * matrix2.M24)) + (matrix1.M33 * matrix2.M34)) + (matrix1.M34 * matrix2.M44);
float m41 = (((matrix1.M41 * matrix2.M11) + (matrix1.M42 * matrix2.M21)) + (matrix1.M43 * matrix2.M31)) + (matrix1.M44 * matrix2.M41);
float m42 = (((matrix1.M41 * matrix2.M12) + (matrix1.M42 * matrix2.M22)) + (matrix1.M43 * matrix2.M32)) + (matrix1.M44 * matrix2.M42);
float m43 = (((matrix1.M41 * matrix2.M13) + (matrix1.M42 * matrix2.M23)) + (matrix1.M43 * matrix2.M33)) + (matrix1.M44 * matrix2.M43);
float m44 = (((matrix1.M41 * matrix2.M14) + (matrix1.M42 * matrix2.M24)) + (matrix1.M43 * matrix2.M34)) + (matrix1.M44 * matrix2.M44);
result.M11 = m11;
result.M12 = m12;
result.M13 = m13;
result.M14 = m14;
result.M21 = m21;
result.M22 = m22;
result.M23 = m23;
result.M24 = m24;
result.M31 = m31;
result.M32 = m32;
result.M33 = m33;
result.M34 = m34;
result.M41 = m41;
result.M42 = m42;
result.M43 = m43;
result.M44 = m44;
}

void float4x4::Transpose(::float4x4& matrix, ::float4x4& result)
{
::float4x4 ret;
ret.M11 = matrix.M11;
ret.M12 = matrix.M21;
ret.M13 = matrix.M31;
ret.M14 = matrix.M41;
ret.M21 = matrix.M12;
ret.M22 = matrix.M22;
ret.M23 = matrix.M32;
ret.M24 = matrix.M42;
ret.M31 = matrix.M13;
ret.M32 = matrix.M23;
ret.M33 = matrix.M33;
ret.M34 = matrix.M43;
ret.M41 = matrix.M14;
ret.M42 = matrix.M24;
ret.M43 = matrix.M34;
ret.M44 = matrix.M44;
result = ret;
}

float4x4::float4x4(float m11, float m12, float m13, float m14, float m21, float m22, float m23, float m24, float m31, float m32, float m33, float m34, float m41, float m42, float m43, float m44) : M11(), M12(), M13(), M14(), M21(), M22(), M23(), M24(), M31(), M32(), M33(), M34(), M41(), M42(), M43(), M44()
{
(*this).M11 = m11;
(*this).M12 = m12;
(*this).M13 = m13;
(*this).M14 = m14;
(*this).M21 = m21;
(*this).M22 = m22;
(*this).M23 = m23;
(*this).M24 = m24;
(*this).M31 = m31;
(*this).M32 = m32;
(*this).M33 = m33;
(*this).M34 = m34;
(*this).M41 = m41;
(*this).M42 = m42;
(*this).M43 = m43;
(*this).M44 = m44;
}

::float4x4 float4x4::identity = ::float4x4(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);

