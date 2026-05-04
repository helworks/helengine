#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeStartupManifest;
class RuntimeCodeModuleManifest;
class RuntimeCodeModuleManifestEntry;

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "RuntimeStartupManifest.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleManifest.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleLoadState.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleManifestEntry.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "system/text/string-builder.hpp"
#include "RuntimeCodeModuleManifest.hpp"
#include "RuntimeStartupManifest.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleManifestEntry.hpp"

class RuntimeManifestJsonReader
{
public:
    virtual ~RuntimeManifestJsonReader() = default;

    static ::RuntimeCodeModuleManifest* ReadRuntimeCodeModuleManifest(std::string json);

    static ::RuntimeStartupManifest* ReadRuntimeStartupManifest(std::string json);
private:
    static int32_t FindMatchingJsonDelimiter(std::string json, int32_t openIndex, char openDelimiter, char closeDelimiter);

    static int32_t FindPropertyNameIndex(std::string json, std::string propertyName);

    static int32_t FindRequiredPropertyValueStart(std::string json, std::string propertyName);

    static bool IsJsonPrimitiveTerminator(char value);

    static bool IsJsonWhitespace(char value);

    static int32_t ReadJsonHexDigit(char value);

    static std::string ReadJsonPrimitiveValue(std::string json, int32_t valueStart, int32_t& valueLength);

    static std::string ReadJsonStringValue(std::string json, int32_t valueStart, int32_t& valueLength);

    static int32_t ReadJsonValueLength(std::string json, int32_t valueStart);

    static std::string ReadRequiredArrayProperty(std::string json, std::string propertyName);

    static int32_t ReadRequiredIntegerProperty(std::string json, std::string propertyName);

    static std::string ReadRequiredObjectProperty(std::string json, std::string propertyName);

    static Array<std::string>* ReadRequiredStringArrayProperty(std::string json, std::string propertyName);

    static std::string ReadRequiredStringProperty(std::string json, std::string propertyName);

    static Array<::RuntimeCodeModuleManifestEntry*>* ReadRuntimeCodeModuleEntries(std::string json);

    static ::RuntimeCodeModuleManifestEntry* ReadRuntimeCodeModuleManifestEntry(std::string json);

    static int32_t SkipWhitespace(std::string json, int32_t startIndex);

    static bool TryReadNextArrayElement(std::string json, int32_t& cursor, int32_t& valueStart, int32_t& valueLength);
};
