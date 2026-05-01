#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ContentManager;
template <typename T> class IContentProcessor_1;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "ContentManager.hpp"
#include "ContentManager.hpp"
#include "IContentProcessor_1.hpp"
#include "runtime/array.hpp"

class RuntimeContentManagerConfiguration
{
public:
    static void ConfigureSharedAssetContentManager(::ContentManager* contentManager);
private:
    static std::string MaterialAssetExtension;

    static std::string ShaderPackageExtension;

    template <typename T>
    static void RegisterProcessorIfMissing(::ContentManager* contentManager, std::string processorId, ::IContentProcessor_1<T>* processor, Array<std::string>* extensions);
};
