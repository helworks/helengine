#ifdef DrawText
#undef DrawText
#endif
#include "float3.hpp"
#include "runtime/native_string.hpp"
#include "float3.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_equatable.hpp"
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
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/directory.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

float3::float3() : X(), Y(), Z()
{
}

::float3 float3::get_One()
{
return one;}

::float3 float3::get_Zero()
{
return zero;}

void float3::Cross(::float3& vector1, ::float3& vector2, ::float3& result)
{
float x = vector1.Y * vector2.Z - vector2.Y * vector1.Z;
float y = -(vector1.X * vector2.Z - vector2.X * vector1.Z);
float z = vector1.X * vector2.Y - vector2.X * vector1.Y;
result.X = x;
result.Y = y;
result.Z = z;
}

::float3 float3::Cross(::float3 vector1, ::float3 vector2)
{
Cross(vector1, vector2, vector1);
return vector1;}

void float3::Dot(::float3& value1, ::float3& value2, float& result)
{
result = value1.X * value2.X + value1.Y * value2.Y + value1.Z * value2.Z;
}

float float3::Dot(::float3 value1, ::float3 value2)
{
return value1.X * value2.X + value1.Y * value2.Y + value1.Z * value2.Z;}

bool float3::Equals(void* obj)
{
    if (obj == nullptr)
    {
return false;    }
else     if (!(obj != nullptr))
    {
return false;    }
::float3 other = (*static_cast<float3*>(obj));
return this->X == other.X && this->Y == other.Y && this->Z == other.Z;}

bool float3::Equals(::float3 other)
{
return this->X == other.X && this->Y == other.Y && this->Z == other.Z;}

int32_t float3::GetHashCode()
{
{
int32_t hashCode = Number::GetHashCode(this->X);
hashCode = (hashCode * 397) ^ Number::GetHashCode(this->Y);
hashCode = (hashCode * 397) ^ Number::GetHashCode(this->Z);
return hashCode;}
}

float3::float3(float x, float y, float z) : X(), Y(), Z()
{
this->X = x;
this->Y = y;
this->Z = z;
}

float3::float3(float value) : X(), Y(), Z()
{
this->X = value;
this->Y = value;
this->Z = value;
}

float3::float3(::float2 value, float z) : X(), Y(), Z()
{
this->X = value.X;
this->Y = value.Y;
this->Z = z;
}

::float3 float3::Normalize(::float3 value)
{
float factor = static_cast<float>(Math::Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z)));
factor = 1.0f / factor;
return ::float3(value.X * factor, value.Y * factor, value.Z * factor);}

std::string float3::ToString()
{
return std::to_string(this->X) + std::string(", ") + std::to_string(Y) + std::string(", ") + std::to_string(Z);}

::float3 float3::one = ::float3(1.0f, 1.0f, 1.0f);

::float3 float3::zero = ::float3(0.0f, 0.0f, 0.0f);

bool operator!=(::float3 a, ::float3 b)
{
return a.X != b.X || a.Y != b.Y || a.Z != b.Z;}

::float3 operator*(::float3 a, ::float3 b)
{
return ::float3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);}

::float3 operator*(::float3 a, float scalar)
{
return ::float3(a.X * scalar, a.Y * scalar, a.Z * scalar);}

::float3 operator+(::float3 a, ::float3 b)
{
return ::float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);}

::float3 operator-(::float3 a, ::float3 b)
{
return ::float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);}

::float3 operator/(::float3 a, float scalar)
{
return ::float3(a.X / scalar, a.Y / scalar, a.Z / scalar);}

::float3 operator/(::float3 a, ::float3 b)
{
return ::float3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);}

bool operator==(::float3 a, ::float3 b)
{
return a.X == b.X && a.Y == b.Y && a.Z == b.Z;}

