#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeContentManagerConfiguration.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "ContentManager.hpp"
#include "RuntimeContentProcessorIds.hpp"
#include "AssetContentProcessor.hpp"
#include "MaterialAsset.hpp"
#include "ModelAsset.hpp"
#include "TextureAsset.hpp"
#include "TextAsset.hpp"
#include "ShaderAsset.hpp"
#include "SceneAsset.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

void RuntimeContentManagerConfiguration::ConfigureSharedAssetContentManager(::ContentManager* contentManager)
{
    if (contentManager == nullptr)
    {
throw new ArgumentNullException("contentManager");
    }
RegisterProcessorIfMissing<MaterialAsset*>(contentManager, RuntimeContentProcessorIds::MaterialAsset, new ::AssetContentProcessor_1<::MaterialAsset*>(), new Array<std::string>({ MaterialAssetExtension }));
RegisterProcessorIfMissing<ModelAsset*>(contentManager, RuntimeContentProcessorIds::ModelAsset, new ::AssetContentProcessor_1<::ModelAsset*>(), nullptr);
RegisterProcessorIfMissing<TextureAsset*>(contentManager, RuntimeContentProcessorIds::TextureAsset, new ::AssetContentProcessor_1<::TextureAsset*>(), nullptr);
RegisterProcessorIfMissing<TextAsset*>(contentManager, RuntimeContentProcessorIds::TextAsset, new ::AssetContentProcessor_1<::TextAsset*>(), nullptr);
RegisterProcessorIfMissing<ShaderAsset*>(contentManager, RuntimeContentProcessorIds::ShaderAsset, new ::AssetContentProcessor_1<::ShaderAsset*>(), new Array<std::string>({ ShaderPackageExtension }));
RegisterProcessorIfMissing<SceneAsset*>(contentManager, RuntimeContentProcessorIds::SceneAsset, new ::AssetContentProcessor_1<::SceneAsset*>(), new Array<std::string>({ SceneAsset::FileExtension }));
}

std::string RuntimeContentManagerConfiguration::MaterialAssetExtension = ".helmat";

std::string RuntimeContentManagerConfiguration::ShaderPackageExtension = ".shader.asset";

template <typename T>
void RuntimeContentManagerConfiguration::RegisterProcessorIfMissing(::ContentManager* contentManager, std::string processorId, ::IContentProcessor_1<T>* processor, Array<std::string>* extensions)
{
    if (contentManager == nullptr)
    {
throw new ArgumentNullException("contentManager");
    }
    if (String::IsNullOrWhiteSpace(processorId))
    {
throw ([&]() {
auto __ctor_arg_5c9bc77c = "Processor id must be provided.";
auto __ctor_arg_f71fc2d1 = "processorId";
return new ArgumentException(__ctor_arg_5c9bc77c, __ctor_arg_f71fc2d1);
})();
    }
    if (processor == nullptr)
    {
throw new ArgumentNullException("processor");
    }
    if (contentManager->IsProcessorRegistered(processorId))
    {
return;    }
contentManager->RegisterProcessor<T>(processorId, processor, extensions);
}

