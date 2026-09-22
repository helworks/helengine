#include <iostream>
#include <stdexcept>
#include "Core.hpp"
#include "CoreInitializationOptions.hpp"
#include "IContentStreamSource.hpp"
#include "RuntimeSceneCatalog.hpp"
#include "runtime/array.hpp"
#include "PlatformInfo.hpp"
#include "SceneManager.hpp"
#include "SceneLoadedEventArgs.hpp"
#include "SceneUnloadingEventArgs.hpp"
#include "Entity.hpp"
#include "RigidBody3DComponent.hpp"
#include "BoxCollider3DComponent.hpp"
#include "SphereCollider3DComponent.hpp"
#include "HelPhysicsRuntime3D.hpp"
#include "HelPhysicsRuntimeComponentRegistration.hpp"
#include "BodyKind3D.hpp"
#include "float3.hpp"
#include "runtime/native_list.hpp"

/// <summary>Rejects unexpected asset reads from the manually constructed smoke scene.</summary>
class EmptyContentStreamSource final : public IContentStreamSource {
public:
    /// <summary>Fails when the fixture unexpectedly requests a content stream.</summary>
    Stream* OpenRead(std::string) override { throw std::runtime_error("OpenRead is not expected in this manually constructed scene."); }
};

/// <summary>Stops the smoke run when one native runtime contract fails.</summary>
static void Require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

/// <summary>Creates one static or trigger box entity for the smoke scene.</summary>
static Entity* MakeBox(Core* core, float y, float3 size, BodyKind3D kind, bool trigger) {
    Entity* entity = new Entity(core);
    entity->set_Position(float3(0.0f, y, 0.0f));
    entity->InitChildren();
    entity->InitComponents();
    RigidBody3DComponent* body = new RigidBody3DComponent();
    body->set_BodyKind(kind);
    body->set_UseGravity(false);
    BoxCollider3DComponent* collider = new BoxCollider3DComponent();
    collider->set_Size(size);
    collider->set_IsTrigger(trigger);
    collider->set_CollisionLayer(1);
    collider->set_CollisionMask(1);
    entity->AddComponent(body);
    entity->AddComponent(collider);
    entity->InitializeHierarchy();
    return entity;
}

/// <summary>Creates one gravity-driven dynamic sphere entity.</summary>
static Entity* MakeSphere(Core* core, float y) {
    Entity* entity = new Entity(core);
    entity->set_Position(float3(0.0f, y, 0.0f));
    entity->InitChildren();
    entity->InitComponents();
    RigidBody3DComponent* body = new RigidBody3DComponent();
    body->set_BodyKind(BodyKind3D::Dynamic);
    body->set_UseGravity(true);
    body->set_GravityScale(1.0);
    body->set_Mass(1.0);
    SphereCollider3DComponent* collider = new SphereCollider3DComponent();
    collider->set_Radius(0.5f);
    collider->set_CollisionLayer(1);
    collider->set_CollisionMask(1);
    entity->AddComponent(body);
    entity->AddComponent(collider);
    entity->InitializeHierarchy();
    return entity;
}

/// <summary>Runs native registration, fixed updates, trigger delivery, and unload/reload checks.</summary>
int main() {
    Entity* floor = nullptr;
    Entity* trigger = nullptr;
    Entity* sphere = nullptr;
    try {
        CoreInitializationOptions* options = new CoreInitializationOptions();
        EmptyContentStreamSource contentSource;
        options->set_ContentStreamSource(&contentSource);
        Array<RuntimeSceneCatalogEntry*>* catalogEntries = new Array<RuntimeSceneCatalogEntry*>(0);
        RuntimeSceneCatalog* sceneCatalog = new RuntimeSceneCatalog(catalogEntries);
        delete catalogEntries;
        options->set_SceneCatalog(sceneCatalog);
        options->set_PhysicsFixedStepSeconds(1.0 / 60.0);
        options->set_PhysicsMaxStepsPerUpdate(4);
        Core* core = new Core(options);
        PlatformInfo platform("helphysics-native-smoke", "runtime-smoke");
        core->Initialize(nullptr, nullptr, nullptr, &platform);
        Require(core->get_SceneManager() != nullptr, "Core did not create a SceneManager.");
        HelPhysicsRuntimeComponentRegistration::Register(core);
        floor = MakeBox(core, 0.0f, float3(12.0f, 1.0f, 12.0f), BodyKind3D::Static, false);
        trigger = MakeBox(core, 2.5f, float3(4.0f, 2.0f, 4.0f), BodyKind3D::Static, true);
        sphere = MakeSphere(core, 6.0f);
        List<Entity*> roots;
        roots.Add(floor);
        roots.Add(trigger);
        roots.Add(sphere);
        SceneLoadedEventArgs loaded("helphysics-smoke", "helphysics-smoke.hscene", &roots);
        core->get_SceneManager()->SceneLoaded.Invoke(core->get_SceneManager(), &loaded);

        HelPhysicsRuntime3D* runtime = dynamic_cast<HelPhysicsRuntime3D*>(core->get_PhysicsRuntime());
        Require(runtime != nullptr, "runtime was not attached after scene-loaded callback");
        Require(runtime->get_RegisteredBodyCount() == 3, "scene binder did not register three bodies");
        const float startY = sphere->get_Position().Y;
        bool sawTriggerEvent = false;
        for (int index = 0; index < 240; index++) {
            core->Update(1.0 / 60.0);
            IReadOnlyList<TriggerEvent3D*>* events = runtime->get_TriggerEvents();
            if (events != nullptr && events->get_Count() > 0) {
                sawTriggerEvent = true;
            }
        }
        const float finalY = sphere->get_Position().Y;
        Require(finalY < startY - 0.25f, "dynamic sphere did not move under native Core.Update");
        Require(finalY > 0.9f && finalY < 1.1f, "dynamic sphere did not settle near static floor");
        Require(sawTriggerEvent, "registered trigger produced no event");

        SceneUnloadingEventArgs unloading("helphysics-smoke", "helphysics-smoke.hscene", &roots);
        core->get_SceneManager()->SceneUnloading.Invoke(core->get_SceneManager(), &unloading);
        Require(core->get_PhysicsRuntime() == nullptr, "scene unload retained the runtime");
        core->get_SceneManager()->SceneLoaded.Invoke(core->get_SceneManager(), &loaded);
        HelPhysicsRuntime3D* reloadedRuntime = dynamic_cast<HelPhysicsRuntime3D*>(core->get_PhysicsRuntime());
        Require(reloadedRuntime != nullptr, "scene reload did not attach a runtime");
        Require(reloadedRuntime->get_RegisteredBodyCount() == 3, "scene reload did not register three bodies");
        core->get_SceneManager()->SceneUnloading.Invoke(core->get_SceneManager(), &unloading);
        Require(core->get_PhysicsRuntime() == nullptr, "second scene unload retained the runtime");
        floor->Dispose();
        trigger->Dispose();
        sphere->Dispose();
        core->Dispose();
        delete core;
        std::cout << "PASS native runtime smoke" << std::endl;
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << "FAIL native runtime smoke: " << exception.what() << std::endl;
        if (floor != nullptr) floor->Dispose();
        if (trigger != nullptr) trigger->Dispose();
        if (sphere != nullptr) sphere->Dispose();
        return 1;
    }
}
