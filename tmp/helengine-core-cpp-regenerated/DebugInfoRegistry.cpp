#ifdef DrawText
#undef DrawText
#endif
#include "DebugInfoRegistry.hpp"
#include "runtime/native_list.hpp"
#include "IDebugInfoProvider.hpp"
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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

void DebugInfoRegistry::Register(::IDebugInfoProvider* provider)
{
    if (provider == nullptr)
    {
return;    }
// Lock omitted in TypeScript
providers->Add(provider);
}

List<ValueTuple<std::string, std::string, std::string>*>* DebugInfoRegistry::Snapshot()
{
List<ValueTuple<std::string, std::string, std::string>*> *result = new List<ValueTuple<std::string, std::string, std::string>*>();
// Lock omitted in TypeScript
for (int32_t i = 0; i < providers->Count(); i++) {
::IDebugInfoProvider *p = (*providers)[i];
List<ValueTuple<std::string, std::string>*> *items = new List<ValueTuple<std::string, std::string>*>();
try {
p->AppendInfo(items);
}
catch (...) {
}
for (int32_t j = 0; j < items->Count(); j++) {
ValueTuple<std::string, std::string> *it = (*items)[j];
result->Add(new ValueTuple<std::string, std::string, std::string>(p->get_Category(), it->Item1, it->Item2));
}
}
return result;}

List<::IDebugInfoProvider*>* DebugInfoRegistry::providers = new List<::IDebugInfoProvider*>();

void* DebugInfoRegistry::sync = new char[1];

