#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class CoreInitializationOptions;
class ContentManager;
class RuntimeContentManagerConfiguration;
class InputManager;
class ObjectManager;
class RenderManager3D;
class RenderManager2D;
class RuntimeSceneAssetReferenceResolver;
class RuntimeSceneLoadService;

#include "runtime/native_disposable.hpp"
#include "runtime/native_exceptions.hpp"
#include "CoreInitializationOptions.hpp"
#include "ContentManager.hpp"
#include "RuntimeContentManagerConfiguration.hpp"
#include "RuntimeContentManagerConfiguration.hpp"
#include "ContentManager.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "InputManager.hpp"
#include "ObjectManager.hpp"
#include "RenderManager3D.hpp"
#include "RenderManager2D.hpp"
#include "CoreInitializationOptions.hpp"
#include "InputManager.hpp"
#include "ObjectManager.hpp"
#include "RenderManager2D.hpp"
#include "RenderManager3D.hpp"
#include "RuntimeSceneAssetReferenceResolver.hpp"
#include "RuntimeSceneLoadService.hpp"
#include "runtime/native_dictionary.hpp"

class Core : public IDisposable
{
public:
    static ::Core* Instance;

    static ::Core* get_Instance();
    static void set_Instance(::Core* value);

    ::ContentManager* get_ContentManager();

    ::CoreInitializationOptions* InitializationOptions;

    ::CoreInitializationOptions* get_InitializationOptions();
    void set_InitializationOptions(::CoreInitializationOptions* value);

    ::InputManager* InputManager;

    ::InputManager* get_InputManager();
    void set_InputManager(::InputManager* value);

    ::ObjectManager* ObjectManager;

    ::ObjectManager* get_ObjectManager();
    void set_ObjectManager(::ObjectManager* value);

    ::RenderManager2D* RenderManager2D;

    ::RenderManager2D* get_RenderManager2D();
    void set_RenderManager2D(::RenderManager2D* value);

    ::RenderManager3D* RenderManager3D;

    ::RenderManager3D* get_RenderManager3D();
    void set_RenderManager3D(::RenderManager3D* value);

    ::RuntimeSceneAssetReferenceResolver* SceneAssetReferenceResolver;

    ::RuntimeSceneAssetReferenceResolver* get_SceneAssetReferenceResolver();
    void set_SceneAssetReferenceResolver(::RuntimeSceneAssetReferenceResolver* value);

    ::RuntimeSceneLoadService* SceneLoadService;

    ::RuntimeSceneLoadService* get_SceneLoadService();
    void set_SceneLoadService(::RuntimeSceneLoadService* value);

    void Dispose();

    virtual void Draw();

    ::ContentManager* GetContentManager();

    ::ContentManager* GetContentManager(std::string rootDirectory);

    virtual void Initialize(::RenderManager3D* render3D, ::RenderManager2D* render2D, ::InputManager* input);

    virtual void Initialize(::RenderManager3D* render3D, ::RenderManager2D* render2D, ::InputManager* input, ::CoreInitializationOptions* options);

    Core();

    Core(::CoreInitializationOptions* options);

    virtual void Update();
private:
    void* ContentManagerLock;

    Dictionary<std::string, ::ContentManager*>* ContentManagersByRootPath;
};
