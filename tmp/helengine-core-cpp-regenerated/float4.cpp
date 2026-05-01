#ifdef DrawText
#undef DrawText
#endif
#include "float4.hpp"
#include "runtime/native_string.hpp"
#include "float4.hpp"
#include "float3.hpp"
#include "runtime/native_exceptions.hpp"
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

float4::float4() : W(), X(), Y(), Z()
{
}

::float4 float4::get_Identity()
{
return identity;}

void float4::Concatenate(::float4& value1, ::float4& value2, ::float4& result)
{
const float x1 = value1.X;
const float y1 = value1.Y;
const float z1 = value1.Z;
const float w1 = value1.W;
const float x2 = value2.X;
const float y2 = value2.Y;
const float z2 = value2.Z;
const float w2 = value2.W;
result.X = ((x2 * w1) + (x1 * w2)) + ((y2 * z1) - (z2 * y1));
result.Y = ((y2 * w1) + (y1 * w2)) + ((z2 * x1) - (x2 * z1));
result.Z = ((z2 * w1) + (z1 * w2)) + ((x2 * y1) - (y2 * x1));
result.W = (w2 * w1) - (((x2 * x1) + (y2 * y1)) + (z2 * z1));
}

bool float4::Contains(float x, float y)
{
return ((((this->X <= x) && (x < (this->X + this->Z))) && (this->Y <= y)) && (y < (this->Y + this->W)));}

void float4::CreateFromAxisAngle(::float3& axis, float angle, ::float4& result)
{
const float half = angle * 0.5f;
const float sin = static_cast<float>(Math::Sin(half));
const float cos = static_cast<float>(Math::Cos(half));
result.X = axis.X * sin;
result.Y = axis.Y * sin;
result.Z = axis.Z * sin;
result.W = cos;
}

void float4::CreateFromYawPitchRoll(float yaw, float pitch, float roll, ::float4& result)
{
const float halfRoll = roll * 0.5f;
const float halfPitch = pitch * 0.5f;
const float halfYaw = yaw * 0.5f;
const float sinRoll = static_cast<float>(Math::Sin(halfRoll));
const float cosRoll = static_cast<float>(Math::Cos(halfRoll));
const float sinPitch = static_cast<float>(Math::Sin(halfPitch));
const float cosPitch = static_cast<float>(Math::Cos(halfPitch));
const float sinYaw = static_cast<float>(Math::Sin(halfYaw));
const float cosYaw = static_cast<float>(Math::Cos(halfYaw));
result.X = (cosYaw * sinPitch * cosRoll) + (sinYaw * cosPitch * sinRoll);
result.Y = (sinYaw * cosPitch * cosRoll) - (cosYaw * sinPitch * sinRoll);
result.Z = (cosYaw * cosPitch * sinRoll) - (sinYaw * sinPitch * cosRoll);
result.W = (cosYaw * cosPitch * cosRoll) + (sinYaw * sinPitch * sinRoll);
}

::float4 float4::Inverse(::float4 value)
{
const double lengthSquared = (value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z) + (value.W * value.W);
    if (lengthSquared <= 0.0)
    {
throw new InvalidOperationException("Cannot invert a zero-length quaternion.");
    }
const double inverseLengthSquared = 1.0 / lengthSquared;
return ::float4(static_cast<float>((-value.X * inverseLengthSquared)), static_cast<float>((-value.Y * inverseLengthSquared)), static_cast<float>((-value.Z * inverseLengthSquared)), static_cast<float>((value.W * inverseLengthSquared)));}

void float4::Normalize()
{
const float num = 1.0f / static_cast<float>(Math::Sqrt((this->X * this->X) + (this->Y * this->Y) + (this->Z * this->Z) + (this->W * this->W)));
this->X *= num;
this->Y *= num;
this->Z *= num;
this->W *= num;
}

::float3 float4::RotateVector(::float3 value, ::float4 rotation)
{
const double qx = rotation.X;
const double qy = rotation.Y;
const double qz = rotation.Z;
const double qw = rotation.W;
const double vx = value.X;
const double vy = value.Y;
const double vz = value.Z;
const double tx = 2.0 * (qy * vz - qz * vy);
const double ty = 2.0 * (qz * vx - qx * vz);
const double tz = 2.0 * (qx * vy - qy * vx);
const double cx = (qy * tz) - (qz * ty);
const double cy = (qz * tx) - (qx * tz);
const double cz = (qx * ty) - (qy * tx);
const double rx = vx + (tx * qw) + cx;
const double ry = vy + (ty * qw) + cy;
const double rz = vz + (tz * qw) + cz;
return ::float3(static_cast<float>(rx), static_cast<float>(ry), static_cast<float>(rz));}

std::string float4::ToString()
{
return std::to_string(this->X) + std::string(", ") + std::to_string(Y) + std::string(", ") + std::to_string(Z) + std::string(", ") + std::to_string(W);}

float4::float4(float x, float y, float z, float w) : W(), X(), Y(), Z()
{
this->X = x;
this->Y = y;
this->Z = z;
this->W = w;
}

::float4 float4::identity = ::float4(0, 0, 0, 1);

::float4 operator*(::float4 quaternion1, ::float4 quaternion2)
{
::float4 quaternion;
const float x = quaternion1.X;
const float y = quaternion1.Y;
const float z = quaternion1.Z;
const float w = quaternion1.W;
const float num4 = quaternion2.X;
const float num3 = quaternion2.Y;
const float num2 = quaternion2.Z;
const float num = quaternion2.W;
const float num12 = (y * num2) - (z * num3);
const float num11 = (z * num4) - (x * num2);
const float num10 = (x * num3) - (y * num4);
const float num9 = ((x * num4) + (y * num3)) + (z * num2);
quaternion.X = ((x * num) + (num4 * w)) + num12;
quaternion.Y = ((y * num) + (num3 * w)) + num11;
quaternion.Z = ((z * num) + (num2 * w)) + num10;
quaternion.W = (w * num) - num9;
return quaternion;}

