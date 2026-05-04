#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IDebugInfoProvider;

#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "IDebugInfoProvider.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_string.hpp"

class DebugInfoRegistry
{
public:
    virtual ~DebugInfoRegistry() = default;

    static void Register(::IDebugInfoProvider* provider);

    static List<ValueTuple<std::string, std::string, std::string>*>* Snapshot();
private:
    static List<::IDebugInfoProvider*>* providers;

    static void* sync;
};
