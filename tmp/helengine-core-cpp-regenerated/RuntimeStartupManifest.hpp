#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeStorageProfileId;

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
#include "RuntimeStorageProfileId.hpp"

class RuntimeStartupManifest
{
public:
    virtual ~RuntimeStartupManifest() = default;

    std::string StartupSceneId;

    std::string get_StartupSceneId();

    ::RuntimeStorageProfileId* StorageProfileId;

    ::RuntimeStorageProfileId* get_StorageProfileId();

    static ::RuntimeStartupManifest* ReadFromFile(std::string startupManifestPath);

    RuntimeStartupManifest(std::string startupSceneId, ::RuntimeStorageProfileId* storageProfileId);
};
