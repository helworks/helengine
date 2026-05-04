#ifdef DrawText
#undef DrawText
#endif
#include "FontInfo.hpp"
#include "runtime/array.hpp"
#include "runtime/native_cast.hpp"
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
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

int32_t FontInfo::get_LineSpacing()
{
return this->LineSpacing;
}

void FontInfo::set_LineSpacing(int32_t value)
{
this->LineSpacing = value;
}

std::string FontInfo::get_Name()
{
return this->Name;
}

void FontInfo::set_Name(std::string value)
{
this->Name = value;
}

float FontInfo::get_SpaceWidth()
{
return this->SpaceWidth;
}

void FontInfo::set_SpaceWidth(float value)
{
this->SpaceWidth = value;
}

FontInfo::FontInfo(std::string name, int32_t lineSpacing, float spaceWidth) : LineSpacing(0), Name(), SpaceWidth()
{
this->set_Name(name);
this->set_LineSpacing(lineSpacing);
this->set_SpaceWidth(spaceWidth);
}

