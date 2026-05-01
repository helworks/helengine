#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IContentProcessor;

#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "runtime/native_type.hpp"
#include "IContentProcessor.hpp"

class ContentProcessorRegistration
{
public:
    Array<std::string>* get_Extensions();

    Type* get_OutputType();

    ::IContentProcessor* get_Processor();

    std::string get_ProcessorId();

    ContentProcessorRegistration(std::string processorId, ::IContentProcessor* processor, Array<std::string>* extensions);
private:
    Array<std::string>* ExtensionsValue;

    std::string ProcessorIdValue;

    ::IContentProcessor* ProcessorValue;

    std::string NormalizeExtension(std::string extension);

    Array<std::string>* NormalizeExtensions(Array<std::string>* sourceExtensions);
};
