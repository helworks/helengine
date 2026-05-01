#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EngineBinaryHeader;
class EngineBinaryHeaderSerializer;
class EngineBinaryWriter;
class Asset;
class EngineBinaryReader;
class TextureAsset;
class ModelAsset;
class TextAsset;
class MaterialAsset;
class SceneAsset;
class SceneEntityAsset;
class SceneComponentAssetRecord;
class MaterialRenderState;
class MaterialConstantBufferAsset;
class ShaderAsset;
class ShaderProgramAsset;
class ShaderBinaryAsset;
class ShaderBindingAsset;
class ShaderConstantMemberAsset;
class ShaderVariantAsset;
class ShaderVertexElementAsset;
class float2;
class float3;
class float4;

#include "runtime/native_exceptions.hpp"
#include "EditorAssetBinaryValueKind.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "EngineBinaryWriter.hpp"
#include "EngineBinaryWriter.hpp"
#include "Asset.hpp"
#include "EngineBinaryReader.hpp"
#include "EngineBinaryReader.hpp"
#include "runtime/native_exceptions.hpp"
#include "EditorAssetBinaryValueKind.hpp"
#include "TextureAsset.hpp"
#include "ModelAsset.hpp"
#include "TextAsset.hpp"
#include "MaterialAsset.hpp"
#include "MaterialAsset.hpp"
#include "SceneAsset.hpp"
#include "SceneEntityAsset.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "MaterialRenderState.hpp"
#include "runtime/native_string.hpp"
#include "MaterialConstantBufferAsset.hpp"
#include "ShaderAsset.hpp"
#include "ShaderProgramAsset.hpp"
#include "ShaderBinaryAsset.hpp"
#include "ShaderBindingAsset.hpp"
#include "ShaderConstantMemberAsset.hpp"
#include "ShaderVariantAsset.hpp"
#include "ShaderVertexElementAsset.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "EditorBinaryRecordKind.hpp"
#include "EngineBinaryEndianness.hpp"
#include "Asset.hpp"
#include "system/io/stream.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "float4.hpp"
#include "MaterialConstantBufferAsset.hpp"
#include "MaterialRenderState.hpp"
#include "ModelAsset.hpp"
#include "SceneAsset.hpp"
#include "SceneComponentAssetRecord.hpp"
#include "SceneEntityAsset.hpp"
#include "ShaderAsset.hpp"
#include "ShaderBinaryAsset.hpp"
#include "ShaderBindingAsset.hpp"
#include "ShaderConstantMemberAsset.hpp"
#include "ShaderProgramAsset.hpp"
#include "ShaderVariantAsset.hpp"
#include "ShaderVertexElementAsset.hpp"
#include "TextAsset.hpp"
#include "TextureAsset.hpp"

class EditorAssetBinarySerializer
{
public:
    static uint8_t CurrentVersion;

    static uint16_t FormatId;

    static ::EditorBinaryRecordKind RecordKind;

    static ::Asset* Deserialize(::Stream* stream);

    static ::Asset* Deserialize(::Stream* stream, ::EngineBinaryHeader* header);

    static void Serialize(::Stream* stream, ::Asset* asset);
private:
    static ::EngineBinaryEndianness PayloadEndianness;

    static ::EditorAssetBinaryValueKind GetValueKind(::Asset* asset);

    static ::Asset* ReadAssetPayload(::EngineBinaryReader* reader, ::EditorAssetBinaryValueKind valueKind);

    static ::float2 ReadFloat2(::EngineBinaryReader* reader);

    static ::float3 ReadFloat3(::EngineBinaryReader* reader);

    static ::float4 ReadFloat4(::EngineBinaryReader* reader);

    static ::MaterialAsset* ReadMaterialAsset(::EngineBinaryReader* reader);

    static ::MaterialConstantBufferAsset* ReadMaterialConstantBufferAsset(::EngineBinaryReader* reader);

    static ::MaterialRenderState* ReadMaterialRenderState(::EngineBinaryReader* reader);

    static ::ModelAsset* ReadModelAsset(::EngineBinaryReader* reader);

    static ::SceneAsset* ReadSceneAsset(::EngineBinaryReader* reader);

    static ::SceneComponentAssetRecord* ReadSceneComponentAssetRecord(::EngineBinaryReader* reader);

    static ::SceneEntityAsset* ReadSceneEntityAsset(::EngineBinaryReader* reader);

    static ::ShaderAsset* ReadShaderAsset(::EngineBinaryReader* reader);

    static ::ShaderBinaryAsset* ReadShaderBinaryAsset(::EngineBinaryReader* reader);

    static ::ShaderBindingAsset* ReadShaderBindingAsset(::EngineBinaryReader* reader);

    static ::ShaderConstantMemberAsset* ReadShaderConstantMemberAsset(::EngineBinaryReader* reader);

    static ::ShaderProgramAsset* ReadShaderProgramAsset(::EngineBinaryReader* reader);

    static ::ShaderVariantAsset* ReadShaderVariantAsset(::EngineBinaryReader* reader);

    static ::ShaderVertexElementAsset* ReadShaderVertexElementAsset(::EngineBinaryReader* reader);

    static std::string ReadStringValue(::EngineBinaryReader* reader);

    static ::TextAsset* ReadTextAsset(::EngineBinaryReader* reader);

    static ::TextureAsset* ReadTextureAsset(::EngineBinaryReader* reader);

    static uint16_t ReadUInt16Value(::EngineBinaryReader* reader);

    static uint32_t ReadUInt32Value(::EngineBinaryReader* reader);

    static void ValidateHeader(::EngineBinaryHeader* header);

    static void WriteAssetPayload(::EngineBinaryWriter* writer, ::Asset* asset);

    static void WriteFloat2(::EngineBinaryWriter* writer, ::float2 value);

    static void WriteFloat3(::EngineBinaryWriter* writer, ::float3 value);

    static void WriteFloat4(::EngineBinaryWriter* writer, ::float4 value);

    static void WriteMaterialAsset(::EngineBinaryWriter* writer, ::MaterialAsset* asset);

    static void WriteMaterialConstantBufferAsset(::EngineBinaryWriter* writer, ::MaterialConstantBufferAsset* asset);

    static void WriteMaterialRenderState(::EngineBinaryWriter* writer, ::MaterialRenderState* renderState);

    static void WriteModelAsset(::EngineBinaryWriter* writer, ::ModelAsset* asset);

    static void WriteSceneAsset(::EngineBinaryWriter* writer, ::SceneAsset* asset);

    static void WriteSceneComponentAssetRecord(::EngineBinaryWriter* writer, ::SceneComponentAssetRecord* record);

    static void WriteSceneEntityAsset(::EngineBinaryWriter* writer, ::SceneEntityAsset* asset);

    static void WriteShaderAsset(::EngineBinaryWriter* writer, ::ShaderAsset* asset);

    static void WriteShaderBinaryAsset(::EngineBinaryWriter* writer, ::ShaderBinaryAsset* asset);

    static void WriteShaderBindingAsset(::EngineBinaryWriter* writer, ::ShaderBindingAsset* asset);

    static void WriteShaderConstantMemberAsset(::EngineBinaryWriter* writer, ::ShaderConstantMemberAsset* asset);

    static void WriteShaderProgramAsset(::EngineBinaryWriter* writer, ::ShaderProgramAsset* asset);

    static void WriteShaderVariantAsset(::EngineBinaryWriter* writer, ::ShaderVariantAsset* asset);

    static void WriteShaderVertexElementAsset(::EngineBinaryWriter* writer, ::ShaderVertexElementAsset* asset);

    static void WriteStringValue(::EngineBinaryWriter* writer, std::string value);

    static void WriteTextAsset(::EngineBinaryWriter* writer, ::TextAsset* asset);

    static void WriteTextureAsset(::EngineBinaryWriter* writer, ::TextureAsset* asset);

    static void WriteUInt16Value(::EngineBinaryWriter* writer, uint16_t value);

    static void WriteUInt32Value(::EngineBinaryWriter* writer, uint32_t value);
};
