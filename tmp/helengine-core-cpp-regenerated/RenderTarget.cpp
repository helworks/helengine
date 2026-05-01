#ifdef DrawText
#undef DrawText
#endif
#include "RenderTarget.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
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
#include "system/math.hpp"
#include "system/text/encoding.hpp"

int32_t RenderTarget::get_Height()
{
return this->RuntimeTexture::get_Height();
}

void RenderTarget::set_Height(int32_t value)
{
this->RuntimeTexture::set_Height(value);
}

int32_t RenderTarget::get_Width()
{
return this->RuntimeTexture::get_Width();
}

void RenderTarget::set_Width(int32_t value)
{
this->RuntimeTexture::set_Width(value);
}

std::string RenderTarget::get_Id()
{
return this->RuntimeData::get_Id();
}

void RenderTarget::set_Id(std::string value)
{
this->RuntimeData::set_Id(value);
}

