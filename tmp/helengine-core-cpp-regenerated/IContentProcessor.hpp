#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_type.hpp"
#include "system/io/stream.hpp"

class IContentProcessor
{
public:
    virtual Type* get_OutputType() = 0;

    virtual void* ReadObject(::Stream* stream) = 0;
};
