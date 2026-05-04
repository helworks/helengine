#ifdef DrawText
#undef DrawText
#endif
#include "EditorAssetBinarySerializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "EditorAssetBinaryValueKind.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "EngineBinaryWriter.hpp"
#include "Asset.hpp"
#include "EngineBinaryReader.hpp"
#include "TextureAsset.hpp"
#include "ModelAsset.hpp"
#include "TextAsset.hpp"
#include "MaterialAsset.hpp"
#include "SceneAsset.hpp"
#include "SceneEntityAsset.hpp"
#include "SceneAssetReference.hpp"
#include "runtime/array.hpp"
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
#include "runtime/array.hpp"
#include "runtime/native_cast.hpp"
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
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

uint8_t EditorAssetBinarySerializer::CurrentVersion = 4;

uint16_t EditorAssetBinarySerializer::FormatId = 1;

::EditorBinaryRecordKind EditorAssetBinarySerializer::RecordKind = EditorBinaryRecordKind::Asset;

::Asset* EditorAssetBinarySerializer::Deserialize(::Stream* stream)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
::EngineBinaryHeader *header = EngineBinaryHeaderSerializer::Read(stream);
return Deserialize(stream, header);}

::Asset* EditorAssetBinarySerializer::Deserialize(::Stream* stream, ::EngineBinaryHeader* header)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
else     if (header == nullptr)
    {
throw new ArgumentNullException("header");
    }
ValidateHeader(header);
{
::EngineBinaryReader *reader = EngineBinaryReader::Create(stream, header->get_Endianness(), true);
return ReadAssetPayload(reader, static_cast<EditorAssetBinaryValueKind>(header->get_ValueKind()), header->get_Version());}
}

void EditorAssetBinarySerializer::Serialize(::Stream* stream, ::Asset* asset)
{
    if (stream == nullptr)
    {
throw new ArgumentNullException("stream");
    }
else     if (asset == nullptr)
    {
throw new ArgumentNullException("asset");
    }
::EditorAssetBinaryValueKind valueKind = GetValueKind(asset);
::EngineBinaryHeader *header = new ::EngineBinaryHeader(PayloadEndianness, CurrentVersion, FormatId, static_cast<uint16_t>(RecordKind), static_cast<uint16_t>(valueKind));
EngineBinaryHeaderSerializer::Write(stream, header);
{
::EngineBinaryWriter *writer = EngineBinaryWriter::Create(stream, PayloadEndianness, true);
WriteAssetPayload(writer, asset);
}
}

uint8_t EditorAssetBinarySerializer::LegacyVersion = 2;

::EngineBinaryEndianness EditorAssetBinarySerializer::PayloadEndianness = EngineBinaryEndianness::LittleEndian;

uint8_t EditorAssetBinarySerializer::SceneEntityPayloadVersion = 1;

::EditorAssetBinaryValueKind EditorAssetBinarySerializer::GetValueKind(::Asset* asset)
{
    if (he_cpp_try_cast<TextureAsset>(asset) != nullptr)
    {
return EditorAssetBinaryValueKind::TextureAsset;    }
else     if (he_cpp_try_cast<ModelAsset>(asset) != nullptr)
    {
return EditorAssetBinaryValueKind::ModelAsset;    }
else     if (he_cpp_try_cast<ShaderAsset>(asset) != nullptr)
    {
return EditorAssetBinaryValueKind::ShaderAsset;    }
else     if (he_cpp_try_cast<TextAsset>(asset) != nullptr)
    {
return EditorAssetBinaryValueKind::TextAsset;    }
else     if (he_cpp_try_cast<MaterialAsset>(asset) != nullptr)
    {
return EditorAssetBinaryValueKind::MaterialAsset;    }
else     if (he_cpp_try_cast<SceneAsset>(asset) != nullptr)
    {
return EditorAssetBinaryValueKind::SceneAsset;    }
throw new InvalidOperationException(std::string("Asset type '") + he_cpp_type_of<Asset>("Asset")->Name + std::string("' is not supported by the editor binary serializer."));
}

::Asset* EditorAssetBinarySerializer::ReadAssetPayload(::EngineBinaryReader* reader, ::EditorAssetBinaryValueKind valueKind, uint8_t version)
{
switch (valueKind) {
case EditorAssetBinaryValueKind::TextureAsset: {
return ReadTextureAsset(reader);}
case EditorAssetBinaryValueKind::ModelAsset: {
return ReadModelAsset(reader);}
case EditorAssetBinaryValueKind::ShaderAsset: {
return ReadShaderAsset(reader);}
case EditorAssetBinaryValueKind::TextAsset: {
return ReadTextAsset(reader);}
case EditorAssetBinaryValueKind::MaterialAsset: {
return ReadMaterialAsset(reader);}
case EditorAssetBinaryValueKind::SceneAsset: {
return ReadSceneAsset(reader, version);}
default:  {
throw new InvalidOperationException(std::string("Unsupported asset value kind '") + std::to_string(static_cast<uint16_t>(valueKind)) + std::string("'."));
}
}

}

::float2 EditorAssetBinarySerializer::ReadFloat2(::EngineBinaryReader* reader)
{
return ([&]() {
auto __ctor_arg_00000036 = reader->ReadSingle();
auto __ctor_arg_00000037 = reader->ReadSingle();
return ::float2(__ctor_arg_00000036, __ctor_arg_00000037);
})();}

::float3 EditorAssetBinarySerializer::ReadFloat3(::EngineBinaryReader* reader)
{
return ([&]() {
auto __ctor_arg_00000038 = reader->ReadSingle();
auto __ctor_arg_00000039 = reader->ReadSingle();
auto __ctor_arg_0000003A = reader->ReadSingle();
return ::float3(__ctor_arg_00000038, __ctor_arg_00000039, __ctor_arg_0000003A);
})();}

::float4 EditorAssetBinarySerializer::ReadFloat4(::EngineBinaryReader* reader)
{
return ([&]() {
auto __ctor_arg_0000003B = reader->ReadSingle();
auto __ctor_arg_0000003C = reader->ReadSingle();
auto __ctor_arg_0000003D = reader->ReadSingle();
auto __ctor_arg_0000003E = reader->ReadSingle();
return ::float4(__ctor_arg_0000003B, __ctor_arg_0000003C, __ctor_arg_0000003D, __ctor_arg_0000003E);
})();}

::SceneEntityAsset* EditorAssetBinarySerializer::ReadLegacySceneEntityAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_0000003F = new ::SceneEntityAsset();
__object_0000003F->set_Id(Guid::NewGuid().ToString("N"));
__object_0000003F->set_Name(reader->ReadString());
__object_0000003F->set_LocalPosition(reader->ReadFloat3());
__object_0000003F->set_LocalScale(reader->ReadFloat3());
__object_0000003F->set_LocalOrientation(reader->ReadFloat4());
__object_0000003F->set_Components(([&]() {
Array<::SceneComponentAssetRecord*>* __coalesce_00000040 = reader->ReadArray<SceneComponentAssetRecord*>(new Func<EngineBinaryReader*, SceneComponentAssetRecord*>(&EditorAssetBinarySerializer::ReadSceneComponentAssetRecord));
return __coalesce_00000040 != nullptr ? __coalesce_00000040 : Array<SceneComponentAssetRecord*>::Empty();
})());
__object_0000003F->set_Children(([&]() {
Array<::SceneEntityAsset*>* __coalesce_00000041 = ReadLegacySceneEntityAssetArray(reader);
return __coalesce_00000041 != nullptr ? __coalesce_00000041 : Array<SceneEntityAsset*>::Empty();
})());
return __object_0000003F;
})();}

Array<::SceneEntityAsset*>* EditorAssetBinarySerializer::ReadLegacySceneEntityAssetArray(::EngineBinaryReader* reader)
{
const int32_t length = reader->ReadInt32();
    if (length == -1)
    {
return nullptr;    }
else     if (length < -1)
    {
throw new InvalidOperationException("Array length cannot be negative.");
    }
else     if (length == 0)
    {
return Array<SceneEntityAsset*>::Empty();    }
Array<::SceneEntityAsset*> *values = new Array<SceneEntityAsset*>(length);
for (int32_t index = 0; index < values->Length; index++) {
(*values)[index] = ReadLegacySceneEntityAsset(reader);
}
return values;}

::MaterialAsset* EditorAssetBinarySerializer::ReadMaterialAsset(::EngineBinaryReader* reader)
{
::MaterialAsset *materialAsset = ([&]() {
auto __object_00000042 = new ::MaterialAsset();
__object_00000042->set_Id(reader->ReadString());
__object_00000042->ShaderAssetId = reader->ReadString();
__object_00000042->VertexProgram = reader->ReadString();
__object_00000042->PixelProgram = reader->ReadString();
__object_00000042->Variant = reader->ReadString();
__object_00000042->RenderState = ReadMaterialRenderState(reader);
__object_00000042->ConstantBuffers = ([&]() {
Array<::MaterialConstantBufferAsset*>* __coalesce_00000043 = reader->ReadArray<MaterialConstantBufferAsset*>(new Func<EngineBinaryReader*, MaterialConstantBufferAsset*>(&EditorAssetBinarySerializer::ReadMaterialConstantBufferAsset));
return __coalesce_00000043 != nullptr ? __coalesce_00000043 : Array<MaterialConstantBufferAsset*>::Empty();
})();
return __object_00000042;
})();
return materialAsset;}

::MaterialConstantBufferAsset* EditorAssetBinarySerializer::ReadMaterialConstantBufferAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000044 = new ::MaterialConstantBufferAsset();
__object_00000044->set_Name(reader->ReadString());
__object_00000044->set_Data(reader->ReadByteArray());
return __object_00000044;
})();}

::MaterialRenderState* EditorAssetBinarySerializer::ReadMaterialRenderState(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000045 = new ::MaterialRenderState();
__object_00000045->set_BlendMode(static_cast<MaterialBlendMode>(reader->ReadInt32()));
__object_00000045->set_CullMode(static_cast<MaterialCullMode>(reader->ReadInt32()));
__object_00000045->set_DepthTestEnabled(reader->ReadByte() != 0);
__object_00000045->set_DepthWriteEnabled(reader->ReadByte() != 0);
return __object_00000045;
})();}

::ModelAsset* EditorAssetBinarySerializer::ReadModelAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000046 = new ::ModelAsset();
__object_00000046->set_Id(reader->ReadString());
__object_00000046->Positions = reader->ReadArray<float3>(new Func<EngineBinaryReader*, float3>(&EditorAssetBinarySerializer::ReadFloat3));
__object_00000046->Normals = reader->ReadArray<float3>(new Func<EngineBinaryReader*, float3>(&EditorAssetBinarySerializer::ReadFloat3));
__object_00000046->TexCoords = reader->ReadArray<float2>(new Func<EngineBinaryReader*, float2>(&EditorAssetBinarySerializer::ReadFloat2));
__object_00000046->Indices16 = reader->ReadArray<uint16_t>(new Func<EngineBinaryReader*, uint16_t>(&EditorAssetBinarySerializer::ReadUInt16Value));
__object_00000046->Indices32 = reader->ReadArray<uint32_t>(new Func<EngineBinaryReader*, uint32_t>(&EditorAssetBinarySerializer::ReadUInt32Value));
return __object_00000046;
})();}

::SceneAsset* EditorAssetBinarySerializer::ReadSceneAsset(::EngineBinaryReader* reader, uint8_t version)
{
return ([&]() {
auto __object_00000047 = new ::SceneAsset();
__object_00000047->set_Id(reader->ReadString());
__object_00000047->set_RootEntities(([&]() {
Array<::SceneEntityAsset*>* __coalesce_00000048 = ReadSceneEntityAssetArray(reader, version);
return __coalesce_00000048 != nullptr ? __coalesce_00000048 : Array<SceneEntityAsset*>::Empty();
})());
__object_00000047->set_AssetReferences(version >= 4 ? ([&]() {
Array<::SceneAssetReference*>* __coalesce_00000049 = ReadSceneAssetReferenceArray(reader);
return __coalesce_00000049 != nullptr ? __coalesce_00000049 : Array<SceneAssetReference*>::Empty();
})() : Array<SceneAssetReference*>::Empty());
return __object_00000047;
})();}

::SceneAssetReference* EditorAssetBinarySerializer::ReadSceneAssetReference(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_0000004A = new ::SceneAssetReference();
__object_0000004A->set_SourceKind(static_cast<SceneAssetReferenceSourceKind>(reader->ReadInt32()));
__object_0000004A->set_RelativePath(reader->ReadString());
__object_0000004A->set_ProviderId(reader->ReadString());
__object_0000004A->set_AssetId(reader->ReadString());
return __object_0000004A;
})();}

Array<::SceneAssetReference*>* EditorAssetBinarySerializer::ReadSceneAssetReferenceArray(::EngineBinaryReader* reader)
{
return reader->ReadArray<SceneAssetReference*>(new Func<EngineBinaryReader*, SceneAssetReference*>(&EditorAssetBinarySerializer::ReadSceneAssetReference));}

::SceneComponentAssetRecord* EditorAssetBinarySerializer::ReadSceneComponentAssetRecord(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_0000004B = new ::SceneComponentAssetRecord();
__object_0000004B->set_ComponentTypeId(reader->ReadString());
__object_0000004B->set_ComponentIndex(reader->ReadInt32());
__object_0000004B->set_Payload(([&]() {
Array<uint8_t>* __coalesce_0000004C = reader->ReadByteArray();
return __coalesce_0000004C != nullptr ? __coalesce_0000004C : Array<uint8_t>::Empty();
})());
return __object_0000004B;
})();}

::SceneEntityAsset* EditorAssetBinarySerializer::ReadSceneEntityAsset(::EngineBinaryReader* reader, uint8_t version)
{
    if (version == LegacyVersion)
    {
return ReadLegacySceneEntityAsset(reader);    }
const uint8_t payloadVersion = reader->ReadByte();
    if (payloadVersion != SceneEntityPayloadVersion)
    {
throw new InvalidOperationException(std::string("Unsupported scene entity payload version '") + std::to_string(payloadVersion) + std::string("'."));
    }
return ([&]() {
auto __object_0000004D = new ::SceneEntityAsset();
__object_0000004D->set_Id(reader->ReadString());
__object_0000004D->set_Name(reader->ReadString());
__object_0000004D->set_LocalPosition(reader->ReadFloat3());
__object_0000004D->set_LocalScale(reader->ReadFloat3());
__object_0000004D->set_LocalOrientation(reader->ReadFloat4());
__object_0000004D->set_Components(([&]() {
Array<::SceneComponentAssetRecord*>* __coalesce_0000004E = reader->ReadArray<SceneComponentAssetRecord*>(new Func<EngineBinaryReader*, SceneComponentAssetRecord*>(&EditorAssetBinarySerializer::ReadSceneComponentAssetRecord));
return __coalesce_0000004E != nullptr ? __coalesce_0000004E : Array<SceneComponentAssetRecord*>::Empty();
})());
__object_0000004D->set_Children(([&]() {
Array<::SceneEntityAsset*>* __coalesce_0000004F = ReadSceneEntityAssetArray(reader, version);
return __coalesce_0000004F != nullptr ? __coalesce_0000004F : Array<SceneEntityAsset*>::Empty();
})());
return __object_0000004D;
})();}

Array<::SceneEntityAsset*>* EditorAssetBinarySerializer::ReadSceneEntityAssetArray(::EngineBinaryReader* reader, uint8_t version)
{
const int32_t length = reader->ReadInt32();
    if (length == -1)
    {
return nullptr;    }
else     if (length < -1)
    {
throw new InvalidOperationException("Array length cannot be negative.");
    }
else     if (length == 0)
    {
return Array<SceneEntityAsset*>::Empty();    }
Array<::SceneEntityAsset*> *values = new Array<SceneEntityAsset*>(length);
for (int32_t index = 0; index < values->Length; index++) {
(*values)[index] = ReadSceneEntityAsset(reader, version);
}
return values;}

::ShaderAsset* EditorAssetBinarySerializer::ReadShaderAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000050 = new ::ShaderAsset();
__object_00000050->set_Id(reader->ReadString());
__object_00000050->Name = reader->ReadString();
__object_00000050->TargetName = reader->ReadString();
__object_00000050->Programs = reader->ReadArray<ShaderProgramAsset*>(new Func<EngineBinaryReader*, ShaderProgramAsset*>(&EditorAssetBinarySerializer::ReadShaderProgramAsset));
__object_00000050->Binaries = reader->ReadArray<ShaderBinaryAsset*>(new Func<EngineBinaryReader*, ShaderBinaryAsset*>(&EditorAssetBinarySerializer::ReadShaderBinaryAsset));
return __object_00000050;
})();}

::ShaderBinaryAsset* EditorAssetBinarySerializer::ReadShaderBinaryAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000051 = new ::ShaderBinaryAsset();
__object_00000051->ProgramName = reader->ReadString();
__object_00000051->Stage = static_cast<ShaderStage>(reader->ReadInt32());
__object_00000051->TargetName = reader->ReadString();
__object_00000051->Variant = reader->ReadString();
__object_00000051->Bytecode = reader->ReadByteArray();
return __object_00000051;
})();}

::ShaderBindingAsset* EditorAssetBinarySerializer::ReadShaderBindingAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000052 = new ::ShaderBindingAsset();
__object_00000052->Name = reader->ReadString();
__object_00000052->Type = static_cast<ShaderResourceType>(reader->ReadInt32());
__object_00000052->Set = reader->ReadInt32();
__object_00000052->Slot = reader->ReadInt32();
__object_00000052->Size = reader->ReadInt32();
__object_00000052->Members = reader->ReadArray<ShaderConstantMemberAsset*>(new Func<EngineBinaryReader*, ShaderConstantMemberAsset*>(&EditorAssetBinarySerializer::ReadShaderConstantMemberAsset));
return __object_00000052;
})();}

::ShaderConstantMemberAsset* EditorAssetBinarySerializer::ReadShaderConstantMemberAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000053 = new ::ShaderConstantMemberAsset();
__object_00000053->Name = reader->ReadString();
__object_00000053->Type = reader->ReadString();
__object_00000053->Offset = reader->ReadInt32();
__object_00000053->Size = reader->ReadInt32();
return __object_00000053;
})();}

::ShaderProgramAsset* EditorAssetBinarySerializer::ReadShaderProgramAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000054 = new ::ShaderProgramAsset();
__object_00000054->Name = reader->ReadString();
__object_00000054->Stage = static_cast<ShaderStage>(reader->ReadInt32());
__object_00000054->EntryPoint = reader->ReadString();
__object_00000054->Bindings = reader->ReadArray<ShaderBindingAsset*>(new Func<EngineBinaryReader*, ShaderBindingAsset*>(&EditorAssetBinarySerializer::ReadShaderBindingAsset));
__object_00000054->Inputs = reader->ReadArray<ShaderVertexElementAsset*>(new Func<EngineBinaryReader*, ShaderVertexElementAsset*>(&EditorAssetBinarySerializer::ReadShaderVertexElementAsset));
__object_00000054->Outputs = reader->ReadArray<ShaderVertexElementAsset*>(new Func<EngineBinaryReader*, ShaderVertexElementAsset*>(&EditorAssetBinarySerializer::ReadShaderVertexElementAsset));
__object_00000054->Variants = reader->ReadArray<ShaderVariantAsset*>(new Func<EngineBinaryReader*, ShaderVariantAsset*>(&EditorAssetBinarySerializer::ReadShaderVariantAsset));
return __object_00000054;
})();}

::ShaderVariantAsset* EditorAssetBinarySerializer::ReadShaderVariantAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000055 = new ::ShaderVariantAsset();
__object_00000055->Name = reader->ReadString();
__object_00000055->Defines = reader->ReadArray<std::string>(new Func<EngineBinaryReader*, std::string>(&EditorAssetBinarySerializer::ReadStringValue));
return __object_00000055;
})();}

::ShaderVertexElementAsset* EditorAssetBinarySerializer::ReadShaderVertexElementAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000056 = new ::ShaderVertexElementAsset();
__object_00000056->Semantic = reader->ReadString();
__object_00000056->Index = reader->ReadInt32();
__object_00000056->Format = reader->ReadString();
return __object_00000056;
})();}

std::string EditorAssetBinarySerializer::ReadStringValue(::EngineBinaryReader* reader)
{
return reader->ReadString();}

::TextAsset* EditorAssetBinarySerializer::ReadTextAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000057 = new ::TextAsset();
__object_00000057->set_Id(reader->ReadString());
__object_00000057->Text = reader->ReadString();
return __object_00000057;
})();}

::TextureAsset* EditorAssetBinarySerializer::ReadTextureAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_00000058 = new ::TextureAsset();
__object_00000058->set_Id(reader->ReadString());
__object_00000058->Width = reader->ReadUInt16();
__object_00000058->Height = reader->ReadUInt16();
__object_00000058->Colors = reader->ReadByteArray();
return __object_00000058;
})();}

uint16_t EditorAssetBinarySerializer::ReadUInt16Value(::EngineBinaryReader* reader)
{
return reader->ReadUInt16();}

uint32_t EditorAssetBinarySerializer::ReadUInt32Value(::EngineBinaryReader* reader)
{
return reader->ReadUInt32();}

void EditorAssetBinarySerializer::ValidateHeader(::EngineBinaryHeader* header)
{
    if (header->get_FormatId() != FormatId)
    {
throw new InvalidOperationException(std::string("Unsupported asset binary format id '") + std::to_string(header->get_FormatId()) + std::string("'."));
    }
else     if (header->get_RecordKind() != static_cast<uint16_t>(RecordKind))
    {
throw new InvalidOperationException(std::string("Unexpected asset record kind '") + std::to_string(header->get_RecordKind()) + std::string("'."));
    }
else     if (header->get_Version() < LegacyVersion || header->get_Version() > CurrentVersion)
    {
throw new InvalidOperationException(std::string("Unsupported asset binary version '") + std::to_string(header->get_Version()) + std::string("'."));
    }
}

void EditorAssetBinarySerializer::WriteAssetPayload(::EngineBinaryWriter* writer, ::Asset* asset)
{
    TextureAsset* textureAsset = he_cpp_try_cast<TextureAsset>(asset);
    if (textureAsset != nullptr)
    {
WriteTextureAsset(writer, textureAsset);
return;    }
else {
    ModelAsset* modelAsset = he_cpp_try_cast<ModelAsset>(asset);
    if (modelAsset != nullptr)
    {
WriteModelAsset(writer, modelAsset);
return;    }
else {
    ShaderAsset* shaderAsset = he_cpp_try_cast<ShaderAsset>(asset);
    if (shaderAsset != nullptr)
    {
WriteShaderAsset(writer, shaderAsset);
return;    }
else {
    TextAsset* textAsset = he_cpp_try_cast<TextAsset>(asset);
    if (textAsset != nullptr)
    {
WriteTextAsset(writer, textAsset);
return;    }
else {
    MaterialAsset* materialAsset = he_cpp_try_cast<MaterialAsset>(asset);
    if (materialAsset != nullptr)
    {
WriteMaterialAsset(writer, materialAsset);
return;    }
else {
    SceneAsset* sceneAsset = he_cpp_try_cast<SceneAsset>(asset);
    if (sceneAsset != nullptr)
    {
WriteSceneAsset(writer, sceneAsset);
return;    }
}
}
}
}
}
throw new InvalidOperationException(std::string("Asset type '") + he_cpp_type_of<Asset>("Asset")->Name + std::string("' is not supported by the editor binary serializer."));
}

void EditorAssetBinarySerializer::WriteFloat2(::EngineBinaryWriter* writer, ::float2 value)
{
writer->WriteSingle(value.X);
writer->WriteSingle(value.Y);
}

void EditorAssetBinarySerializer::WriteFloat3(::EngineBinaryWriter* writer, ::float3 value)
{
writer->WriteSingle(value.X);
writer->WriteSingle(value.Y);
writer->WriteSingle(value.Z);
}

void EditorAssetBinarySerializer::WriteFloat4(::EngineBinaryWriter* writer, ::float4 value)
{
writer->WriteSingle(value.X);
writer->WriteSingle(value.Y);
writer->WriteSingle(value.Z);
writer->WriteSingle(value.W);
}

void EditorAssetBinarySerializer::WriteMaterialAsset(::EngineBinaryWriter* writer, ::MaterialAsset* asset)
{
writer->WriteString(asset->Id);
writer->WriteString(asset->ShaderAssetId);
writer->WriteString(asset->VertexProgram);
writer->WriteString(asset->PixelProgram);
writer->WriteString(asset->Variant);
WriteMaterialRenderState(writer, asset->RenderState);
writer->WriteArray<MaterialConstantBufferAsset*>(asset->ConstantBuffers, new Action<EngineBinaryWriter*, MaterialConstantBufferAsset*>(&EditorAssetBinarySerializer::WriteMaterialConstantBufferAsset));
}

void EditorAssetBinarySerializer::WriteMaterialConstantBufferAsset(::EngineBinaryWriter* writer, ::MaterialConstantBufferAsset* asset)
{
    if (asset == nullptr)
    {
throw new ArgumentNullException("asset");
    }
else     if (String::IsNullOrWhiteSpace(asset->get_Name()))
    {
throw new InvalidOperationException("Material constant-buffer assets must define a binding name.");
    }
else     if (asset->get_Data() == nullptr)
    {
throw new InvalidOperationException("Material constant-buffer assets must define a byte payload.");
    }
writer->WriteString(asset->get_Name());
writer->WriteByteArray(asset->get_Data());
}

void EditorAssetBinarySerializer::WriteMaterialRenderState(::EngineBinaryWriter* writer, ::MaterialRenderState* renderState)
{
    if (renderState == nullptr)
    {
throw new ArgumentNullException("renderState");
    }
writer->WriteInt32(static_cast<int32_t>(renderState->get_BlendMode()));
writer->WriteInt32(static_cast<int32_t>(renderState->get_CullMode()));
writer->WriteByte(renderState->get_DepthTestEnabled() ? static_cast<uint8_t>(1) : static_cast<uint8_t>(0));
writer->WriteByte(renderState->get_DepthWriteEnabled() ? static_cast<uint8_t>(1) : static_cast<uint8_t>(0));
}

void EditorAssetBinarySerializer::WriteModelAsset(::EngineBinaryWriter* writer, ::ModelAsset* asset)
{
writer->WriteString(asset->Id);
writer->WriteArray<float3>(asset->Positions, new Action<EngineBinaryWriter*, float3>(&EditorAssetBinarySerializer::WriteFloat3));
writer->WriteArray<float3>(asset->Normals, new Action<EngineBinaryWriter*, float3>(&EditorAssetBinarySerializer::WriteFloat3));
writer->WriteArray<float2>(asset->TexCoords, new Action<EngineBinaryWriter*, float2>(&EditorAssetBinarySerializer::WriteFloat2));
writer->WriteArray<uint16_t>(asset->Indices16, new Action<EngineBinaryWriter*, uint16_t>(&EditorAssetBinarySerializer::WriteUInt16Value));
writer->WriteArray<uint32_t>(asset->Indices32, new Action<EngineBinaryWriter*, uint32_t>(&EditorAssetBinarySerializer::WriteUInt32Value));
}

void EditorAssetBinarySerializer::WriteSceneAsset(::EngineBinaryWriter* writer, ::SceneAsset* asset)
{
writer->WriteString(asset->Id);
writer->WriteArray<SceneEntityAsset*>(asset->get_RootEntities(), new Action<EngineBinaryWriter*, SceneEntityAsset*>(&EditorAssetBinarySerializer::WriteSceneEntityAsset));
writer->WriteArray<SceneAssetReference*>(asset->get_AssetReferences(), new Action<EngineBinaryWriter*, SceneAssetReference*>(&EditorAssetBinarySerializer::WriteSceneAssetReference));
}

void EditorAssetBinarySerializer::WriteSceneAssetReference(::EngineBinaryWriter* writer, ::SceneAssetReference* reference)
{
writer->WriteInt32(static_cast<int32_t>(reference->get_SourceKind()));
writer->WriteString(reference->get_RelativePath());
writer->WriteString(reference->get_ProviderId());
writer->WriteString(reference->get_AssetId());
}

void EditorAssetBinarySerializer::WriteSceneComponentAssetRecord(::EngineBinaryWriter* writer, ::SceneComponentAssetRecord* record)
{
writer->WriteString(record->get_ComponentTypeId());
writer->WriteInt32(record->get_ComponentIndex());
writer->WriteByteArray(record->get_Payload());
}

void EditorAssetBinarySerializer::WriteSceneEntityAsset(::EngineBinaryWriter* writer, ::SceneEntityAsset* asset)
{
writer->WriteByte(SceneEntityPayloadVersion);
writer->WriteString(asset->get_Id());
writer->WriteString(asset->get_Name());
writer->WriteFloat3(asset->get_LocalPosition());
writer->WriteFloat3(asset->get_LocalScale());
writer->WriteFloat4(asset->get_LocalOrientation());
writer->WriteArray<SceneComponentAssetRecord*>(asset->get_Components(), new Action<EngineBinaryWriter*, SceneComponentAssetRecord*>(&EditorAssetBinarySerializer::WriteSceneComponentAssetRecord));
writer->WriteArray<SceneEntityAsset*>(asset->get_Children(), new Action<EngineBinaryWriter*, SceneEntityAsset*>(&EditorAssetBinarySerializer::WriteSceneEntityAsset));
}

void EditorAssetBinarySerializer::WriteShaderAsset(::EngineBinaryWriter* writer, ::ShaderAsset* asset)
{
writer->WriteString(asset->Id);
writer->WriteString(asset->Name);
writer->WriteString(asset->TargetName);
writer->WriteArray<ShaderProgramAsset*>(asset->Programs, new Action<EngineBinaryWriter*, ShaderProgramAsset*>(&EditorAssetBinarySerializer::WriteShaderProgramAsset));
writer->WriteArray<ShaderBinaryAsset*>(asset->Binaries, new Action<EngineBinaryWriter*, ShaderBinaryAsset*>(&EditorAssetBinarySerializer::WriteShaderBinaryAsset));
}

void EditorAssetBinarySerializer::WriteShaderBinaryAsset(::EngineBinaryWriter* writer, ::ShaderBinaryAsset* asset)
{
writer->WriteString(asset->ProgramName);
writer->WriteInt32(static_cast<int32_t>(asset->Stage));
writer->WriteString(asset->TargetName);
writer->WriteString(asset->Variant);
writer->WriteByteArray(asset->Bytecode);
}

void EditorAssetBinarySerializer::WriteShaderBindingAsset(::EngineBinaryWriter* writer, ::ShaderBindingAsset* asset)
{
writer->WriteString(asset->Name);
writer->WriteInt32(static_cast<int32_t>(asset->Type));
writer->WriteInt32(asset->Set);
writer->WriteInt32(asset->Slot);
writer->WriteInt32(asset->Size);
writer->WriteArray<ShaderConstantMemberAsset*>(asset->Members, new Action<EngineBinaryWriter*, ShaderConstantMemberAsset*>(&EditorAssetBinarySerializer::WriteShaderConstantMemberAsset));
}

void EditorAssetBinarySerializer::WriteShaderConstantMemberAsset(::EngineBinaryWriter* writer, ::ShaderConstantMemberAsset* asset)
{
writer->WriteString(asset->Name);
writer->WriteString(asset->Type);
writer->WriteInt32(asset->Offset);
writer->WriteInt32(asset->Size);
}

void EditorAssetBinarySerializer::WriteShaderProgramAsset(::EngineBinaryWriter* writer, ::ShaderProgramAsset* asset)
{
writer->WriteString(asset->Name);
writer->WriteInt32(static_cast<int32_t>(asset->Stage));
writer->WriteString(asset->EntryPoint);
writer->WriteArray<ShaderBindingAsset*>(asset->Bindings, new Action<EngineBinaryWriter*, ShaderBindingAsset*>(&EditorAssetBinarySerializer::WriteShaderBindingAsset));
writer->WriteArray<ShaderVertexElementAsset*>(asset->Inputs, new Action<EngineBinaryWriter*, ShaderVertexElementAsset*>(&EditorAssetBinarySerializer::WriteShaderVertexElementAsset));
writer->WriteArray<ShaderVertexElementAsset*>(asset->Outputs, new Action<EngineBinaryWriter*, ShaderVertexElementAsset*>(&EditorAssetBinarySerializer::WriteShaderVertexElementAsset));
writer->WriteArray<ShaderVariantAsset*>(asset->Variants, new Action<EngineBinaryWriter*, ShaderVariantAsset*>(&EditorAssetBinarySerializer::WriteShaderVariantAsset));
}

void EditorAssetBinarySerializer::WriteShaderVariantAsset(::EngineBinaryWriter* writer, ::ShaderVariantAsset* asset)
{
writer->WriteString(asset->Name);
writer->WriteArray<std::string>(asset->Defines, new Action<EngineBinaryWriter*, std::string>(&EditorAssetBinarySerializer::WriteStringValue));
}

void EditorAssetBinarySerializer::WriteShaderVertexElementAsset(::EngineBinaryWriter* writer, ::ShaderVertexElementAsset* asset)
{
writer->WriteString(asset->Semantic);
writer->WriteInt32(asset->Index);
writer->WriteString(asset->Format);
}

void EditorAssetBinarySerializer::WriteStringValue(::EngineBinaryWriter* writer, std::string value)
{
writer->WriteString(value);
}

void EditorAssetBinarySerializer::WriteTextAsset(::EngineBinaryWriter* writer, ::TextAsset* asset)
{
writer->WriteString(asset->Id);
writer->WriteString(asset->Text);
}

void EditorAssetBinarySerializer::WriteTextureAsset(::EngineBinaryWriter* writer, ::TextureAsset* asset)
{
writer->WriteString(asset->Id);
writer->WriteUInt16(asset->Width);
writer->WriteUInt16(asset->Height);
writer->WriteByteArray(asset->Colors);
}

void EditorAssetBinarySerializer::WriteUInt16Value(::EngineBinaryWriter* writer, uint16_t value)
{
writer->WriteUInt16(value);
}

void EditorAssetBinarySerializer::WriteUInt32Value(::EngineBinaryWriter* writer, uint32_t value)
{
writer->WriteUInt32(value);
}

