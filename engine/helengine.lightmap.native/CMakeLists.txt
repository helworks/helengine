cmake_minimum_required(VERSION 3.16)
project(helengine_lightmap_native VERSION 1.0 LANGUAGES CXX)
# Keep the embedding application's Debug CRT and symbols while optimizing the
# native bulk decode/checksum passes. /RTC checks conflict with /O2; these flag
# changes are directory-local and never alter the parent renderer configuration.
if(MSVC)
    string(REGEX REPLACE "[-/](Od|Ob0|RTC[1su]+)" "" CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}")
endif()
option(HELENGINE_LIGHTMAP_TESTS "Build native lightmap correctness tests" ON)
option(HELLM_BUILD_TESTS "Build native lightmap correctness tests" ${HELENGINE_LIGHTMAP_TESTS})
option(HELLM_BUILD_CLI "Build standalone lightmap baker" ON)
option(HELLM_ENABLE_VULKAN "Build the optional Vulkan ray-query lightmap backend" OFF)
set(HELLM_VULKAN_INCLUDE_DIR "" CACHE PATH "Directory containing vulkan/vulkan.h")
set(HELLM_GLSLC "" CACHE FILEPATH "Path to the glslc shader compiler")
find_package(Threads REQUIRED)
add_library(hellm_xatlas STATIC third_party/xatlas/xatlas.cpp)
target_include_directories(hellm_xatlas PUBLIC third_party/xatlas)
target_compile_features(hellm_xatlas PUBLIC cxx_std_17)
set_target_properties(hellm_xatlas PROPERTIES POSITION_INDEPENDENT_CODE ON)
add_library(helengine_lightmap_native STATIC src/Assets.cpp src/Brdf.cpp src/Probe.cpp src/Transport.cpp src/UvGeometry.cpp src/Baker.cpp)
add_library(hellm::lightmap ALIAS helengine_lightmap_native)
add_library(helengine_lightmap ALIAS helengine_lightmap_native)
target_include_directories(helengine_lightmap_native PUBLIC include)
target_link_libraries(helengine_lightmap_native PRIVATE hellm_xatlas Threads::Threads)
target_compile_features(helengine_lightmap_native PUBLIC cxx_std_17)
set_target_properties(helengine_lightmap_native PROPERTIES POSITION_INDEPENDENT_CODE ON)
if(HELLM_ENABLE_VULKAN)
    if(NOT EXISTS "${HELLM_VULKAN_INCLUDE_DIR}/vulkan/vulkan.h")
        message(FATAL_ERROR "HELLM_ENABLE_VULKAN requires HELLM_VULKAN_INCLUDE_DIR containing vulkan/vulkan.h")
    endif()
    if(NOT EXISTS "${HELLM_GLSLC}")
        message(FATAL_ERROR "HELLM_ENABLE_VULKAN requires HELLM_GLSLC pointing to glslc")
    endif()
    # Isolate graphics headers from SDK sysroots that also contain incompatible C library headers.
    set(HELLM_VULKAN_HEADERS "${CMAKE_CURRENT_BINARY_DIR}/vulkan-headers")
    file(MAKE_DIRECTORY "${HELLM_VULKAN_HEADERS}")
    file(COPY "${HELLM_VULKAN_INCLUDE_DIR}/vulkan" DESTINATION "${HELLM_VULKAN_HEADERS}")
    if(EXISTS "${HELLM_VULKAN_INCLUDE_DIR}/vk_video")
        file(COPY "${HELLM_VULKAN_INCLUDE_DIR}/vk_video" DESTINATION "${HELLM_VULKAN_HEADERS}")
    endif()
    set(HELLM_SPIRV "${CMAKE_CURRENT_BINARY_DIR}/Transport.spv")
    set(HELLM_SPIRV_HEADER "${CMAKE_CURRENT_BINARY_DIR}/TransportSpirv.h")
    add_custom_command(OUTPUT "${HELLM_SPIRV_HEADER}"
        COMMAND "${HELLM_GLSLC}" --target-env=vulkan1.2 -O
            "${CMAKE_CURRENT_SOURCE_DIR}/src/vulkan/transport.comp" -o "${HELLM_SPIRV}"
        COMMAND "${CMAKE_COMMAND}" "-DINPUT=${HELLM_SPIRV}" "-DOUTPUT=${HELLM_SPIRV_HEADER}"
            -P "${CMAKE_CURRENT_SOURCE_DIR}/src/vulkan/EmbedSpirv.cmake"
        DEPENDS src/vulkan/transport.comp src/vulkan/EmbedSpirv.cmake
        BYPRODUCTS "${HELLM_SPIRV}"
        VERBATIM)
    target_sources(helengine_lightmap_native PRIVATE src/vulkan/VulkanTransport.cpp
        src/VulkanTracing.cpp "${HELLM_SPIRV_HEADER}")
    target_include_directories(helengine_lightmap_native PRIVATE "${HELLM_VULKAN_HEADERS}" "${CMAKE_CURRENT_BINARY_DIR}")
    target_compile_definitions(helengine_lightmap_native PUBLIC HELLM_ENABLE_VULKAN=1)
    target_link_libraries(helengine_lightmap_native PRIVATE ${CMAKE_DL_LIBS})
endif()
if(MSVC)
    target_compile_options(helengine_lightmap_native PRIVATE /W4 /permissive- $<$<CONFIG:Debug>:/O2> $<$<CONFIG:Debug>:/Zi>)
else()
    target_compile_options(helengine_lightmap_native PRIVATE -Wall -Wextra -Wpedantic)
endif()
if(HELLM_BUILD_CLI)
    add_executable(helengine-lightmap src/Main.cpp)
    target_link_libraries(helengine-lightmap PRIVATE helengine_lightmap_native)
endif()
if(HELENGINE_LIGHTMAP_TESTS AND HELLM_BUILD_TESTS)
    enable_testing()
    add_executable(helengine-lightmap-tests tests/Tests.cpp)
    target_link_libraries(helengine-lightmap-tests PRIVATE helengine_lightmap_native)
    add_test(NAME hellm.correctness COMMAND helengine-lightmap-tests)
    add_executable(helengine-reflection-tests tests/ProbeTests.cpp)
    target_link_libraries(helengine-reflection-tests PRIVATE helengine_lightmap_native)
    add_test(NAME hellm.reflection COMMAND helengine-reflection-tests)
    if(HELLM_BUILD_CLI)
        add_test(NAME hellm.cli COMMAND "${CMAKE_COMMAND}"
            "-DCLI=$<TARGET_FILE:helengine-lightmap>"
            "-DTESTS=$<TARGET_FILE:helengine-lightmap-tests>"
            "-DOUTPUT_DIRECTORY=${CMAKE_CURRENT_BINARY_DIR}"
            "-DENABLE_VULKAN=${HELLM_ENABLE_VULKAN}"
            -P "${CMAKE_CURRENT_SOURCE_DIR}/tests/Cli.cmake")
    endif()
    if(HELLM_ENABLE_VULKAN)
        add_test(NAME hellm.vulkan COMMAND helengine-lightmap-tests --vulkan)
        add_test(NAME hellm.reflection.vulkan COMMAND helengine-reflection-tests --vulkan)
    endif()
endif()

