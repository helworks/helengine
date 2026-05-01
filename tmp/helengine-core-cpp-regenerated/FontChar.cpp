#ifdef DrawText
#undef DrawText
#endif
#include "FontChar.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

FontChar::FontChar() : AdvanceWidth(), BearingX(), BearingY(), OffsetY(), SourceRect()
{
}

FontChar::FontChar(::float4 sourceRect, float offsetY, float advanceWidth, float bearingX, float bearingY) : AdvanceWidth(), BearingX(), BearingY(), OffsetY(), SourceRect()
{
this->SourceRect = sourceRect;
this->OffsetY = offsetY;
this->AdvanceWidth = advanceWidth;
this->BearingX = bearingX;
this->BearingY = bearingY;
}

FontChar::FontChar(::float4 r, float offsetY) : AdvanceWidth(), BearingX(), BearingY(), OffsetY(), SourceRect()
{
this->SourceRect = r;
this->OffsetY = offsetY;
this->AdvanceWidth = r.Z;
this->BearingX = 0;
this->BearingY = 0;
}

