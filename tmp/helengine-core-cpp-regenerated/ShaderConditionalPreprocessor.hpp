#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderConditionalFrame;
class ShaderDefine;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_stack.hpp"
#include "system/text/string-builder.hpp"
#include "runtime/native_string.hpp"
#include "system/io/string-reader.hpp"
#include "system/io/string-reader.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/text/string-builder.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderConditionalFrame.hpp"
#include "ShaderConditionalFrame.hpp"
#include "runtime/array.hpp"
#include "runtime/native_dictionary.hpp"
#include "system/string_comparer.hpp"
#include "system/string_comparer.hpp"
#include "runtime/native_list.hpp"
#include "ShaderDefine.hpp"

class ShaderConditionalPreprocessor
{
public:
    static std::string Preprocess(std::string source, List<::ShaderDefine*>* defines);
private:
    static void AppendSourceLine(StringBuilder* builder, std::string line, bool includeLine);

    static void ApplyDefineDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup);

    static void ApplyElseDirective(Stack<::ShaderConditionalFrame*>* frames);

    static void ApplyElseIfDirective(std::string expression, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames);

    static void ApplyEndIfDirective(Stack<::ShaderConditionalFrame*>* frames);

    static void ApplyUndefDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup);

    static Dictionary<std::string, std::string>* BuildDefineLookup(List<::ShaderDefine*>* defines);

    static bool EvaluateExpression(std::string expression, Dictionary<std::string, std::string>* defineLookup);

    static ::ShaderConditionalFrame* GetCurrentFrame(Stack<::ShaderConditionalFrame*>* frames);

    static std::string GetDirectiveArgument(std::string trimmedLine, std::string directive);

    static bool IsCurrentBranchIncluded(Stack<::ShaderConditionalFrame*>* frames);

    static bool IsDefineEnabled(std::string identifier, Dictionary<std::string, std::string>* defineLookup);

    static void ProcessLine(std::string line, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames, StringBuilder* builder);

    static bool TryProcessConditionalDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames);

    static bool TryProcessDefineDirective(std::string trimmedLine, Dictionary<std::string, std::string>* defineLookup, Stack<::ShaderConditionalFrame*>* frames);
};
