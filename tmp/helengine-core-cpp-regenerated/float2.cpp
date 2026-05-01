#ifdef DrawText
#undef DrawText
#endif
#include "float2.hpp"
#include "float2.hpp"
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

float2::float2() : X(), Y()
{
}

bool float2::Equals(void* obj)
{
    if (obj == nullptr)
    {
return false;    }
else     if (!(obj != nullptr))
    {
return false;    }
::float2 other = (*static_cast<float2*>(obj));
return this->X == other.X && this->Y == other.Y;}

bool float2::Equals(::float3 other)
{
return this->X == other.X && this->Y == other.Y;}

int32_t float2::GetHashCode()
{
{
int32_t hash = Number::GetHashCode(this->X);
hash = (hash * 397) ^ Number::GetHashCode(this->Y);
return hash;}
}

float2::float2(float x, float y) : X(), Y()
{
this->X = x;
this->Y = y;
}

float2::float2(float value) : X(), Y()
{
this->X = value;
this->Y = value;
}

bool operator!=(::float2 a, ::float2 b)
{
return a.X != b.X || a.Y != b.Y;}

::float2 operator*(::float2 a, ::float2 b)
{
return ::float2(a.X * b.X, a.Y * b.Y);}

::float2 operator*(::float2 a, float scalar)
{
return ::float2(a.X * scalar, a.Y * scalar);}

::float2 operator/(::float2 a, ::float2 b)
{
return ::float2(a.X / b.X, a.Y / b.Y);}

::float2 operator/(::float2 a, float scalar)
{
return ::float2(a.X / scalar, a.Y / scalar);}

bool operator==(::float2 a, ::float2 b)
{
return a.X == b.X && a.Y == b.Y;}

