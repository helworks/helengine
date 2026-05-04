#ifdef DrawText
#undef DrawText
#endif
#include "Core.hpp"
#include "InputSystem.hpp"
#include "runtime/native_exceptions.hpp"
#include "CoreInitializationOptions.hpp"
#include "ContentManager.hpp"
#include "RuntimeContentManagerConfiguration.hpp"
#include "RuntimeComponentRegistry.hpp"
#include "runtime/native_string.hpp"
#include "system/io/path.hpp"
#include "FPSComponent.hpp"
#include "ObjectManager.hpp"
#include "PointerInteractionSystem.hpp"
#include "RenderManager3D.hpp"
#include "RenderManager2D.hpp"
#include "RuntimeSceneAssetReferenceResolver.hpp"
#include "ShaderCompileTarget.hpp"
#include "RuntimeSceneLoadService.hpp"
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
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::Core* Core::Instance;

::Core* Core::get_Instance()
{
return Core::Instance;
}

void Core::set_Instance(::Core* value)
{
Core::Instance = value;
}

::ContentManager* Core::get_ContentManager()
{
return this->GetContentManager();
}

::FontAsset* Core::get_DefaultFontAsset()
{
return this->DefaultFontAssetValue;}

void Core::set_DefaultFontAsset(::FontAsset* value)
{
    if (value == nullptr)
    {
throw new ArgumentNullException("value");
    }
this->DefaultFontAssetValue = value;
}

::CoreInitializationOptions* Core::get_InitializationOptions()
{
return this->InitializationOptions;
}

void Core::set_InitializationOptions(::CoreInitializationOptions* value)
{
this->InitializationOptions = value;
}

InputSystem* Core::get_Input()
{
return this->Input;
}

void Core::set_Input(InputSystem* value)
{
this->Input = value;
}

::ObjectManager* Core::get_ObjectManager()
{
return this->ObjectManager;
}

void Core::set_ObjectManager(::ObjectManager* value)
{
this->ObjectManager = value;
}

::PointerInteractionSystem* Core::get_PointerInteractionSystem()
{
return this->PointerInteractionSystem;
}

void Core::set_PointerInteractionSystem(::PointerInteractionSystem* value)
{
this->PointerInteractionSystem = value;
}

::RenderManager2D* Core::get_RenderManager2D()
{
return this->RenderManager2D;
}

void Core::set_RenderManager2D(::RenderManager2D* value)
{
this->RenderManager2D = value;
}

::RenderManager3D* Core::get_RenderManager3D()
{
return this->RenderManager3D;
}

void Core::set_RenderManager3D(::RenderManager3D* value)
{
this->RenderManager3D = value;
}

::RuntimeSceneAssetReferenceResolver* Core::get_SceneAssetReferenceResolver()
{
return this->SceneAssetReferenceResolver;
}

void Core::set_SceneAssetReferenceResolver(::RuntimeSceneAssetReferenceResolver* value)
{
this->SceneAssetReferenceResolver = value;
}

::RuntimeSceneLoadService* Core::get_SceneLoadService()
{
return this->SceneLoadService;
}

void Core::set_SceneLoadService(::RuntimeSceneLoadService* value)
{
this->SceneLoadService = value;
}

void Core::Dispose()
{
this->RenderManager3D->Dispose();
this->RenderManager2D->Dispose();
}

void Core::Draw()
{
this->RenderManager3D->Draw();
FPSComponent::RecordRenderFrame();
}

::ContentManager* Core::GetContentManager()
{
return this->GetContentManager(this->InitializationOptions->get_ContentRootPath());}

::ContentManager* Core::GetContentManager(std::string rootDirectory)
{
    if (String::IsNullOrWhiteSpace(rootDirectory))
    {
throw ([&]() {
auto __ctor_arg_0000017D = "Root directory must be provided.";
auto __ctor_arg_0000017E = "rootDirectory";
return new ArgumentException(__ctor_arg_0000017D, __ctor_arg_0000017E);
})();
    }
const std::string normalizedRootDirectory = Path::GetFullPath(rootDirectory);
// Lock omitted in TypeScript
::ContentManager* contentManager;
    if (this->ContentManagersByRootPath->TryGetValue(normalizedRootDirectory, contentManager))
    {
return contentManager;    }
contentManager = new ::ContentManager(normalizedRootDirectory);
this->ContentManagersByRootPath->Add(normalizedRootDirectory, contentManager);
return contentManager;}

void Core::Initialize(::RenderManager3D* render3D, ::RenderManager2D* render2D, IInputBackend* input)
{
this->Initialize(render3D, render2D, input, this->InitializationOptions);
}

void Core::Initialize(::RenderManager3D* render3D, ::RenderManager2D* render2D, IInputBackend* input, ::CoreInitializationOptions* options)
{
this->set_RenderManager3D(render3D);
this->set_RenderManager2D(render2D);
this->Input->SetBackend(input);
    if (options == nullptr)
    {
throw new ArgumentNullException("options");
    }
options->Normalize();
this->set_InitializationOptions(options);
this->set_ObjectManager(new ::ObjectManager(options));
::ContentManager *contentManager = this->GetContentManager();
RuntimeContentManagerConfiguration::ConfigureSharedAssetContentManager(contentManager);
this->set_SceneAssetReferenceResolver(new ::RuntimeSceneAssetReferenceResolver(contentManager, this->InitializationOptions->get_ContentRootPath(), ShaderCompileTarget::DirectX11));
::RuntimeComponentRegistry *runtimeComponentRegistry = RuntimeComponentRegistry::CreateDefault();
this->set_SceneLoadService(new ::RuntimeSceneLoadService(this->SceneAssetReferenceResolver, runtimeComponentRegistry));
}

Core::Core() : Core(new CoreInitializationOptions())
{
}

Core::Core(::CoreInitializationOptions* options) : InitializationOptions(), Input(), ObjectManager(), PointerInteractionSystem(), RenderManager2D(), RenderManager3D(), SceneAssetReferenceResolver(), SceneLoadService(), ContentManagerLock(), ContentManagersByRootPath(), DefaultFontAssetValue()
{
    if (options == nullptr)
    {
throw new ArgumentNullException("options");
    }
this->ContentManagersByRootPath = new Dictionary<std::string, ::ContentManager*>(StringComparer::OrdinalIgnoreCase);
this->ContentManagerLock = new char[1];
Core::set_Instance(this);
this->set_InitializationOptions(options);
this->InitializationOptions->Normalize();
this->set_Input(new InputSystem());
this->set_PointerInteractionSystem(new ::PointerInteractionSystem(this, this->Input));
}

void Core::Update()
{
this->Input->EarlyUpdate();
FPSComponent::RecordUpdateFrame();
this->ObjectManager->Update();
this->Input->Update();
this->PointerInteractionSystem->Update();
}

