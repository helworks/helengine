#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class MaterialLayout;
class RuntimeTexture;
class MaterialLayoutBinding;

#include "MaterialLayout.hpp"
#include "runtime/native_exceptions.hpp"
#include "RuntimeTexture.hpp"
#include "MaterialLayoutBinding.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "MaterialLayout.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "RuntimeTexture.hpp"
#include "runtime/native_string.hpp"

class MaterialPropertyBlock
{
public:
    ::MaterialLayout* get_Layout();

    void CopyMatchingValuesFrom(::MaterialPropertyBlock* source);

    Array<uint8_t>* GetConstantBufferData(int32_t bindingIndex);

    ::RuntimeTexture* GetTexture(int32_t bindingIndex);

    MaterialPropertyBlock(::MaterialLayout* layout);

    void SetConstantBufferData(std::string bindingName, Array<uint8_t>* data);

    void SetConstantBufferData(int32_t bindingIndex, Array<uint8_t>* data);

    void SetTexture(std::string bindingName, ::RuntimeTexture* texture);

    void SetTexture(int32_t bindingIndex, ::RuntimeTexture* texture);

    bool TryGetFirstTexture(::RuntimeTexture*& texture);
private:
    Array<Array<uint8_t>*>* ConstantBufferValues;

    ::MaterialLayout* LayoutValue;

    Array<::RuntimeTexture*>* TextureValues;

    void CopyMatchingConstantBufferValuesFrom(::MaterialPropertyBlock* source);

    void CopyMatchingTextureValuesFrom(::MaterialPropertyBlock* source);

    void ValidateConstantBufferBindingIndex(int32_t bindingIndex);

    void ValidateTextureBindingIndex(int32_t bindingIndex);
};
