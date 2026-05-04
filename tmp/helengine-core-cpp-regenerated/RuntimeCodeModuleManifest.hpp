#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeCodeModuleManifestEntry;

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/file.hpp"
#include "system/io/stream-reader.hpp"
#include "system/text/encoding.hpp"
#include "system/text/encoding.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/file-stream.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleManifestEntry.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleLoadState.hpp"

class RuntimeCodeModuleManifest
{
public:
    virtual ~RuntimeCodeModuleManifest() = default;

    Array<::RuntimeCodeModuleManifestEntry*>* Entries;

    Array<::RuntimeCodeModuleManifestEntry*>* get_Entries();

    bool CanUnloadModule(std::string moduleId);

    Array<std::string>* GetResidentModuleIds();

    Array<std::string>* GetUnloadableModuleIds();

    static ::RuntimeCodeModuleManifest* ReadFromFile(std::string manifestPath);

    RuntimeCodeModuleManifest(Array<::RuntimeCodeModuleManifestEntry*>* entries);
private:
    ::RuntimeCodeModuleLoadState GetLoadState(std::string moduleId);
};
