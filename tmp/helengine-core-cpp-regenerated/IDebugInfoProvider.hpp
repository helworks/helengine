#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_tuple.hpp"

class IDebugInfoProvider
{
public:
    virtual std::string get_Category() = 0;

    virtual void AppendInfo(List<ValueTuple<std::string, std::string>*>* items) = 0;
};
