#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IContentProcessor;
template <typename T> class IContentProcessor_1;
class TextContent;

#include "IContentProcessor.hpp"
#include "IContentProcessor_1.hpp"
#include "TextContent.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/stream-reader.hpp"
#include "system/text/encoding.hpp"
#include "system/text/encoding.hpp"
#include "TextContent.hpp"
#include "runtime/native_type.hpp"
#include "system/io/stream.hpp"

class TextContentProcessor : public IContentProcessor_1<::TextContent*>
{
public:
    virtual ~TextContentProcessor() = default;

    Type* get_OutputType();

    ::TextContent* Read(::Stream* stream);
private:
    void* ReadObject(::Stream* stream);
};
