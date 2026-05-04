#ifdef DrawText
#undef DrawText
#endif
#include "MaterialRenderState.hpp"
#include "MaterialRenderState.hpp"
#include "MaterialBlendMode.hpp"
#include "MaterialCullMode.hpp"
#include "runtime/array.hpp"
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
#include "system/guid.hpp"
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

::MaterialBlendMode MaterialRenderState::get_BlendMode()
{
return this->BlendMode;
}

void MaterialRenderState::set_BlendMode(::MaterialBlendMode value)
{
this->BlendMode = value;
}

::MaterialCullMode MaterialRenderState::get_CullMode()
{
return this->CullMode;
}

void MaterialRenderState::set_CullMode(::MaterialCullMode value)
{
this->CullMode = value;
}

bool MaterialRenderState::get_DepthTestEnabled()
{
return this->DepthTestEnabled;
}

void MaterialRenderState::set_DepthTestEnabled(bool value)
{
this->DepthTestEnabled = value;
}

bool MaterialRenderState::get_DepthWriteEnabled()
{
return this->DepthWriteEnabled;
}

void MaterialRenderState::set_DepthWriteEnabled(bool value)
{
this->DepthWriteEnabled = value;
}

::MaterialRenderState* MaterialRenderState::Clone()
{
return ([&]() {
auto __object_000000B3 = new ::MaterialRenderState();
__object_000000B3->set_BlendMode(this->BlendMode);
__object_000000B3->set_CullMode(this->CullMode);
__object_000000B3->set_DepthTestEnabled(this->DepthTestEnabled);
__object_000000B3->set_DepthWriteEnabled(this->DepthWriteEnabled);
return __object_000000B3;
})();}

MaterialRenderState::MaterialRenderState() : BlendMode(), CullMode(), DepthTestEnabled(), DepthWriteEnabled()
{
this->set_BlendMode(MaterialBlendMode::Opaque);
this->set_CullMode(MaterialCullMode::Back);
this->set_DepthTestEnabled(true);
this->set_DepthWriteEnabled(true);
}

