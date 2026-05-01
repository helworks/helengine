#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IContentProcessor;

#include "IContentProcessor.hpp"
#include "system/io/stream.hpp"

template <typename T>
class IContentProcessor_1 : public IContentProcessor
{
public:
    virtual T Read(::Stream* stream) = 0;
};
