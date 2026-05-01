#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderConditionalPreprocessor;
class ShaderConstantMember;
class ShaderBinding;
class ShaderBindingPolicy;
class ShaderDefine;

#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderConditionalPreprocessor.hpp"
#include "ShaderConditionalPreprocessor.hpp"
#include "runtime/native_list.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "ShaderConstantMember.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "ShaderResourceType.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "runtime/array.hpp"
#include "ShaderBinding.hpp"
#include "ShaderBindingPolicy.hpp"
#include "runtime/native_list.hpp"
#include "ShaderDefine.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "ShaderResourceType.hpp"

class HlslShaderBindingParser
{
public:
    static Array<::ShaderBinding*>* ParseBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy);

    static Array<::ShaderBinding*>* ParseBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy, List<::ShaderDefine*>* defines);
private:
    static Regex BlockCommentPattern;

    static Regex ConstantBufferMemberPattern;

    static Regex ConstantBufferPattern;

    static Regex LineCommentPattern;

    static Regex MatrixTypePattern;

    static int32_t RegisterSizeInBytes;

    static Regex ResourcePattern;

    static void AddConstantBufferBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy, List<::ShaderBinding*>* bindings);

    static void AddResourceBindings(std::string source, ::ShaderBindingPolicy* bindingPolicy, List<::ShaderBinding*>* bindings);

    static int32_t AlignToRegister(int32_t offset);

    static int32_t ComputeArrayStorageSize(std::string type, int32_t arrayCount);

    static int32_t ComputeConstantBufferSize(List<::ShaderConstantMember*>* members);

    static int32_t ComputeMatrixStorageSize(std::string type);

    static int32_t ComputeMemberOffset(int32_t currentOffset, std::string type, int32_t arrayCount, int32_t memberSize);

    static int32_t ComputeMemberStorageSize(std::string type, int32_t arrayCount);

    static int32_t ComputeScalarOrVectorStorageSize(std::string type);

    static std::string ExtractBaseType(std::string type);

    static bool IsMatrixType(std::string type);

    static Array<::ShaderConstantMember*>* ParseConstantBufferMembers(std::string body);

    static int32_t ParseOptionalInt(std::string text, int32_t fallbackValue);

    static int32_t ParseRequiredInt(std::string text, std::string label);

    static bool RequiresRegisterAlignment(std::string type, int32_t arrayCount);

    static ::ShaderResourceType ResolveResourceType(std::string typeText, std::string registerClass);

    static int32_t ResolveScalarTypeSize(std::string type);

    static std::string StripComments(std::string source);

    static bool TryParseVectorComponentCount(std::string type, int32_t& componentCount);
};
