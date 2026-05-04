#ifdef DrawText
#undef DrawText
#endif
#include "FontTightMetrics.hpp"
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
#include "system/math.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

FontTightMetrics::FontTightMetrics() : MaxBottom(), MinTop(), Width()
{
}

float FontTightMetrics::get_Height()
{
return Math::Max(1.0f, this->MaxBottom - this->MinTop);
}

FontTightMetrics::FontTightMetrics(float width, float minTop, float maxBottom) : MaxBottom(), MinTop(), Width()
{
this->Width = width;
this->MinTop = minTop;
this->MaxBottom = maxBottom;
}

