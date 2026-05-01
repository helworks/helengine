#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IContentProcessor;
template <typename T> class IContentProcessor_1;

#include "IContentProcessor.hpp"
#include "IContentProcessor_1.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_type.hpp"
#include "system/func.hpp"
#include "system/io/stream.hpp"

template <typename T>
class BinaryContentProcessor_1 : public IContentProcessor_1<T>
{
public:
    Type* get_OutputType();

    BinaryContentProcessor_1(Func<Stream*, T>* reader);

    T Read(::Stream* stream);
private:
    Func<Stream*, T>* Reader;

    void* ReadObject(::Stream* stream);
};
