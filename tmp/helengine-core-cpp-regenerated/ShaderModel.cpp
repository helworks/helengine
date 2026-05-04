#ifdef DrawText
#undef DrawText
#endif
#include "ShaderModel.hpp"
#include "runtime/native_string.hpp"
#include "ShaderStage.hpp"
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
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

int32_t ShaderModel::get_Major()
{
return this->Major;
}

int32_t ShaderModel::get_Minor()
{
return this->Minor;
}

std::string ShaderModel::GetProfile(::ShaderStage stage)
{
const std::string prefix = this->GetStagePrefix(stage);
return String::Concat(prefix, "_", std::to_string(this->Major), "_", std::to_string(this->Minor));}

ShaderModel::ShaderModel(int32_t major, int32_t minor) : Major(0), Minor(0)
{
    if (major < 0)
    {
throw ([&]() {
auto __ctor_arg_00000130 = "major";
auto __ctor_arg_00000131 = "Major version cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_00000130, __ctor_arg_00000131);
})();
    }
    if (minor < 0)
    {
throw ([&]() {
auto __ctor_arg_00000132 = "minor";
auto __ctor_arg_00000133 = "Minor version cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_00000132, __ctor_arg_00000133);
})();
    }
this->Major = major;
this->Minor = minor;
}

std::string ShaderModel::ToString()
{
return String::Concat(std::to_string(this->Major), ".", std::to_string(this->Minor));}

std::string ShaderModel::GetStagePrefix(::ShaderStage stage)
{
switch (stage) {
case ShaderStage::Vertex: {
return "vs";}
case ShaderStage::Pixel: {
return "ps";}
case ShaderStage::Geometry: {
return "gs";}
case ShaderStage::Hull: {
return "hs";}
case ShaderStage::Domain: {
return "ds";}
case ShaderStage::Compute: {
return "cs";}
default:  {
throw ([&]() {
auto __ctor_arg_00000134 = "stage";
auto __ctor_arg_00000135 = "Unsupported shader stage.";
return new ArgumentOutOfRangeException(__ctor_arg_00000134, __ctor_arg_00000135);
})();
}
}

}

