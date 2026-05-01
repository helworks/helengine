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
#include "runtime/finally.hpp"
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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

uint8_t EditorAssetBinarySerializer::CurrentVersion = 2;

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
return ReadAssetPayload(reader, static_cast<EditorAssetBinaryValueKind>(header->get_ValueKind()));}
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

::EngineBinaryEndianness EditorAssetBinarySerializer::PayloadEndianness = EngineBinaryEndianness::LittleEndian;

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

::Asset* EditorAssetBinarySerializer::ReadAssetPayload(::EngineBinaryReader* reader, ::EditorAssetBinaryValueKind valueKind)
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
return ReadSceneAsset(reader);}
default:  {
throw new InvalidOperationException(std::string("Unsupported asset value kind '") + std::to_string(static_cast<uint16_t>(valueKind)) + std::string("'."));
}
}

}

::float2 EditorAssetBinarySerializer::ReadFloat2(::EngineBinaryReader* reader)
{
return ([&]() {
auto __ctor_arg_ac7ee6a2 = reader->ReadSingle();
auto __ctor_arg_5bdbffb6 = reader->ReadSingle();
return ::float2(__ctor_arg_ac7ee6a2, __ctor_arg_5bdbffb6);
})();}

::float3 EditorAssetBinarySerializer::ReadFloat3(::EngineBinaryReader* reader)
{
return ([&]() {
auto __ctor_arg_fd928ebf = reader->ReadSingle();
auto __ctor_arg_80859b3d = reader->ReadSingle();
auto __ctor_arg_d8785a9a = reader->ReadSingle();
return ::float3(__ctor_arg_fd928ebf, __ctor_arg_80859b3d, __ctor_arg_d8785a9a);
})();}

::float4 EditorAssetBinarySerializer::ReadFloat4(::EngineBinaryReader* reader)
{
return ([&]() {
auto __ctor_arg_0775a2b9 = reader->ReadSingle();
auto __ctor_arg_25db0011 = reader->ReadSingle();
auto __ctor_arg_236e9b6e = reader->ReadSingle();
auto __ctor_arg_efca8e56 = reader->ReadSingle();
return ::float4(__ctor_arg_0775a2b9, __ctor_arg_25db0011, __ctor_arg_236e9b6e, __ctor_arg_efca8e56);
})();}

::MaterialAsset* EditorAssetBinarySerializer::ReadMaterialAsset(::EngineBinaryReader* reader)
{
::MaterialAsset *materialAsset = ([&]() {
auto __object_7261fe0d = new ::MaterialAsset();
__object_7261fe0d->set_Id(reader->ReadString());
__object_7261fe0d->ShaderAssetId = reader->ReadString();
__object_7261fe0d->VertexProgram = reader->ReadString();
__object_7261fe0d->PixelProgram = reader->ReadString();
__object_7261fe0d->Variant = reader->ReadString();
__object_7261fe0d->RenderState = ReadMaterialRenderState(reader);
__object_7261fe0d->ConstantBuffers = ([&]() {
Array<::MaterialConstantBufferAsset*>* __coalesce_cf2b8f0d = reader->ReadArray<MaterialConstantBufferAsset*>(new Func<EngineBinaryReader*, MaterialConstantBufferAsset*>(&EditorAssetBinarySerializer::ReadMaterialConstantBufferAsset));
return __coalesce_cf2b8f0d != nullptr ? __coalesce_cf2b8f0d : Array<MaterialConstantBufferAsset*>::Empty();
})();
return __object_7261fe0d;
})();
return materialAsset;}

::MaterialConstantBufferAsset* EditorAssetBinarySerializer::ReadMaterialConstantBufferAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_b2731949 = new ::MaterialConstantBufferAsset();
__object_b2731949->set_Name(reader->ReadString());
__object_b2731949->set_Data(reader->ReadByteArray());
return __object_b2731949;
})();}

::MaterialRenderState* EditorAssetBinarySerializer::ReadMaterialRenderState(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_13d2e27c = new ::MaterialRenderState();
__object_13d2e27c->set_BlendMode(static_cast<MaterialBlendMode>(reader->ReadInt32()));
__object_13d2e27c->set_CullMode(static_cast<MaterialCullMode>(reader->ReadInt32()));
__object_13d2e27c->set_DepthTestEnabled(reader->ReadByte() != 0);
__object_13d2e27c->set_DepthWriteEnabled(reader->ReadByte() != 0);
return __object_13d2e27c;
})();}

::ModelAsset* EditorAssetBinarySerializer::ReadModelAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_9518a010 = new ::ModelAsset();
__object_9518a010->set_Id(reader->ReadString());
__object_9518a010->Positions = reader->ReadArray<float3>(new Func<EngineBinaryReader*, float3>(&EditorAssetBinarySerializer::ReadFloat3));
__object_9518a010->Normals = reader->ReadArray<float3>(new Func<EngineBinaryReader*, float3>(&EditorAssetBinarySerializer::ReadFloat3));
__object_9518a010->TexCoords = reader->ReadArray<float2>(new Func<EngineBinaryReader*, float2>(&EditorAssetBinarySerializer::ReadFloat2));
__object_9518a010->Indices16 = reader->ReadArray<uint16_t>(new Func<EngineBinaryReader*, uint16_t>(&EditorAssetBinarySerializer::ReadUInt16Value));
__object_9518a010->Indices32 = reader->ReadArray<uint32_t>(new Func<EngineBinaryReader*, uint32_t>(&EditorAssetBinarySerializer::ReadUInt32Value));
return __object_9518a010;
})();}

::SceneAsset* EditorAssetBinarySerializer::ReadSceneAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_d440cbe6 = new ::SceneAsset();
__object_d440cbe6->set_Id(reader->ReadString());
__object_d440cbe6->set_RootEntities(([&]() {
Array<::SceneEntityAsset*>* __coalesce_385b90ee = reader->ReadArray<SceneEntityAsset*>(new Func<EngineBinaryReader*, SceneEntityAsset*>(&EditorAssetBinarySerializer::ReadSceneEntityAsset));
return __coalesce_385b90ee != nullptr ? __coalesce_385b90ee : Array<SceneEntityAsset*>::Empty();
})());
return __object_d440cbe6;
})();}

::SceneComponentAssetRecord* EditorAssetBinarySerializer::ReadSceneComponentAssetRecord(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_96bbd2ea = new ::SceneComponentAssetRecord();
__object_96bbd2ea->set_ComponentTypeId(reader->ReadString());
__object_96bbd2ea->set_ComponentIndex(reader->ReadInt32());
__object_96bbd2ea->set_Payload(([&]() {
Array<uint8_t>* __coalesce_b9666d8f = reader->ReadByteArray();
return __coalesce_b9666d8f != nullptr ? __coalesce_b9666d8f : Array<uint8_t>::Empty();
})());
return __object_96bbd2ea;
})();}

::SceneEntityAsset* EditorAssetBinarySerializer::ReadSceneEntityAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_f440da29 = new ::SceneEntityAsset();
__object_f440da29->set_Name(reader->ReadString());
__object_f440da29->set_LocalPosition(ReadFloat3(reader));
__object_f440da29->set_LocalScale(ReadFloat3(reader));
__object_f440da29->set_LocalOrientation(ReadFloat4(reader));
__object_f440da29->set_Components(([&]() {
Array<::SceneComponentAssetRecord*>* __coalesce_41424c42 = reader->ReadArray<SceneComponentAssetRecord*>(new Func<EngineBinaryReader*, SceneComponentAssetRecord*>(&EditorAssetBinarySerializer::ReadSceneComponentAssetRecord));
return __coalesce_41424c42 != nullptr ? __coalesce_41424c42 : Array<SceneComponentAssetRecord*>::Empty();
})());
__object_f440da29->set_Children(([&]() {
Array<::SceneEntityAsset*>* __coalesce_1797705c = reader->ReadArray<SceneEntityAsset*>(new Func<EngineBinaryReader*, SceneEntityAsset*>(&EditorAssetBinarySerializer::ReadSceneEntityAsset));
return __coalesce_1797705c != nullptr ? __coalesce_1797705c : Array<SceneEntityAsset*>::Empty();
})());
return __object_f440da29;
})();}

::ShaderAsset* EditorAssetBinarySerializer::ReadShaderAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_a5145cd2 = new ::ShaderAsset();
__object_a5145cd2->set_Id(reader->ReadString());
__object_a5145cd2->Name = reader->ReadString();
__object_a5145cd2->TargetName = reader->ReadString();
__object_a5145cd2->Programs = reader->ReadArray<ShaderProgramAsset*>(new Func<EngineBinaryReader*, ShaderProgramAsset*>(&EditorAssetBinarySerializer::ReadShaderProgramAsset));
__object_a5145cd2->Binaries = reader->ReadArray<ShaderBinaryAsset*>(new Func<EngineBinaryReader*, ShaderBinaryAsset*>(&EditorAssetBinarySerializer::ReadShaderBinaryAsset));
return __object_a5145cd2;
})();}

::ShaderBinaryAsset* EditorAssetBinarySerializer::ReadShaderBinaryAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_f531c2bd = new ::ShaderBinaryAsset();
__object_f531c2bd->ProgramName = reader->ReadString();
__object_f531c2bd->Stage = static_cast<ShaderStage>(reader->ReadInt32());
__object_f531c2bd->TargetName = reader->ReadString();
__object_f531c2bd->Variant = reader->ReadString();
__object_f531c2bd->Bytecode = reader->ReadByteArray();
return __object_f531c2bd;
})();}

::ShaderBindingAsset* EditorAssetBinarySerializer::ReadShaderBindingAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_a02e6d9b = new ::ShaderBindingAsset();
__object_a02e6d9b->Name = reader->ReadString();
__object_a02e6d9b->Type = static_cast<ShaderResourceType>(reader->ReadInt32());
__object_a02e6d9b->Set = reader->ReadInt32();
__object_a02e6d9b->Slot = reader->ReadInt32();
__object_a02e6d9b->Size = reader->ReadInt32();
__object_a02e6d9b->Members = reader->ReadArray<ShaderConstantMemberAsset*>(new Func<EngineBinaryReader*, ShaderConstantMemberAsset*>(&EditorAssetBinarySerializer::ReadShaderConstantMemberAsset));
return __object_a02e6d9b;
})();}

::ShaderConstantMemberAsset* EditorAssetBinarySerializer::ReadShaderConstantMemberAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_a503f4f3 = new ::ShaderConstantMemberAsset();
__object_a503f4f3->Name = reader->ReadString();
__object_a503f4f3->Type = reader->ReadString();
__object_a503f4f3->Offset = reader->ReadInt32();
__object_a503f4f3->Size = reader->ReadInt32();
return __object_a503f4f3;
})();}

::ShaderProgramAsset* EditorAssetBinarySerializer::ReadShaderProgramAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_4baa460a = new ::ShaderProgramAsset();
__object_4baa460a->Name = reader->ReadString();
__object_4baa460a->Stage = static_cast<ShaderStage>(reader->ReadInt32());
__object_4baa460a->EntryPoint = reader->ReadString();
__object_4baa460a->Bindings = reader->ReadArray<ShaderBindingAsset*>(new Func<EngineBinaryReader*, ShaderBindingAsset*>(&EditorAssetBinarySerializer::ReadShaderBindingAsset));
__object_4baa460a->Inputs = reader->ReadArray<ShaderVertexElementAsset*>(new Func<EngineBinaryReader*, ShaderVertexElementAsset*>(&EditorAssetBinarySerializer::ReadShaderVertexElementAsset));
__object_4baa460a->Outputs = reader->ReadArray<ShaderVertexElementAsset*>(new Func<EngineBinaryReader*, ShaderVertexElementAsset*>(&EditorAssetBinarySerializer::ReadShaderVertexElementAsset));
__object_4baa460a->Variants = reader->ReadArray<ShaderVariantAsset*>(new Func<EngineBinaryReader*, ShaderVariantAsset*>(&EditorAssetBinarySerializer::ReadShaderVariantAsset));
return __object_4baa460a;
})();}

::ShaderVariantAsset* EditorAssetBinarySerializer::ReadShaderVariantAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_8bc3e99c = new ::ShaderVariantAsset();
__object_8bc3e99c->Name = reader->ReadString();
__object_8bc3e99c->Defines = reader->ReadArray<std::string>(new Func<EngineBinaryReader*, std::string>(&EditorAssetBinarySerializer::ReadStringValue));
return __object_8bc3e99c;
})();}

::ShaderVertexElementAsset* EditorAssetBinarySerializer::ReadShaderVertexElementAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_42006717 = new ::ShaderVertexElementAsset();
__object_42006717->Semantic = reader->ReadString();
__object_42006717->Index = reader->ReadInt32();
__object_42006717->Format = reader->ReadString();
return __object_42006717;
})();}

std::string EditorAssetBinarySerializer::ReadStringValue(::EngineBinaryReader* reader)
{
return reader->ReadString();}

::TextAsset* EditorAssetBinarySerializer::ReadTextAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_99212a5b = new ::TextAsset();
__object_99212a5b->set_Id(reader->ReadString());
__object_99212a5b->Text = reader->ReadString();
return __object_99212a5b;
})();}

::TextureAsset* EditorAssetBinarySerializer::ReadTextureAsset(::EngineBinaryReader* reader)
{
return ([&]() {
auto __object_e6fa428f = new ::TextureAsset();
__object_e6fa428f->set_Id(reader->ReadString());
__object_e6fa428f->Width = reader->ReadUInt16();
__object_e6fa428f->Height = reader->ReadUInt16();
__object_e6fa428f->Colors = reader->ReadByteArray();
return __object_e6fa428f;
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
else     if (header->get_Version() != CurrentVersion)
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
}

void EditorAssetBinarySerializer::WriteSceneComponentAssetRecord(::EngineBinaryWriter* writer, ::SceneComponentAssetRecord* record)
{
writer->WriteString(record->get_ComponentTypeId());
writer->WriteInt32(record->get_ComponentIndex());
writer->WriteByteArray(record->get_Payload());
}

void EditorAssetBinarySerializer::WriteSceneEntityAsset(::EngineBinaryWriter* writer, ::SceneEntityAsset* asset)
{
writer->WriteString(asset->get_Name());
WriteFloat3(writer, asset->get_LocalPosition());
WriteFloat3(writer, asset->get_LocalScale());
WriteFloat4(writer, asset->get_LocalOrientation());
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

