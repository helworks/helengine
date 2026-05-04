#include "runtime/runtime_startup_manifest.hpp"

static const char kRuntimeStartupSceneRelativePath[] = "cooked/scenes/main.hasset";

const char* he_get_runtime_startup_scene_relative_path() {
    return kRuntimeStartupSceneRelativePath;
}
