#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeTexture.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/bit_converter.hpp"
#include "system/io/stream.hpp"
#include "system/text/encoding.hpp"

RuntimeTexture::RuntimeTexture() : Height(0), Width(0)
{
}

int32_t RuntimeTexture::get_Height()
{
return this->Height;
}

void RuntimeTexture::set_Height(int32_t value)
{
this->Height = value;
}

int32_t RuntimeTexture::get_Width()
{
return this->Width;
}

void RuntimeTexture::set_Width(int32_t value)
{
this->Width = value;
}

std::string RuntimeTexture::get_Id()
{
return this->RuntimeData::get_Id();
}

void RuntimeTexture::set_Id(std::string value)
{
this->RuntimeData::set_Id(value);
}

