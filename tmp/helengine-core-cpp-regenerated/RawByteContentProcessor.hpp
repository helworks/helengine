#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IContentProcessor;
template <typename T> class IContentProcessor_1;
class RawByteContent;

#include "IContentProcessor.hpp"
#include "IContentProcessor_1.hpp"
#include "RawByteContent.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/stream.hpp"
#include "RawByteContent.hpp"
#include "runtime/native_type.hpp"
#include "system/io/stream.hpp"

class RawByteContentProcessor : public IContentProcessor_1<::RawByteContent*>
{
public:
    Type* get_OutputType();

    ::RawByteContent* Read(::Stream* stream);
private:
    void* ReadObject(::Stream* stream);
};
