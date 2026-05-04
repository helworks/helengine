#ifdef DrawText
#undef DrawText
#endif
#include "ShaderConditionalFrame.hpp"
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
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

bool ShaderConditionalFrame::get_BranchMatched()
{
return this->BranchMatched;
}

void ShaderConditionalFrame::set_BranchMatched(bool value)
{
this->BranchMatched = value;
}

bool ShaderConditionalFrame::get_CurrentIncluded()
{
return this->CurrentIncluded;
}

void ShaderConditionalFrame::set_CurrentIncluded(bool value)
{
this->CurrentIncluded = value;
}

bool ShaderConditionalFrame::get_HasElseBranch()
{
return this->HasElseBranch;
}

void ShaderConditionalFrame::set_HasElseBranch(bool value)
{
this->HasElseBranch = value;
}

bool ShaderConditionalFrame::get_ParentIncluded()
{
return this->ParentIncluded;
}

ShaderConditionalFrame::ShaderConditionalFrame(bool parentIncluded, bool branchMatched, bool currentIncluded) : BranchMatched(), CurrentIncluded(), HasElseBranch(), ParentIncluded()
{
this->ParentIncluded = parentIncluded;
this->set_BranchMatched(branchMatched);
this->set_CurrentIncluded(currentIncluded);
}

