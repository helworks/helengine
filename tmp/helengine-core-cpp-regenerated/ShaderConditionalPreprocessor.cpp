#ifdef DrawText
#undef DrawText
#endif
#include "ShaderConditionalPreprocessor.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_stack.hpp"
#include "system/text/string-builder.hpp"
#include "runtime/native_string.hpp"
#include "system/io/string-reader.hpp"
#include "ShaderConditionalFrame.hpp"
#include "runtime/array.hpp"
#include "system/string_comparer.hpp"
#include "ShaderDefine.hpp"
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

std::string ShaderConditionalPreprocessor::Preprocess(std::string source, List<::ShaderDefine*>* defines)
{
    if (String::IsNullOrEmpty(source))
    {
throw new ArgumentNullException("source");
    }
    if (defines == nullptr)
    {
throw new ArgumentNullException("defines");
    }
Dictionary<std::string, std::string> *defineLookup = BuildDefineLookup(defines);
Stack<::ShaderConditionalFrame*> *frames = new Stack<::ShaderConditionalFrame*>();
StringBuilder *builder = new StringBuilder(static_cast<int32_t>(source.size()));
{
StringReader *reader = new StringReader(source);
StringReaderLine line = reader->ReadLine();
while (!String::IsNullOrEmpty(line)) {
ProcessLine(line, defineLookup, frames, builder);
line = reader->ReadLine();
}
if (reader != nullptr) {
reader->Dispose();
}
}
    if (frames->Count > 0)
    {
throw new InvalidOperationException("Shader source ended before all conditional preprocessor blocks were closed.");
    }
return builder->ToString();}

void ShaderConditionalPreprocessor::AppendSourceLine(StringBuilder* builder, std::string line, bool includeLine)
{
    if (builder == nullptr)
    {
throw new ArgumentNullException("builder");
    }
    if (String::IsNullOrEmpty(line))
    {
throw new ArgumentNullException("line");
    }
    if (includeLine)
    {
builder->AppendLine(line);
return;    }
builder->AppendLine();
}

void ShaderConditionalPreprocessor::ApplyDefineDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup)
{
    if (String::IsNullOrEmpty(trimmedLine))
    {
throw new ArgumentNullException("trimmedLine");
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
const std::string remainder = GetDirectiveArgument(trimmedLine, "#define");
Array<std::string> *parts = String::Split(remainder, new Array<char>({ ' ', '\t' }), 2, StringSplitOptions::RemoveEmptyEntries);
    if (parts->Length == 0)
    {
throw new InvalidOperationException("Shader source contains #define without an identifier.");
    }
const std::string value = parts->Length > 1 ? String::Trim((*parts)[1]) : "1";
(*defineLookup)[(*parts)[0]] = value;
}

void ShaderConditionalPreprocessor::ApplyElseDirective(Stack<::ShaderConditionalFrame*>* frames)
{
::ShaderConditionalFrame *frame = GetCurrentFrame(frames);
    if (frame->get_HasElseBranch())
    {
throw new InvalidOperationException("Shader source cannot contain more than one #else branch in the same conditional block.");
    }
const bool branchIncluded = frame->get_ParentIncluded() && !frame->get_BranchMatched();
frame->set_CurrentIncluded(branchIncluded);
frame->set_BranchMatched(true);
frame->set_HasElseBranch(true);
}

void ShaderConditionalPreprocessor::ApplyElseIfDirective(std::string expression, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames)
{
    if (String::IsNullOrWhiteSpace(expression))
    {
throw ([&]() {
auto __ctor_arg_0000010E = "Conditional expression must be provided.";
auto __ctor_arg_0000010F = "expression";
return new ArgumentException(__ctor_arg_0000010E, __ctor_arg_0000010F);
})();
    }
::ShaderConditionalFrame *frame = GetCurrentFrame(frames);
    if (frame->get_HasElseBranch())
    {
throw new InvalidOperationException("Shader source cannot use #elif after #else in the same conditional block.");
    }
const bool branchIncluded = frame->get_ParentIncluded() && !frame->get_BranchMatched() && EvaluateExpression(expression, defineLookup);
frame->set_CurrentIncluded(branchIncluded);
    if (branchIncluded)
    {
frame->set_BranchMatched(true);
    }
}

void ShaderConditionalPreprocessor::ApplyEndIfDirective(Stack<::ShaderConditionalFrame*>* frames)
{
    if (frames == nullptr)
    {
throw new ArgumentNullException("frames");
    }
else     if (frames->Count == 0)
    {
throw new InvalidOperationException("Shader source contains #endif without a matching opening conditional directive.");
    }
frames->Pop();
}

void ShaderConditionalPreprocessor::ApplyUndefDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup)
{
    if (String::IsNullOrEmpty(trimmedLine))
    {
throw new ArgumentNullException("trimmedLine");
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
const std::string identifier = GetDirectiveArgument(trimmedLine, "#undef");
defineLookup->Remove(identifier);
}

Dictionary<std::string, std::string>* ShaderConditionalPreprocessor::BuildDefineLookup(List<::ShaderDefine*>* defines)
{
    if (defines == nullptr)
    {
throw new ArgumentNullException("defines");
    }
Dictionary<std::string, std::string> *lookup = new Dictionary<std::string, std::string>(StringComparer::Ordinal);
for (int32_t defineIndex = 0; defineIndex < defines->get_Count(); defineIndex++) {
::ShaderDefine *define = (*defines)[defineIndex];
    if (define == nullptr)
    {
throw new InvalidOperationException("Shader define lists must not contain null entries.");
    }
(*lookup)[define->get_Name()] = define->get_Value();
}
return lookup;}

bool ShaderConditionalPreprocessor::EvaluateExpression(std::string expression, Dictionary<std::string, std::string>* defineLookup)
{
    if (String::IsNullOrWhiteSpace(expression))
    {
throw ([&]() {
auto __ctor_arg_00000110 = "Conditional expression must be provided.";
auto __ctor_arg_00000111 = "expression";
return new ArgumentException(__ctor_arg_00000110, __ctor_arg_00000111);
})();
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
const std::string trimmedExpression = String::Trim(expression);
    if (String::StartsWith(trimmedExpression, "defined(", StringComparison::Ordinal) && String::EndsWith(trimmedExpression, ")", StringComparison::Ordinal))
    {
const std::string identifier = String::Trim(String::Substring(trimmedExpression, 8, static_cast<int32_t>(trimmedExpression.size()) - 8 - 1));
return IsDefineEnabled(identifier, defineLookup);    }
else     if (String::StartsWith(trimmedExpression, "!", StringComparison::Ordinal))
    {
const std::string nestedExpression = String::Trim(String::Substring(trimmedExpression, 1));
return !EvaluateExpression(nestedExpression, defineLookup);    }
else     if (String::Equals(trimmedExpression, "0", StringComparison::Ordinal))
    {
return false;    }
else     if (String::Equals(trimmedExpression, "1", StringComparison::Ordinal))
    {
return true;    }
return IsDefineEnabled(trimmedExpression, defineLookup);}

::ShaderConditionalFrame* ShaderConditionalPreprocessor::GetCurrentFrame(Stack<::ShaderConditionalFrame*>* frames)
{
    if (frames == nullptr)
    {
throw new ArgumentNullException("frames");
    }
else     if (frames->Count == 0)
    {
throw new InvalidOperationException("Shader source contains a conditional branch directive without a matching opening conditional directive.");
    }
return frames->Peek();}

std::string ShaderConditionalPreprocessor::GetDirectiveArgument(std::string trimmedLine, std::string directive)
{
    if (String::IsNullOrWhiteSpace(trimmedLine))
    {
throw ([&]() {
auto __ctor_arg_00000112 = "Directive line must be provided.";
auto __ctor_arg_00000113 = "trimmedLine";
return new ArgumentException(__ctor_arg_00000112, __ctor_arg_00000113);
})();
    }
    if (String::IsNullOrWhiteSpace(directive))
    {
throw ([&]() {
auto __ctor_arg_00000114 = "Directive keyword must be provided.";
auto __ctor_arg_00000115 = "directive";
return new ArgumentException(__ctor_arg_00000114, __ctor_arg_00000115);
})();
    }
const std::string argument = String::Trim(String::Substring(trimmedLine, static_cast<int32_t>(directive.size())));
    if (String::IsNullOrWhiteSpace(argument))
    {
throw new InvalidOperationException(std::string("Shader source directive '") + directive + std::string("' is missing its required argument."));
    }
return argument;}

bool ShaderConditionalPreprocessor::IsCurrentBranchIncluded(Stack<::ShaderConditionalFrame*>* frames)
{
    if (frames == nullptr)
    {
throw new ArgumentNullException("frames");
    }
    if (frames->Count == 0)
    {
return true;    }
return frames->Peek()->get_CurrentIncluded();}

bool ShaderConditionalPreprocessor::IsDefineEnabled(std::string identifier, Dictionary<std::string, std::string>* defineLookup)
{
    if (String::IsNullOrWhiteSpace(identifier))
    {
throw ([&]() {
auto __ctor_arg_00000116 = "Define identifier must be provided.";
auto __ctor_arg_00000117 = "identifier";
return new ArgumentException(__ctor_arg_00000116, __ctor_arg_00000117);
})();
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
std::string value;
    if (!defineLookup->TryGetValue(identifier, value))
    {
return false;    }
else     if (String::IsNullOrWhiteSpace(value))
    {
return true;    }
else     if (String::Equals(value, "0", StringComparison::Ordinal))
    {
return false;    }
return true;}

void ShaderConditionalPreprocessor::ProcessLine(std::string line, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames, StringBuilder* builder)
{
    if (String::IsNullOrEmpty(line))
    {
throw new ArgumentNullException("line");
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
    if (frames == nullptr)
    {
throw new ArgumentNullException("frames");
    }
    if (builder == nullptr)
    {
throw new ArgumentNullException("builder");
    }
const std::string trimmedLine = String::TrimStart(line);
    if (!String::StartsWith(trimmedLine, "#", StringComparison::Ordinal))
    {
AppendSourceLine(builder, line, IsCurrentBranchIncluded(frames));
return;    }
    if (TryProcessConditionalDirective(trimmedLine, defineLookup, frames))
    {
builder->AppendLine();
return;    }
    if (TryProcessDefineDirective(trimmedLine, defineLookup, frames))
    {
builder->AppendLine();
return;    }
builder->AppendLine();
}

bool ShaderConditionalPreprocessor::TryProcessConditionalDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames)
{
    if (String::IsNullOrEmpty(trimmedLine))
    {
throw new ArgumentNullException("trimmedLine");
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
    if (frames == nullptr)
    {
throw new ArgumentNullException("frames");
    }
    if (String::StartsWith(trimmedLine, "#ifdef", StringComparison::Ordinal))
    {
const std::string identifier = GetDirectiveArgument(trimmedLine, "#ifdef");
const bool branchIncluded = IsCurrentBranchIncluded(frames) && IsDefineEnabled(identifier, defineLookup);
frames->Push(([&]() {
auto __ctor_arg_00000118 = IsCurrentBranchIncluded(frames);
auto __ctor_arg_00000119 = branchIncluded;
auto __ctor_arg_0000011A = branchIncluded;
return new ::ShaderConditionalFrame(__ctor_arg_00000118, __ctor_arg_00000119, __ctor_arg_0000011A);
})());
return true;    }
else     if (String::StartsWith(trimmedLine, "#ifndef", StringComparison::Ordinal))
    {
const std::string identifier = GetDirectiveArgument(trimmedLine, "#ifndef");
const bool branchIncluded = IsCurrentBranchIncluded(frames) && !IsDefineEnabled(identifier, defineLookup);
frames->Push(([&]() {
auto __ctor_arg_0000011B = IsCurrentBranchIncluded(frames);
auto __ctor_arg_0000011C = branchIncluded;
auto __ctor_arg_0000011D = branchIncluded;
return new ::ShaderConditionalFrame(__ctor_arg_0000011B, __ctor_arg_0000011C, __ctor_arg_0000011D);
})());
return true;    }
else     if (String::StartsWith(trimmedLine, "#if", StringComparison::Ordinal))
    {
const std::string expression = GetDirectiveArgument(trimmedLine, "#if");
const bool branchIncluded = IsCurrentBranchIncluded(frames) && EvaluateExpression(expression, defineLookup);
frames->Push(([&]() {
auto __ctor_arg_0000011E = IsCurrentBranchIncluded(frames);
auto __ctor_arg_0000011F = branchIncluded;
auto __ctor_arg_00000120 = branchIncluded;
return new ::ShaderConditionalFrame(__ctor_arg_0000011E, __ctor_arg_0000011F, __ctor_arg_00000120);
})());
return true;    }
else     if (String::StartsWith(trimmedLine, "#elif", StringComparison::Ordinal))
    {
const std::string expression = GetDirectiveArgument(trimmedLine, "#elif");
ApplyElseIfDirective(expression, defineLookup, frames);
return true;    }
else     if (String::StartsWith(trimmedLine, "#else", StringComparison::Ordinal))
    {
ApplyElseDirective(frames);
return true;    }
else     if (String::StartsWith(trimmedLine, "#endif", StringComparison::Ordinal))
    {
ApplyEndIfDirective(frames);
return true;    }
return false;}

bool ShaderConditionalPreprocessor::TryProcessDefineDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames)
{
    if (String::IsNullOrEmpty(trimmedLine))
    {
throw new ArgumentNullException("trimmedLine");
    }
    if (defineLookup == nullptr)
    {
throw new ArgumentNullException("defineLookup");
    }
    if (frames == nullptr)
    {
throw new ArgumentNullException("frames");
    }
    if (!IsCurrentBranchIncluded(frames))
    {
return String::StartsWith(trimmedLine, "#define", StringComparison::Ordinal) || String::StartsWith(trimmedLine, "#undef", StringComparison::Ordinal);    }
    if (String::StartsWith(trimmedLine, "#define", StringComparison::Ordinal))
    {
ApplyDefineDirective(trimmedLine, defineLookup);
return true;    }
else     if (String::StartsWith(trimmedLine, "#undef", StringComparison::Ordinal))
    {
ApplyUndefDirective(trimmedLine, defineLookup);
return true;    }
return false;}

