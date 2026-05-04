#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;
class ShaderModuleDefinition;
class ShaderTargetNames;
class ShaderBinaryAsset;
class ShaderProgramAsset;
class ShaderProgramBinary;
class ShaderProgramDefinition;

#include "Asset.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ShaderModuleDefinition.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "ShaderTargetNames.hpp"
#include "ShaderTargetNames.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ShaderBinaryAsset.hpp"
#include "runtime/array.hpp"
#include "ShaderProgramAsset.hpp"
#include "ShaderModuleDefinition.hpp"
#include "ShaderCompileTarget.hpp"
#include "runtime/array.hpp"
#include "ShaderProgramBinary.hpp"
#include "runtime/array.hpp"
#include "ShaderProgramDefinition.hpp"

class ShaderAsset : public Asset
{
public:
    virtual ~ShaderAsset() = default;

    ShaderAsset();

    Array<::ShaderBinaryAsset*>* Binaries;

    std::string Name;

    Array<::ShaderProgramAsset*>* Programs;

    std::string TargetName;

    ::ShaderModuleDefinition* BuildDefinition();

    static ::ShaderAsset* FromDefinition(::ShaderModuleDefinition* definition, ::ShaderCompileTarget target);

    std::string get_Id();

    void set_Id(std::string value);
private:
    static Array<::ShaderBinaryAsset*>* BuildBinaryAssets(::ShaderModuleDefinition* definition, std::string targetName);

    Array<::ShaderProgramBinary*>* BuildBinaryDefinitions();

    static Array<::ShaderProgramAsset*>* BuildProgramAssets(::ShaderModuleDefinition* definition);

    Array<::ShaderProgramDefinition*>* BuildProgramDefinitions();

    void Validate();
};
