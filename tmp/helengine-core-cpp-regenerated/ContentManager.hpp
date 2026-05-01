#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ContentProcessorRegistration;
template <typename T> class IContentProcessor_1;

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "ContentProcessorRegistration.hpp"
#include "runtime/native_dictionary.hpp"
#include "IContentProcessor_1.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/file.hpp"
#include "ContentProcessorRegistration.hpp"
#include "IContentProcessor_1.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_type.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"

class ContentManager
{
public:
    std::string get_RootDirectory();

    ContentManager(std::string rootDirectory);

    bool IsProcessorRegistered(std::string processorId);

    template <typename T>
    T Load(std::string assetPath, std::string processorId);

    template <typename T>
    T Load(std::string assetPath, ::IContentProcessor_1<T>* processor);

    void RegisterProcessor(::ContentProcessorRegistration* registration);

    template <typename T>
    void RegisterProcessor(std::string processorId, ::IContentProcessor_1<T>* processor, Array<std::string>* extensions);
private:
    static std::string RawByteContentProcessorId;

    static std::string TextContentProcessorId;

    static std::string WildcardExtension;

    Dictionary<Type*, Dictionary<std::string, ::ContentProcessorRegistration*>*>* DefaultProcessorsByTypeAndExtension;

    Dictionary<std::string, ::ContentProcessorRegistration*>* ProcessorRegistrationsById;

    std::string RootDirectoryPath;

    Dictionary<std::string, ::ContentProcessorRegistration*>* GetOrCreateTypeRegistrationMap(Type* outputType);

    template <typename T>
    T LoadProcessedContent(std::string fullPath, ::IContentProcessor_1<T>* processor);

    std::string NormalizeExtension(std::string extension);

    void RegisterBuiltInProcessors();

    std::string ResolveContentPath(std::string assetPath);

    ::ContentProcessorRegistration* ResolveDefaultProcessorRegistration(Type* requestedType, std::string fullPath);

    ::ContentProcessorRegistration* ResolveExplicitProcessorRegistration(Type* requestedType, std::string processorId);

    template <typename T>
    ::IContentProcessor_1<T>* ResolveProcessor(std::string fullPath, std::string processorId);

    bool TryResolveRegisteredExtension(std::string fileName, Dictionary<std::string, ::ContentProcessorRegistration*>* registrationsByExtension, std::string& matchedExtension);
};
