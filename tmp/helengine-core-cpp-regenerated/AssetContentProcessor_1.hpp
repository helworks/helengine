#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IContentProcessor;
template <typename T> class IContentProcessor_1;
class Asset;
class AssetSerializer;

#include "IContentProcessor.hpp"
#include "IContentProcessor_1.hpp"
#include "runtime/native_exceptions.hpp"
#include "Asset.hpp"
#include "AssetSerializer.hpp"
#include "AssetSerializer.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_type.hpp"
#include "system/io/stream.hpp"

template <typename TAsset>
class AssetContentProcessor_1 : public IContentProcessor_1<TAsset>
{
public:
    Type* get_OutputType();

    TAsset Read(::Stream* stream);
private:
    void* ReadObject(::Stream* stream);
};
