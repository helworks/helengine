#ifdef DrawText
#undef DrawText
#endif
#include "AnchorData.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
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
#include "system/bit_converter.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/text/encoding.hpp"

AnchorData::AnchorData() : BottomDistance(), LeftDistance(), RightDistance(), TopDistance()
{
}

Nullable<float> AnchorData::get_BottomDistance()
{
return this->BottomDistance;
}

void AnchorData::set_BottomDistance(Nullable<float> value)
{
this->BottomDistance = value;
}

Nullable<float> AnchorData::get_LeftDistance()
{
return this->LeftDistance;
}

void AnchorData::set_LeftDistance(Nullable<float> value)
{
this->LeftDistance = value;
}

Nullable<float> AnchorData::get_RightDistance()
{
return this->RightDistance;
}

void AnchorData::set_RightDistance(Nullable<float> value)
{
this->RightDistance = value;
}

Nullable<float> AnchorData::get_TopDistance()
{
return this->TopDistance;
}

void AnchorData::set_TopDistance(Nullable<float> value)
{
this->TopDistance = value;
}

