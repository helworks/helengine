#ifdef DrawText
#undef DrawText
#endif
#include "HlslShaderBindingParser.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderConditionalPreprocessor.hpp"
#include "runtime/native_list.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "HlslShaderBindingParser.hpp"
#include "ShaderConstantMember.hpp"
#include "ShaderResourceType.hpp"
#include "ShaderBinding.hpp"
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
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

Array<::ShaderBinding*>* HlslShaderBindingParser::ParseBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy)
{
return ParseBindings(source, bindingPolicy, new List<ShaderDefine*>(Array<ShaderDefine*>::Empty()));}

Array<::ShaderBinding*>* HlslShaderBindingParser::ParseBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy, List<::ShaderDefine*>* defines)
{
    if (String::IsNullOrWhiteSpace(source))
    {
throw ([&]() {
auto __ctor_arg_7b63244d = "Shader source must be provided.";
auto __ctor_arg_65d59de9 = "source";
return new ArgumentException(__ctor_arg_7b63244d, __ctor_arg_65d59de9);
})();
    }
    if (bindingPolicy == nullptr)
    {
throw new ArgumentNullException("bindingPolicy");
    }
    if (defines == nullptr)
    {
throw new ArgumentNullException("defines");
    }
const std::string preprocessedSource = ShaderConditionalPreprocessor::Preprocess(source, defines);
const std::string normalizedSource = StripComments(preprocessedSource);
List<::ShaderBinding*> *bindings = new List<::ShaderBinding*>();
AddConstantBufferBindings(normalizedSource, bindingPolicy, bindings);
AddResourceBindings(normalizedSource, bindingPolicy, bindings);
return bindings->ToArray();}

Regex HlslShaderBindingParser::BlockCommentPattern = Regex("/\\*.*?\\*/", RegexOptions::Compiled | RegexOptions::Singleline);

Regex HlslShaderBindingParser::ConstantBufferMemberPattern = Regex("(?<type>[A-Za-z_]\\w*(?:<[^;>]+>)?)\\s+(?<name>[A-Za-z_]\\w*)(?:\\s*\\[\\s*(?<count>\\d+)\\s*\\])?\\s*;", RegexOptions::Compiled);

Regex HlslShaderBindingParser::ConstantBufferPattern = Regex("\\bcbuffer\\s+(?<name>[A-Za-z_]\\w*)\\s*:\\s*register\\s*\\(\\s*b(?<slot>\\d+)(?:\\s*,\\s*space(?<space>\\d+))?\\s*\\)\\s*\\{(?<body>.*?)\\}\\s*;?", RegexOptions::Compiled | RegexOptions::Singleline);

Regex HlslShaderBindingParser::LineCommentPattern = Regex("//.*?$", RegexOptions::Compiled | RegexOptions::Multiline);

Regex HlslShaderBindingParser::MatrixTypePattern = Regex("^(?<base>[A-Za-z_]\\w*)(?<rows>\\d)x(?<columns>\\d)$", RegexOptions::Compiled);

int32_t HlslShaderBindingParser::RegisterSizeInBytes = 16;

Regex HlslShaderBindingParser::ResourcePattern = Regex("^\\s*(?<type>[A-Za-z_]\\w*(?:<[^;>]+>)?)\\s+(?<name>[A-Za-z_]\\w*)\\s*:\\s*register\\s*\\(\\s*(?<register>[bstu])(?<slot>\\d+)(?:\\s*,\\s*space(?<space>\\d+))?\\s*\\)\\s*;", RegexOptions::Compiled | RegexOptions::Multiline);

void HlslShaderBindingParser::AddConstantBufferBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy, List<::ShaderBinding*>* bindings)
{
    if (String::IsNullOrEmpty(source))
    {
throw new ArgumentNullException("source");
    }
    if (bindingPolicy == nullptr)
    {
throw new ArgumentNullException("bindingPolicy");
    }
    if (bindings == nullptr)
    {
throw new ArgumentNullException("bindings");
    }
MatchCollection matches = ConstantBufferPattern.Matches(source);
for (int32_t matchIndex = 0; matchIndex < matches.Count; matchIndex++) {
Match match = matches[matchIndex];
    if (!match.Success)
    {
continue;
    }
const std::string name = match.Groups["name"].Value;
const int32_t registerIndex = ParseRequiredInt(match.Groups["slot"].Value, "constant-buffer register");
const int32_t set = ParseOptionalInt(match.Groups["space"].Value, bindingPolicy->get_DefaultSpace());
const int32_t slot = bindingPolicy->GetSlot(ShaderResourceType::ConstantBuffer, registerIndex);
const std::string body = match.Groups["body"].Value;
Array<::ShaderConstantMember*> *members = ParseConstantBufferMembers(body);
const int32_t size = ComputeConstantBufferSize(new List<ShaderConstantMember*>(members));
bindings->Add(new ::ShaderBinding(name, ShaderResourceType::ConstantBuffer, set, slot, size, members));
}
}

void HlslShaderBindingParser::AddResourceBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy, List<::ShaderBinding*>* bindings)
{
    if (String::IsNullOrEmpty(source))
    {
throw new ArgumentNullException("source");
    }
    if (bindingPolicy == nullptr)
    {
throw new ArgumentNullException("bindingPolicy");
    }
    if (bindings == nullptr)
    {
throw new ArgumentNullException("bindings");
    }
MatchCollection matches = ResourcePattern.Matches(source);
for (int32_t matchIndex = 0; matchIndex < matches.Count; matchIndex++) {
Match match = matches[matchIndex];
    if (!match.Success)
    {
continue;
    }
const std::string typeText = match.Groups["type"].Value;
::ShaderResourceType resourceType = ResolveResourceType(typeText, match.Groups["register"].Value);
const std::string name = match.Groups["name"].Value;
const int32_t registerIndex = ParseRequiredInt(match.Groups["slot"].Value, "resource register");
const int32_t set = ParseOptionalInt(match.Groups["space"].Value, bindingPolicy->get_DefaultSpace());
const int32_t slot = bindingPolicy->GetSlot(resourceType, registerIndex);
bindings->Add(([&]() {
auto __ctor_arg_20f4d484 = name;
auto __ctor_arg_15a14004 = resourceType;
auto __ctor_arg_f8346c98 = set;
auto __ctor_arg_144bc42d = slot;
auto __ctor_arg_551f9bd1 = 0;
auto __ctor_arg_04efb049 = Array<ShaderConstantMember*>::Empty();
return new ::ShaderBinding(__ctor_arg_20f4d484, __ctor_arg_15a14004, __ctor_arg_f8346c98, __ctor_arg_144bc42d, __ctor_arg_551f9bd1, __ctor_arg_04efb049);
})());
}
}

int32_t HlslShaderBindingParser::AlignToRegister(int32_t offset)
{
    if (offset < 0)
    {
throw ([&]() {
auto __ctor_arg_af5abe89 = "offset";
auto __ctor_arg_66dc026d = "Offset cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_af5abe89, __ctor_arg_66dc026d);
})();
    }
const int32_t remainder = offset % RegisterSizeInBytes;
    if (remainder == 0)
    {
return offset;    }
return offset + (RegisterSizeInBytes - remainder);}

int32_t HlslShaderBindingParser::ComputeArrayStorageSize(std::string type, int32_t arrayCount)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_24d8cebc = "Element type must be provided.";
auto __ctor_arg_b47b581b = "type";
return new ArgumentException(__ctor_arg_24d8cebc, __ctor_arg_b47b581b);
})();
    }
    if (arrayCount <= 0)
    {
throw ([&]() {
auto __ctor_arg_bba60b09 = "arrayCount";
auto __ctor_arg_97f8414a = "Array count must be greater than zero.";
return new ArgumentOutOfRangeException(__ctor_arg_bba60b09, __ctor_arg_97f8414a);
})();
    }
    if (IsMatrixType(type))
    {
return ComputeMatrixStorageSize(type) * arrayCount;    }
return RegisterSizeInBytes * arrayCount;}

int32_t HlslShaderBindingParser::ComputeConstantBufferSize(List<::ShaderConstantMember*>* members)
{
    if (members == nullptr)
    {
throw new ArgumentNullException("members");
    }
    if (members->get_Count() == 0)
    {
return 0;    }
::ShaderConstantMember *lastMember = (*members)[members->get_Count() - 1];
const int32_t endOffset = lastMember->get_Offset() + lastMember->get_Size();
return AlignToRegister(endOffset);}

int32_t HlslShaderBindingParser::ComputeMatrixStorageSize(std::string type)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_e954231f = "Matrix type must be provided.";
auto __ctor_arg_094fb9d2 = "type";
return new ArgumentException(__ctor_arg_e954231f, __ctor_arg_094fb9d2);
})();
    }
    if (String::Equals(type, "matrix", StringComparison::Ordinal))
    {
return RegisterSizeInBytes * 4;    }
Match match = MatrixTypePattern.Match(type);
    if (!match.Success)
    {
throw new InvalidOperationException(std::string("Unsupported HLSL matrix type '") + type + std::string("'."));
    }
const int32_t columns = ParseRequiredInt(match.Groups["columns"].Value, "matrix column count");
return RegisterSizeInBytes * columns;}

int32_t HlslShaderBindingParser::ComputeMemberOffset(int32_t currentOffset, std::string type, int32_t arrayCount, int32_t memberSize)
{
    if (currentOffset < 0)
    {
throw ([&]() {
auto __ctor_arg_bbcecd31 = "currentOffset";
auto __ctor_arg_e835b116 = "Current offset cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_bbcecd31, __ctor_arg_e835b116);
})();
    }
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_a9604fdd = "Member type must be provided.";
auto __ctor_arg_70f7dcbb = "type";
return new ArgumentException(__ctor_arg_a9604fdd, __ctor_arg_70f7dcbb);
})();
    }
    if (arrayCount < 0)
    {
throw ([&]() {
auto __ctor_arg_c9c890cb = "arrayCount";
auto __ctor_arg_4b3bc482 = "Array count cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_c9c890cb, __ctor_arg_4b3bc482);
})();
    }
    if (memberSize < 0)
    {
throw ([&]() {
auto __ctor_arg_20948ec8 = "memberSize";
auto __ctor_arg_9d25c648 = "Member size cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_20948ec8, __ctor_arg_9d25c648);
})();
    }
    if (RequiresRegisterAlignment(type, arrayCount))
    {
return AlignToRegister(currentOffset);    }
const int32_t registerOffset = currentOffset % RegisterSizeInBytes;
    if (registerOffset + memberSize > RegisterSizeInBytes)
    {
return AlignToRegister(currentOffset);    }
return currentOffset;}

int32_t HlslShaderBindingParser::ComputeMemberStorageSize(std::string type, int32_t arrayCount)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_5571cd73 = "Member type must be provided.";
auto __ctor_arg_a40ee601 = "type";
return new ArgumentException(__ctor_arg_5571cd73, __ctor_arg_a40ee601);
})();
    }
    if (arrayCount < 0)
    {
throw ([&]() {
auto __ctor_arg_698d8c21 = "arrayCount";
auto __ctor_arg_f83b4c12 = "Array count cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_698d8c21, __ctor_arg_f83b4c12);
})();
    }
    if (arrayCount > 0)
    {
return ComputeArrayStorageSize(type, arrayCount);    }
else     if (IsMatrixType(type))
    {
return ComputeMatrixStorageSize(type);    }
return ComputeScalarOrVectorStorageSize(type);}

int32_t HlslShaderBindingParser::ComputeScalarOrVectorStorageSize(std::string type)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_11c3ca6a = "Member type must be provided.";
auto __ctor_arg_8a28fe07 = "type";
return new ArgumentException(__ctor_arg_11c3ca6a, __ctor_arg_8a28fe07);
})();
    }
const std::string normalizedType = String::Trim(type);
    if (String::Equals(normalizedType, "matrix", StringComparison::Ordinal))
    {
return RegisterSizeInBytes * 4;    }
int32_t componentCount;
    if (TryParseVectorComponentCount(normalizedType, componentCount))
    {
return ResolveScalarTypeSize(normalizedType) * componentCount;    }
return ResolveScalarTypeSize(normalizedType);}

std::string HlslShaderBindingParser::ExtractBaseType(std::string type)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_584c29e7 = "Type must be provided.";
auto __ctor_arg_547bee2a = "type";
return new ArgumentException(__ctor_arg_584c29e7, __ctor_arg_547bee2a);
})();
    }
int32_t numericIndex = static_cast<int32_t>(type.size());
for (int32_t characterIndex = 0; characterIndex < static_cast<int32_t>(type.size()); characterIndex++) {
    if (String::IsDigit(type[characterIndex]))
    {
numericIndex = characterIndex;
break;
    }
}
return String::Substring(type, 0, numericIndex);}

bool HlslShaderBindingParser::IsMatrixType(std::string type)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_fcdba0f8 = "Type must be provided.";
auto __ctor_arg_f992f443 = "type";
return new ArgumentException(__ctor_arg_fcdba0f8, __ctor_arg_f992f443);
})();
    }
    if (String::Equals(type, "matrix", StringComparison::Ordinal))
    {
return true;    }
return MatrixTypePattern.IsMatch(type);}

Array<::ShaderConstantMember*>* HlslShaderBindingParser::ParseConstantBufferMembers(std::string body)
{
    if (String::IsNullOrEmpty(body))
    {
throw new ArgumentNullException("body");
    }
List<::ShaderConstantMember*> *members = new List<::ShaderConstantMember*>();
MatchCollection matches = ConstantBufferMemberPattern.Matches(body);
int32_t currentOffset = 0;
for (int32_t matchIndex = 0; matchIndex < matches.Count; matchIndex++) {
Match match = matches[matchIndex];
    if (!match.Success)
    {
continue;
    }
const std::string type = match.Groups["type"].Value;
const std::string name = match.Groups["name"].Value;
const int32_t arrayCount = ParseOptionalInt(match.Groups["count"].Value, 0);
const int32_t memberSize = ComputeMemberStorageSize(type, arrayCount);
const int32_t memberOffset = ComputeMemberOffset(currentOffset, type, arrayCount, memberSize);
members->Add(new ::ShaderConstantMember(name, type, memberOffset, memberSize));
currentOffset = memberOffset + memberSize;
}
return members->ToArray();}

int32_t HlslShaderBindingParser::ParseOptionalInt(std::string text, int32_t fallbackValue)
{
    if (String::IsNullOrWhiteSpace(text))
    {
return fallbackValue;    }
return ParseRequiredInt(text, "optional integer");}

int32_t HlslShaderBindingParser::ParseRequiredInt(std::string text, std::string label)
{
    if (String::IsNullOrWhiteSpace(text))
    {
throw ([&]() {
auto __ctor_arg_e7383678 = "Integer text must be provided.";
auto __ctor_arg_97f93ec4 = "text";
return new ArgumentException(__ctor_arg_e7383678, __ctor_arg_97f93ec4);
})();
    }
    if (String::IsNullOrWhiteSpace(label))
    {
throw ([&]() {
auto __ctor_arg_ba21e23a = "Label must be provided.";
auto __ctor_arg_8a73b978 = "label";
return new ArgumentException(__ctor_arg_ba21e23a, __ctor_arg_8a73b978);
})();
    }
int32_t value;
    if (Number::TryParse(text, value))
    {
return value;    }
throw new InvalidOperationException(std::string("The ") + label + std::string(" value '") + text + std::string("' is not a valid integer."));
}

bool HlslShaderBindingParser::RequiresRegisterAlignment(std::string type, int32_t arrayCount)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_f7adde29 = "Member type must be provided.";
auto __ctor_arg_e40c60d4 = "type";
return new ArgumentException(__ctor_arg_f7adde29, __ctor_arg_e40c60d4);
})();
    }
    if (arrayCount < 0)
    {
throw ([&]() {
auto __ctor_arg_392fce9c = "arrayCount";
auto __ctor_arg_54d164d0 = "Array count cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_392fce9c, __ctor_arg_54d164d0);
})();
    }
    if (arrayCount > 0)
    {
return true;    }
return IsMatrixType(type);}

::ShaderResourceType HlslShaderBindingParser::ResolveResourceType(std::string typeText, std::string registerClass)
{
    if (String::IsNullOrWhiteSpace(typeText))
    {
throw ([&]() {
auto __ctor_arg_c0ed260e = "Resource type text must be provided.";
auto __ctor_arg_200b8904 = "typeText";
return new ArgumentException(__ctor_arg_c0ed260e, __ctor_arg_200b8904);
})();
    }
    if (String::IsNullOrWhiteSpace(registerClass))
    {
throw ([&]() {
auto __ctor_arg_ce470d20 = "Register class must be provided.";
auto __ctor_arg_2c1f24b5 = "registerClass";
return new ArgumentException(__ctor_arg_ce470d20, __ctor_arg_2c1f24b5);
})();
    }
const std::string normalizedType = String::Trim(typeText);
    if (String::Equals(registerClass, "s", StringComparison::Ordinal))
    {
return ShaderResourceType::Sampler;    }
else     if (String::StartsWith(normalizedType, "TextureCube", StringComparison::Ordinal))
    {
return ShaderResourceType::TextureCube;    }
else     if (String::StartsWith(normalizedType, "Texture2D", StringComparison::Ordinal))
    {
return ShaderResourceType::Texture2D;    }
else     if (String::StartsWith(normalizedType, "RWTexture2D", StringComparison::Ordinal))
    {
return ShaderResourceType::StorageTexture2D;    }
else     if (String::StartsWith(normalizedType, "RWStructuredBuffer", StringComparison::Ordinal) || String::StartsWith(normalizedType, "RWBuffer", StringComparison::Ordinal) || String::Equals(normalizedType, "ByteAddressBuffer", StringComparison::Ordinal) || String::Equals(normalizedType, "RWByteAddressBuffer", StringComparison::Ordinal) || String::StartsWith(normalizedType, "AppendStructuredBuffer", StringComparison::Ordinal) || String::StartsWith(normalizedType, "ConsumeStructuredBuffer", StringComparison::Ordinal))
    {
return ShaderResourceType::StorageBuffer;    }
else     if (String::StartsWith(normalizedType, "StructuredBuffer", StringComparison::Ordinal) || String::StartsWith(normalizedType, "Buffer", StringComparison::Ordinal))
    {
return ShaderResourceType::Buffer;    }
throw new InvalidOperationException(std::string("Unsupported HLSL resource type '") + typeText + std::string("'."));
}

int32_t HlslShaderBindingParser::ResolveScalarTypeSize(std::string type)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_ece26c33 = "Scalar type must be provided.";
auto __ctor_arg_4977592f = "type";
return new ArgumentException(__ctor_arg_ece26c33, __ctor_arg_4977592f);
})();
    }
const std::string baseType = ExtractBaseType(type);
{
const std::string __switchValue41_1 = baseType;
if (String::Equals(__switchValue41_1, "bool") || String::Equals(__switchValue41_1, "int") || String::Equals(__switchValue41_1, "uint") || String::Equals(__switchValue41_1, "float") || String::Equals(__switchValue41_1, "half") || String::Equals(__switchValue41_1, "min16float") || String::Equals(__switchValue41_1, "min10float") || String::Equals(__switchValue41_1, "min16int") || String::Equals(__switchValue41_1, "min12int") || String::Equals(__switchValue41_1, "min16uint")) {
return 4;}
else {
throw new InvalidOperationException(std::string("Unsupported HLSL scalar type '") + type + std::string("'."));
}
}

}

std::string HlslShaderBindingParser::StripComments(std::string source)
{
    if (String::IsNullOrEmpty(source))
    {
throw new ArgumentNullException("source");
    }
const std::string withoutBlockComments = BlockCommentPattern.Replace(source, String::Empty);
return LineCommentPattern.Replace(withoutBlockComments, String::Empty);}

bool HlslShaderBindingParser::TryParseVectorComponentCount(std::string type, int32_t& componentCount)
{
    if (String::IsNullOrWhiteSpace(type))
    {
throw ([&]() {
auto __ctor_arg_722caf1a = "Type must be provided.";
auto __ctor_arg_aae80197 = "type";
return new ArgumentException(__ctor_arg_722caf1a, __ctor_arg_aae80197);
})();
    }
Match match = MatrixTypePattern.Match(type);
    if (match.Success)
    {
componentCount = 0;
return false;    }
const int32_t trailingDigitIndex = static_cast<int32_t>(type.size()) - 1;
    if (trailingDigitIndex < 0 || !String::IsDigit(type[trailingDigitIndex]))
    {
componentCount = 1;
return false;    }
componentCount = ParseRequiredInt(String::Substring(type, trailingDigitIndex, 1), "vector component count");
return true;}

