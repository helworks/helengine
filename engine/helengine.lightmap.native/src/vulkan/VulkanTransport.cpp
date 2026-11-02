#define VK_NO_PROTOTYPES
#include "VulkanTransportImplementation.h"
#include "TransportSpirv.h"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <stdexcept>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#else
#include <dlfcn.h>
#endif

namespace hellm::detail {
/// <summary>Surface all Vulkan failures with the operation and numeric driver result.</summary>
static void Check(VkResult result, const char *operation) {
    if (result != VK_SUCCESS) throw std::runtime_error(std::string("Vulkan ") + operation + " failed (VkResult " + std::to_string(result) + ")");
}

VulkanTransport::Implementation::~Implementation() {
    if (Device && DeviceWaitIdle) DeviceWaitIdle(Device);
    if (Pipeline) DestroyPipeline(Device, Pipeline, nullptr);
    if (PipelineLayout) DestroyPipelineLayout(Device, PipelineLayout, nullptr);
    if (DescriptorPool) DestroyDescriptorPool(Device, DescriptorPool, nullptr);
    if (DescriptorLayout) DestroyDescriptorSetLayout(Device, DescriptorLayout, nullptr);
    if (Tlas) DestroyAccelerationStructureKHR(Device, Tlas, nullptr);
    if (Blas) DestroyAccelerationStructureKHR(Device, Blas, nullptr);
    Output.reset(); Coverages.reset(); Texels.reset(); SceneBuffers.clear();
    if (Fence) DestroyFence(Device, Fence, nullptr);
    if (CommandPool) DestroyCommandPool(Device, CommandPool, nullptr);
    if (Device && DestroyDevice) DestroyDevice(Device, nullptr);
    if (Instance && DestroyInstance) DestroyInstance(Instance, nullptr);
#ifdef _WIN32
    if (Library) FreeLibrary(static_cast<HMODULE>(Library));
#else
    if (Library) dlclose(Library);
#endif
}

void VulkanTransport::Implementation::Initialize(const GpuScene &scene) {
    if (scene.Triangles.empty() || scene.Materials.empty()) throw std::invalid_argument("Vulkan scene requires triangles and materials");
    for (const auto &material : scene.Materials) {
        uint64_t width = material.Texture[0], height = material.Texture[1], offset = material.Texture[2];
        if ((width == 0) != (height == 0) || width > INT32_MAX || height > INT32_MAX || offset > scene.Pixels.size() || width * height > scene.Pixels.size() - offset)
            throw std::invalid_argument("Vulkan material texture range is invalid");
        for (size_t component = 0; component < 3; ++component)
            if (!std::isfinite(material.Albedo[component]) || material.Albedo[component] < 0) throw std::invalid_argument("Vulkan material albedo is invalid");
    }
#ifdef _WIN32
    Library = LoadLibraryExW(L"vulkan-1.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (Library) GetInstanceProcAddr = reinterpret_cast<PFN_vkGetInstanceProcAddr>(GetProcAddress(static_cast<HMODULE>(Library), "vkGetInstanceProcAddr"));
#else
    Library = dlopen("libvulkan.so.1", RTLD_NOW | RTLD_LOCAL);
    if (Library) GetInstanceProcAddr = reinterpret_cast<PFN_vkGetInstanceProcAddr>(dlsym(Library, "vkGetInstanceProcAddr"));
#endif
    if (!GetInstanceProcAddr) throw std::runtime_error("Vulkan backend unavailable: platform Vulkan loader was not found");
    auto enumerateVersion = reinterpret_cast<PFN_vkEnumerateInstanceVersion>(GetInstanceProcAddr(VK_NULL_HANDLE, "vkEnumerateInstanceVersion"));
    uint32_t version = VK_API_VERSION_1_0;
    if (enumerateVersion) Check(enumerateVersion(&version), "enumerate instance version");
    if (version < VK_API_VERSION_1_2) throw std::runtime_error("Vulkan backend requires Vulkan 1.2 or newer");
    auto createInstance = reinterpret_cast<PFN_vkCreateInstance>(GetInstanceProcAddr(VK_NULL_HANDLE, "vkCreateInstance"));
    if (!createInstance) throw std::runtime_error("Vulkan loader is missing vkCreateInstance");
    VkApplicationInfo application{VK_STRUCTURE_TYPE_APPLICATION_INFO};
    application.pApplicationName = "Helengine lightmap baker";
    application.apiVersion = VK_API_VERSION_1_2;
    VkInstanceCreateInfo instanceInfo{VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO};
    instanceInfo.pApplicationInfo = &application;
    Check(createInstance(&instanceInfo, nullptr, &Instance), "create instance");
#define HELLM_LOAD_INSTANCE(name) name = reinterpret_cast<PFN_vk##name>(GetInstanceProcAddr(Instance, "vk" #name)); if (!name) throw std::runtime_error("Vulkan loader missing vk" #name);
    HELLM_VULKAN_INSTANCE_FUNCTIONS(HELLM_LOAD_INSTANCE)
#undef HELLM_LOAD_INSTANCE
    uint32_t deviceCount = 0;
    Check(EnumeratePhysicalDevices(Instance, &deviceCount, nullptr), "enumerate physical devices");
    std::vector<VkPhysicalDevice> devices(deviceCount);
    Check(EnumeratePhysicalDevices(Instance, &deviceCount, devices.data()), "read physical devices");
    const char *required[] = {VK_KHR_ACCELERATION_STRUCTURE_EXTENSION_NAME, VK_KHR_RAY_QUERY_EXTENSION_NAME, VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME};
    int bestScore = -1;
    for (VkPhysicalDevice candidate : devices) {
        VkPhysicalDeviceProperties properties{};
        GetPhysicalDeviceProperties(candidate, &properties);
        if (properties.apiVersion < VK_API_VERSION_1_2) continue;
        uint32_t extensionCount = 0;
        Check(EnumerateDeviceExtensionProperties(candidate, nullptr, &extensionCount, nullptr), "enumerate device extensions");
        std::vector<VkExtensionProperties> extensions(extensionCount);
        Check(EnumerateDeviceExtensionProperties(candidate, nullptr, &extensionCount, extensions.data()), "read device extensions");
        bool supported = true;
        for (const char *name : required) {
            bool found = false;
            for (const auto &extension : extensions) if (std::strcmp(extension.extensionName, name) == 0) found = true;
            if (!found) supported = false;
        }
        if (!supported) continue;
        VkPhysicalDeviceRayQueryFeaturesKHR ray{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_QUERY_FEATURES_KHR};
        VkPhysicalDeviceAccelerationStructureFeaturesKHR acceleration{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_ACCELERATION_STRUCTURE_FEATURES_KHR};
        VkPhysicalDeviceBufferDeviceAddressFeatures address{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_BUFFER_DEVICE_ADDRESS_FEATURES};
        VkPhysicalDeviceFeatures2 features{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2};
        features.pNext = &address; address.pNext = &acceleration; acceleration.pNext = &ray;
        GetPhysicalDeviceFeatures2(candidate, &features);
        if (!features.features.shaderInt64 || !address.bufferDeviceAddress || !acceleration.accelerationStructure || !ray.rayQuery) continue;
        uint32_t familyCount = 0;
        GetPhysicalDeviceQueueFamilyProperties(candidate, &familyCount, nullptr);
        std::vector<VkQueueFamilyProperties> families(familyCount);
        GetPhysicalDeviceQueueFamilyProperties(candidate, &familyCount, families.data());
        for (uint32_t family = 0; family < familyCount; ++family) {
            if (!families[family].queueCount || !(families[family].queueFlags & VK_QUEUE_COMPUTE_BIT)) continue;
            int score = properties.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU ? 100 : 1;
            if (score > bestScore) { bestScore = score; PhysicalDevice = candidate; QueueFamily = family; Properties = properties; }
            break;
        }
    }
    if (!PhysicalDevice) throw std::runtime_error("Vulkan backend unavailable: no Vulkan 1.2 compute device supports accelerationStructure, rayQuery, bufferDeviceAddress and shaderInt64");
    Name = Properties.deviceName;
    if (Properties.limits.maxPushConstantsSize < sizeof(GpuParameters) || Properties.limits.maxComputeWorkGroupInvocations < 64 || Properties.limits.maxComputeWorkGroupSize[0] < 64 || Properties.limits.maxPerStageDescriptorStorageBuffers < 6)
        throw std::runtime_error("Vulkan device limits do not support the irradiance shader");
    VkPhysicalDeviceProperties2 properties2{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2};
    properties2.pNext = &AccelerationProperties;
    GetPhysicalDeviceProperties2(PhysicalDevice, &properties2);
    if (scene.Triangles.size() > AccelerationProperties.maxPrimitiveCount || scene.Triangles.size() > std::numeric_limits<uint32_t>::max() / 3)
        throw std::runtime_error("Vulkan scene exceeds acceleration-structure primitive limits");
    GetPhysicalDeviceMemoryProperties(PhysicalDevice, &MemoryProperties);
    float priority = 1.0f;
    VkDeviceQueueCreateInfo queueInfo{VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO};
    queueInfo.queueFamilyIndex = QueueFamily; queueInfo.queueCount = 1; queueInfo.pQueuePriorities = &priority;
    VkPhysicalDeviceRayQueryFeaturesKHR ray{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_QUERY_FEATURES_KHR}; ray.rayQuery = VK_TRUE;
    VkPhysicalDeviceAccelerationStructureFeaturesKHR acceleration{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_ACCELERATION_STRUCTURE_FEATURES_KHR}; acceleration.accelerationStructure = VK_TRUE; acceleration.pNext = &ray;
    VkPhysicalDeviceBufferDeviceAddressFeatures address{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_BUFFER_DEVICE_ADDRESS_FEATURES}; address.bufferDeviceAddress = VK_TRUE; address.pNext = &acceleration;
    VkPhysicalDeviceFeatures features{}; features.shaderInt64 = VK_TRUE;
    VkDeviceCreateInfo deviceInfo{VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO};
    deviceInfo.pNext = &address; deviceInfo.queueCreateInfoCount = 1; deviceInfo.pQueueCreateInfos = &queueInfo;
    deviceInfo.enabledExtensionCount = 3; deviceInfo.ppEnabledExtensionNames = required; deviceInfo.pEnabledFeatures = &features;
    Check(CreateDevice(PhysicalDevice, &deviceInfo, nullptr, &Device), "create device");
#define HELLM_LOAD_DEVICE(name) name = reinterpret_cast<PFN_vk##name>(GetDeviceProcAddr(Device, "vk" #name)); if (!name) throw std::runtime_error("Vulkan device missing vk" #name);
    HELLM_VULKAN_DEVICE_FUNCTIONS(HELLM_LOAD_DEVICE)
#undef HELLM_LOAD_DEVICE
    GetDeviceQueue(Device, QueueFamily, 0, &Queue);
    VkCommandPoolCreateInfo poolInfo{VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO}; poolInfo.queueFamilyIndex = QueueFamily;
    Check(CreateCommandPool(Device, &poolInfo, nullptr, &CommandPool), "create command pool");
    VkCommandBufferAllocateInfo commandInfo{VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO}; commandInfo.commandPool = CommandPool; commandInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY; commandInfo.commandBufferCount = 1;
    Check(AllocateCommandBuffers(Device, &commandInfo, &Command), "allocate command buffer");
    VkFenceCreateInfo fenceInfo{VK_STRUCTURE_TYPE_FENCE_CREATE_INFO};
    Check(CreateFence(Device, &fenceInfo, nullptr, &Fence), "create completion fence");
    Triangles = Upload(scene.Triangles.data(), scene.Triangles.size() * sizeof(GpuTriangle), VK_BUFFER_USAGE_STORAGE_BUFFER_BIT);
    Materials = Upload(scene.Materials.data(), scene.Materials.size() * sizeof(GpuMaterial), VK_BUFFER_USAGE_STORAGE_BUFFER_BIT);
    Pixels = Upload(scene.Pixels.data(), scene.Pixels.size() * sizeof(scene.Pixels[0]), VK_BUFFER_USAGE_STORAGE_BUFFER_BIT);
    std::vector<float> vertices; vertices.reserve(scene.Triangles.size() * 9);
    for (const auto &triangle : scene.Triangles) {
        if (triangle.Metadata[0] >= scene.Materials.size()) throw std::invalid_argument("Vulkan triangle references an invalid material");
        for (const float *point : {triangle.A, triangle.B, triangle.C}) for (size_t i = 0; i < 3; ++i) {
            if (!std::isfinite(point[i])) throw std::invalid_argument("Vulkan geometry contains a non-finite position");
            vertices.push_back(point[i]);
        }
    }
    auto vertexBuffer = Upload(vertices.data(), vertices.size() * sizeof(float), VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR | VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT);
    VkAccelerationStructureGeometryKHR geometry{VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR};
    geometry.geometryType = VK_GEOMETRY_TYPE_TRIANGLES_KHR; geometry.flags = VK_GEOMETRY_OPAQUE_BIT_KHR;
    auto &triangles = geometry.geometry.triangles; triangles.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR;
    triangles.vertexFormat = VK_FORMAT_R32G32B32_SFLOAT; triangles.vertexData.deviceAddress = Address(*vertexBuffer); triangles.vertexStride = 3 * sizeof(float);
    triangles.maxVertex = static_cast<uint32_t>(scene.Triangles.size() * 3 - 1); triangles.indexType = VK_INDEX_TYPE_NONE_KHR;
    Blas = Build(VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR, geometry, static_cast<uint32_t>(scene.Triangles.size()));
    VkAccelerationStructureDeviceAddressInfoKHR addressInfo{VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR}; addressInfo.accelerationStructure = Blas;
    VkAccelerationStructureInstanceKHR instance{};
    instance.transform.matrix[0][0] = 1; instance.transform.matrix[1][1] = 1; instance.transform.matrix[2][2] = 1;
    instance.mask = 0xff; instance.flags = VK_GEOMETRY_INSTANCE_TRIANGLE_FACING_CULL_DISABLE_BIT_KHR;
    instance.accelerationStructureReference = GetAccelerationStructureDeviceAddressKHR(Device, &addressInfo);
    if (!instance.accelerationStructureReference) throw std::runtime_error("Vulkan BLAS device address is zero");
    auto instances = Upload(&instance, sizeof(instance), VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR | VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT);
    VkAccelerationStructureGeometryKHR top{VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR}; top.geometryType = VK_GEOMETRY_TYPE_INSTANCES_KHR;
    top.geometry.instances.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR;
    top.geometry.instances.data.deviceAddress = Address(*instances);
    Tlas = Build(VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR, top, 1);
    CreatePipeline();
}

std::unique_ptr<VulkanBuffer> VulkanTransport::Implementation::CreateBufferAllocation(VkDeviceSize size, VkBufferUsageFlags usage, bool host, bool descriptorBound) {
    size = std::max<VkDeviceSize>(16, size);
    if (descriptorBound && size > Properties.limits.maxStorageBufferRange) throw std::runtime_error("Vulkan storage buffer exceeds maxStorageBufferRange");
    auto buffer = std::make_unique<VulkanBuffer>();
    buffer->Device = Device; buffer->Size = size; buffer->DestroyBuffer = DestroyBuffer; buffer->FreeMemory = FreeMemory; buffer->UnmapMemory = UnmapMemory;
    VkBufferCreateInfo info{VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO}; info.size = size; info.usage = usage; info.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
    Check(CreateBuffer(Device, &info, nullptr, &buffer->Buffer), "create buffer");
    VkMemoryRequirements requirements{}; GetBufferMemoryRequirements(Device, buffer->Buffer, &requirements);
    VkMemoryPropertyFlags flags = host ? VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT : VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
    uint32_t type = MemoryProperties.memoryTypeCount;
    for (uint32_t i = 0; i < MemoryProperties.memoryTypeCount; ++i) {
        if ((requirements.memoryTypeBits & (1u << i)) && (MemoryProperties.memoryTypes[i].propertyFlags & flags) == flags && requirements.size <= MemoryProperties.memoryHeaps[MemoryProperties.memoryTypes[i].heapIndex].size) { type = i; break; }
    }
    if (type == MemoryProperties.memoryTypeCount) throw std::runtime_error("Vulkan device has no compatible memory type with sufficient heap capacity");
    VkMemoryAllocateFlagsInfo allocationFlags{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO};
    allocationFlags.flags = (usage & VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT) ? VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT : 0;
    VkMemoryAllocateInfo allocation{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO}; allocation.pNext = &allocationFlags; allocation.allocationSize = requirements.size; allocation.memoryTypeIndex = type;
    Check(AllocateMemory(Device, &allocation, nullptr, &buffer->Memory), "allocate buffer memory");
    Check(BindBufferMemory(Device, buffer->Buffer, buffer->Memory, 0), "bind buffer memory");
    if (host) Check(MapMemory(Device, buffer->Memory, 0, VK_WHOLE_SIZE, 0, &buffer->Mapping), "map coherent buffer");
    return buffer;
}

VulkanBuffer *VulkanTransport::Implementation::Upload(const void *data, VkDeviceSize bytes, VkBufferUsageFlags usage) {
    auto buffer = CreateBufferAllocation(bytes, usage, true, (usage & VK_BUFFER_USAGE_STORAGE_BUFFER_BIT) != 0);
    std::memset(buffer->Mapping, 0, static_cast<size_t>(buffer->Size));
    if (bytes) std::memcpy(buffer->Mapping, data, static_cast<size_t>(bytes));
    auto result = buffer.get(); SceneBuffers.push_back(std::move(buffer)); return result;
}

VkDeviceAddress VulkanTransport::Implementation::Address(const VulkanBuffer &buffer) const {
    VkBufferDeviceAddressInfo info{VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO}; info.buffer = buffer.Buffer;
    VkDeviceAddress result = GetBufferDeviceAddress(Device, &info);
    if (!result) throw std::runtime_error("Vulkan buffer device address is zero");
    return result;
}

void VulkanTransport::Implementation::Begin() {
    Check(ResetCommandPool(Device, CommandPool, 0), "reset command pool");
    Check(ResetFences(Device, 1, &Fence), "reset completion fence");
    VkCommandBufferBeginInfo begin{VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO}; begin.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
    Check(BeginCommandBuffer(Command, &begin), "begin command buffer");
}

void VulkanTransport::Implementation::Submit() {
    Check(EndCommandBuffer(Command), "end command buffer");
    VkSubmitInfo submission{VK_STRUCTURE_TYPE_SUBMIT_INFO}; submission.commandBufferCount = 1; submission.pCommandBuffers = &Command;
    Check(QueueSubmit(Queue, 1, &submission, Fence), "submit compute work");
    Check(WaitForFences(Device, 1, &Fence, VK_TRUE, 60000000000ull), "wait for GPU completion (60-second limit)");
}

VkAccelerationStructureKHR VulkanTransport::Implementation::Build(VkAccelerationStructureTypeKHR type,
    const VkAccelerationStructureGeometryKHR &geometry, uint32_t primitives) {
    VkAccelerationStructureBuildGeometryInfoKHR build{VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR};
    build.type = type; build.flags = VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR; build.mode = VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR; build.geometryCount = 1; build.pGeometries = &geometry;
    VkAccelerationStructureBuildSizesInfoKHR sizes{VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR};
    GetAccelerationStructureBuildSizesKHR(Device, VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR, &build, &primitives, &sizes);
    if (!sizes.accelerationStructureSize || !sizes.buildScratchSize) throw std::runtime_error("Vulkan returned an invalid acceleration-structure build size");
    auto backing = CreateBufferAllocation(sizes.accelerationStructureSize, VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR, false, false);
    VkAccelerationStructureCreateInfoKHR info{VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR}; info.buffer = backing->Buffer; info.size = sizes.accelerationStructureSize; info.type = type;
    VkAccelerationStructureKHR result = VK_NULL_HANDLE;
    Check(CreateAccelerationStructureKHR(Device, &info, nullptr, &result), "create acceleration structure");
    // Retain the handle immediately so failure in scratch allocation or submission is also cleaned up.
    if (type == VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR) Blas = result; else Tlas = result;
    SceneBuffers.push_back(std::move(backing));
    VkDeviceSize alignment = AccelerationProperties.minAccelerationStructureScratchOffsetAlignment;
    if (!alignment || sizes.buildScratchSize > std::numeric_limits<VkDeviceSize>::max() - alignment) throw std::runtime_error("Vulkan scratch allocation size is invalid");
    auto scratch = CreateBufferAllocation(sizes.buildScratchSize + alignment, VK_BUFFER_USAGE_STORAGE_BUFFER_BIT | VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT, false, false);
    VkDeviceAddress address = Address(*scratch);
    build.scratchData.deviceAddress = ((address + alignment - 1) / alignment) * alignment;
    build.dstAccelerationStructure = result;
    VkAccelerationStructureBuildRangeInfoKHR range{}; range.primitiveCount = primitives;
    const VkAccelerationStructureBuildRangeInfoKHR *ranges[] = {&range};
    Begin();
    VkMemoryBarrier input{VK_STRUCTURE_TYPE_MEMORY_BARRIER}; input.srcAccessMask = VK_ACCESS_HOST_WRITE_BIT | VK_ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR; input.dstAccessMask = VK_ACCESS_SHADER_READ_BIT | VK_ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR;
    CmdPipelineBarrier(Command, VK_PIPELINE_STAGE_HOST_BIT | VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, 0, 1, &input, 0, nullptr, 0, nullptr);
    CmdBuildAccelerationStructuresKHR(Command, 1, &build, ranges);
    VkMemoryBarrier complete{VK_STRUCTURE_TYPE_MEMORY_BARRIER}; complete.srcAccessMask = VK_ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR; complete.dstAccessMask = VK_ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR;
    CmdPipelineBarrier(Command, VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR | VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, 0, 1, &complete, 0, nullptr, 0, nullptr);
    // Scratch must remain alive until the fence signals, including an exceptional wait result.
    SceneBuffers.push_back(std::move(scratch));
    Submit();
    SceneBuffers.pop_back();
    return result;
}

void VulkanTransport::Implementation::CreatePipeline() {
    VkDescriptorSetLayoutBinding bindings[7]{};
    for (uint32_t i = 0; i < 7; ++i) { bindings[i].binding = i; bindings[i].descriptorCount = 1; bindings[i].descriptorType = i ? VK_DESCRIPTOR_TYPE_STORAGE_BUFFER : VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR; bindings[i].stageFlags = VK_SHADER_STAGE_COMPUTE_BIT; }
    VkDescriptorSetLayoutCreateInfo layout{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO}; layout.bindingCount = 7; layout.pBindings = bindings;
    Check(CreateDescriptorSetLayout(Device, &layout, nullptr, &DescriptorLayout), "create descriptor layout");
    VkDescriptorPoolSize sizes[] = {{VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, 1}, {VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, 6}};
    VkDescriptorPoolCreateInfo pool{VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO}; pool.maxSets = 1; pool.poolSizeCount = 2; pool.pPoolSizes = sizes;
    Check(CreateDescriptorPool(Device, &pool, nullptr, &DescriptorPool), "create descriptor pool");
    VkDescriptorSetAllocateInfo allocation{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO}; allocation.descriptorPool = DescriptorPool; allocation.descriptorSetCount = 1; allocation.pSetLayouts = &DescriptorLayout;
    Check(AllocateDescriptorSets(Device, &allocation, &DescriptorSet), "allocate descriptor set");
    VkPushConstantRange push{}; push.stageFlags = VK_SHADER_STAGE_COMPUTE_BIT; push.size = sizeof(GpuParameters);
    VkPipelineLayoutCreateInfo pipelineLayout{VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO}; pipelineLayout.setLayoutCount = 1; pipelineLayout.pSetLayouts = &DescriptorLayout; pipelineLayout.pushConstantRangeCount = 1; pipelineLayout.pPushConstantRanges = &push;
    Check(CreatePipelineLayout(Device, &pipelineLayout, nullptr, &PipelineLayout), "create pipeline layout");
    VkShaderModuleCreateInfo shaderInfo{VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO}; shaderInfo.codeSize = sizeof(TransportSpirv); shaderInfo.pCode = TransportSpirv;
    VkShaderModule shader = VK_NULL_HANDLE;
    Check(CreateShaderModule(Device, &shaderInfo, nullptr, &shader), "create shader module");
    VkComputePipelineCreateInfo pipeline{VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO}; pipeline.layout = PipelineLayout;
    pipeline.stage.sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO; pipeline.stage.stage = VK_SHADER_STAGE_COMPUTE_BIT; pipeline.stage.module = shader; pipeline.stage.pName = "main";
    VkResult result = CreateComputePipelines(Device, VK_NULL_HANDLE, 1, &pipeline, nullptr, &Pipeline);
    DestroyShaderModule(Device, shader, nullptr);
    Check(result, "create irradiance pipeline");
}

void VulkanTransport::Implementation::Trace(const std::vector<GpuTexel> &texels, const std::vector<GpuCoverage> &coverages,
    const GpuParameters &parameters, std::vector<float> &rgba) {
    if (texels.empty()) { rgba.clear(); return; }
    if (texels.size() > std::numeric_limits<uint32_t>::max() || parameters.Settings[2] != texels.size() || !parameters.Settings[0]) throw std::invalid_argument("Vulkan batch parameter count or sample count is invalid");
    if (coverages.empty()) throw std::invalid_argument("Vulkan occupied texel batch has no coverage");
    for (size_t component = 0; component < 4; ++component)
        if (!std::isfinite(parameters.Sun[component]) || !std::isfinite(parameters.Radiance[component])) throw std::invalid_argument("Vulkan lighting parameters are not finite");
    if (!(parameters.Sun[3] > 0)) throw std::invalid_argument("Vulkan ray epsilon must be positive");
    uint64_t groups = (texels.size() + 63ull) / 64ull;
    if (groups > Properties.limits.maxComputeWorkGroupCount[0]) throw std::invalid_argument("Vulkan batch exceeds compute dispatch limits");
    for (const auto &texel : texels) {
        if (!texel.Metadata[1] || texel.Metadata[0] > coverages.size() || texel.Metadata[1] > coverages.size() - texel.Metadata[0]) throw std::invalid_argument("Vulkan texel coverage range is invalid");
    }
    if (parameters.Seed[2] > 1) throw std::invalid_argument("Vulkan trace mode is invalid");
    for (const auto &coverage : coverages) {
        if (parameters.Seed[2] == 1) {
            double lengthSquared = 0;
            for (size_t component = 0; component < 3; ++component) {
                if (!std::isfinite(coverage.Points[0][component]) || !std::isfinite(coverage.Points[1][component]))
                    throw std::invalid_argument("Vulkan reflection ray is not finite");
                lengthSquared += double(coverage.Points[1][component]) * coverage.Points[1][component];
            }
            if (std::abs(lengthSquared - 1) > 1e-4) throw std::invalid_argument("Vulkan reflection ray direction must be normalized");
        } else {
            if (coverage.Metadata[0] >= Triangles->Size / sizeof(GpuTriangle) || coverage.Metadata[1] < 3 || coverage.Metadata[1] > 12 || !std::isfinite(coverage.Measures[0]) || !std::isfinite(coverage.Measures[1]) || coverage.Measures[0] <= 0 || coverage.Measures[1] <= 0) throw std::invalid_argument("Vulkan coverage polygon is invalid");
            for (uint32_t i = 0; i < coverage.Metadata[1]; ++i)
                for (size_t component = 0; component < 3; ++component)
                    if (!std::isfinite(coverage.Points[i][component]) || (component == 2 && coverage.Points[i][component] < 0)) throw std::invalid_argument("Vulkan coverage vertex is invalid");
        }
    }
    VkDeviceSize texelBytes = texels.size() * sizeof(GpuTexel), coverageBytes = coverages.size() * sizeof(GpuCoverage), outputBytes = texels.size() * 4 * sizeof(float);
    if (!Texels || Texels->Size < texelBytes) Texels = CreateBufferAllocation(texelBytes, VK_BUFFER_USAGE_STORAGE_BUFFER_BIT, true, true);
    if (!Coverages || Coverages->Size < coverageBytes) Coverages = CreateBufferAllocation(coverageBytes, VK_BUFFER_USAGE_STORAGE_BUFFER_BIT, true, true);
    if (!Output || Output->Size < outputBytes) Output = CreateBufferAllocation(outputBytes, VK_BUFFER_USAGE_STORAGE_BUFFER_BIT, true, true);
    std::memcpy(Texels->Mapping, texels.data(), static_cast<size_t>(texelBytes));
    std::memcpy(Coverages->Mapping, coverages.data(), static_cast<size_t>(coverageBytes));
    // Sentinel initialization detects a shader invocation that fails to write its complete result.
    std::fill_n(static_cast<float *>(Output->Mapping), texels.size() * 4, std::numeric_limits<float>::quiet_NaN());
    VkWriteDescriptorSetAccelerationStructureKHR acceleration{VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR}; acceleration.accelerationStructureCount = 1; acceleration.pAccelerationStructures = &Tlas;
    VkWriteDescriptorSet writes[7]{}; VkDescriptorBufferInfo buffers[6]{};
    VulkanBuffer *bound[] = {Triangles, Materials, Pixels, Texels.get(), Coverages.get(), Output.get()};
    for (uint32_t i = 0; i < 7; ++i) {
        writes[i].sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET; writes[i].dstSet = DescriptorSet; writes[i].dstBinding = i; writes[i].descriptorCount = 1;
        writes[i].descriptorType = i ? VK_DESCRIPTOR_TYPE_STORAGE_BUFFER : VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
        if (i) { buffers[i - 1].buffer = bound[i - 1]->Buffer; buffers[i - 1].range = bound[i - 1]->Size; writes[i].pBufferInfo = &buffers[i - 1]; } else writes[i].pNext = &acceleration;
    }
    UpdateDescriptorSets(Device, 7, writes, 0, nullptr);
    Begin();
    VkMemoryBarrier upload{VK_STRUCTURE_TYPE_MEMORY_BARRIER}; upload.srcAccessMask = VK_ACCESS_HOST_WRITE_BIT; upload.dstAccessMask = VK_ACCESS_SHADER_READ_BIT | VK_ACCESS_SHADER_WRITE_BIT;
    CmdPipelineBarrier(Command, VK_PIPELINE_STAGE_HOST_BIT, VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, 0, 1, &upload, 0, nullptr, 0, nullptr);
    CmdBindPipeline(Command, VK_PIPELINE_BIND_POINT_COMPUTE, Pipeline);
    CmdBindDescriptorSets(Command, VK_PIPELINE_BIND_POINT_COMPUTE, PipelineLayout, 0, 1, &DescriptorSet, 0, nullptr);
    CmdPushConstants(Command, PipelineLayout, VK_SHADER_STAGE_COMPUTE_BIT, 0, sizeof(parameters), &parameters);
    CmdDispatch(Command, static_cast<uint32_t>(groups), 1, 1);
    VkMemoryBarrier readback{VK_STRUCTURE_TYPE_MEMORY_BARRIER}; readback.srcAccessMask = VK_ACCESS_SHADER_WRITE_BIT; readback.dstAccessMask = VK_ACCESS_HOST_READ_BIT;
    CmdPipelineBarrier(Command, VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, VK_PIPELINE_STAGE_HOST_BIT, 0, 1, &readback, 0, nullptr, 0, nullptr);
    Submit();
    const float *result = static_cast<const float *>(Output->Mapping);
    rgba.assign(result, result + texels.size() * 4);
    for (float value : rgba) if (!std::isfinite(value)) throw std::runtime_error("Vulkan irradiance shader returned non-finite output");
}

VulkanTransport::VulkanTransport(const GpuScene &scene) : State(std::make_unique<Implementation>()) { State->Initialize(scene); }
VulkanTransport::~VulkanTransport() = default;
std::string VulkanTransport::DeviceName() const { return State->Name; }
void VulkanTransport::Trace(const std::vector<GpuTexel> &texels, const std::vector<GpuCoverage> &coverages,
    const GpuParameters &parameters, std::vector<float> &rgba) { State->Trace(texels, coverages, parameters, rgba); }
} // namespace hellm::detail
