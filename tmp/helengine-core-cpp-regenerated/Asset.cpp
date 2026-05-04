#ifdef DrawText
#undef DrawText
#endif
#include "Asset.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/bit_converter.hpp"
#include "system/io/stream.hpp"
#include "system/text/encoding.hpp"

Asset::Asset() : Id()
{
}

std::string Asset::get_Id()
{
return this->Id;
}

void Asset::set_Id(std::string value)
{
this->Id = value;
}

