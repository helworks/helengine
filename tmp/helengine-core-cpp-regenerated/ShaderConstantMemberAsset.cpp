#ifdef DrawText
#undef DrawText
#endif
#include "ShaderConstantMemberAsset.hpp"
#include "ShaderConstantMember.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderConstantMemberAsset.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

ShaderConstantMemberAsset::ShaderConstantMemberAsset() : Name(), Offset(0), Size(0), Type()
{
}

::ShaderConstantMemberAsset* ShaderConstantMemberAsset::FromMember(::ShaderConstantMember* member)
{
    if (member == nullptr)
    {
throw new ArgumentNullException("member");
    }
::ShaderConstantMemberAsset *asset = ([&]() {
auto __object_00000129 = new ::ShaderConstantMemberAsset();
__object_00000129->Name = member->get_Name();
__object_00000129->Type = member->get_Type();
__object_00000129->Offset = member->get_Offset();
__object_00000129->Size = member->get_Size();
return __object_00000129;
})();
return asset;}

::ShaderConstantMember* ShaderConstantMemberAsset::ToMember()
{
this->Validate();
return new ::ShaderConstantMember(this->Name, this->Type, this->Offset, this->Size);}

void ShaderConstantMemberAsset::Validate()
{
    if (String::IsNullOrWhiteSpace(this->Name))
    {
throw new InvalidOperationException("Member name must be provided.");
    }
else     if (String::IsNullOrWhiteSpace(this->Type))
    {
throw new InvalidOperationException("Member type must be provided.");
    }
else     if (this->Offset < 0)
    {
throw new InvalidOperationException("Member offset cannot be negative.");
    }
else     if (this->Size < 0)
    {
throw new InvalidOperationException("Member size cannot be negative.");
    }
}

