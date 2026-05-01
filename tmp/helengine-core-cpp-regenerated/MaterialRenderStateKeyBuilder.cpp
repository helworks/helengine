#ifdef DrawText
#undef DrawText
#endif
#include "MaterialRenderStateKeyBuilder.hpp"
#include "runtime/native_exceptions.hpp"
#include "MaterialRenderState.hpp"
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
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

int32_t MaterialRenderStateKeyBuilder::Build(::MaterialRenderState* renderState)
{
    if (renderState == nullptr)
    {
throw new ArgumentNullException("renderState");
    }
int32_t key = static_cast<int32_t>(renderState->get_BlendMode()) & 0xFF;
key |= (static_cast<int32_t>(renderState->get_CullMode()) & 0xFF) << 8;
    if (renderState->get_DepthTestEnabled())
    {
key |= 1 << 16;
    }
    if (renderState->get_DepthWriteEnabled())
    {
key |= 1 << 17;
    }
return key;}

