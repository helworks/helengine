#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeManifestJsonReader.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "RuntimeStartupManifest.hpp"
#include "runtime/array.hpp"
#include "RuntimeCodeModuleManifest.hpp"
#include "runtime/native_list.hpp"
#include "RuntimeCodeModuleLoadState.hpp"
#include "RuntimeCodeModuleManifestEntry.hpp"
#include "system/text/string-builder.hpp"
#include "RuntimeStorageProfileId.hpp"
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

::RuntimeCodeModuleManifest* RuntimeManifestJsonReader::ReadRuntimeCodeModuleManifest(std::string json)
{
    if (String::IsNullOrWhiteSpace(json))
    {
throw ([&]() {
auto __ctor_arg_000000C6 = "Runtime code-module manifest JSON is required.";
auto __ctor_arg_000000C7 = "json";
return new ArgumentException(__ctor_arg_000000C6, __ctor_arg_000000C7);
})();
    }
Array<::RuntimeCodeModuleManifestEntry*> *entries = ReadRuntimeCodeModuleEntries(json);
return new ::RuntimeCodeModuleManifest(entries);}

::RuntimeStartupManifest* RuntimeManifestJsonReader::ReadRuntimeStartupManifest(std::string json)
{
    if (String::IsNullOrWhiteSpace(json))
    {
throw ([&]() {
auto __ctor_arg_000000C8 = "Runtime startup manifest JSON is required.";
auto __ctor_arg_000000C9 = "json";
return new ArgumentException(__ctor_arg_000000C8, __ctor_arg_000000C9);
})();
    }
const std::string startupSceneId = ReadRequiredStringProperty(json, "StartupSceneId");
const std::string storageProfileJson = ReadRequiredObjectProperty(json, "StorageProfileId");
const std::string storageProfileValue = ReadRequiredStringProperty(storageProfileJson, "Value");
return ([&]() {
auto __ctor_arg_000000CA = startupSceneId;
auto __ctor_arg_000000CB = new ::RuntimeStorageProfileId(storageProfileValue);
return new ::RuntimeStartupManifest(__ctor_arg_000000CA, __ctor_arg_000000CB);
})();}

int32_t RuntimeManifestJsonReader::FindMatchingJsonDelimiter(std::string json, int32_t openIndex, char openDelimiter, char closeDelimiter)
{
    if (json[openIndex] != openDelimiter)
    {
throw new InvalidOperationException("JSON delimiter scan started on the wrong character.");
    }
int32_t depth = 0;
bool insideString = false;
bool escaping = false;
for (int32_t index = openIndex; index < static_cast<int32_t>(json.size()); index++) {
const char current = json[index];
    if (insideString)
    {
    if (escaping)
    {
escaping = false;
    }
else     if (current == '\\')
    {
escaping = true;
    }
else     if (current == '"')
    {
insideString = false;
    }
continue;
    }
    if (current == '"')
    {
insideString = true;
continue;
    }
    if (current == openDelimiter)
    {
depth++;
continue;
    }
    if (current == closeDelimiter)
    {
depth--;
    if (depth == 0)
    {
return index;    }
    }
}
throw new InvalidOperationException("JSON delimiter was not terminated.");
}

int32_t RuntimeManifestJsonReader::FindPropertyNameIndex(std::string json, std::string propertyName)
{
    if (String::IsNullOrWhiteSpace(json) || String::IsNullOrWhiteSpace(propertyName))
    {
return -1;    }
const int32_t searchLength = static_cast<int32_t>(propertyName.size()) + 2;
    if (static_cast<int32_t>(json.size()) < searchLength)
    {
return -1;    }
for (int32_t index = 0; index <= static_cast<int32_t>(json.size()) - searchLength; index++) {
    if (json[index] != '"')
    {
continue;
    }
bool matches = true;
for (int32_t offset = 0; offset < static_cast<int32_t>(propertyName.size()); offset++) {
    if (json[index + offset + 1] != propertyName[offset])
    {
matches = false;
break;
    }
}
    if (matches && json[index + static_cast<int32_t>(propertyName.size()) + 1] == '"')
    {
return index;    }
}
return -1;}

int32_t RuntimeManifestJsonReader::FindRequiredPropertyValueStart(std::string json, std::string propertyName)
{
    if (String::IsNullOrWhiteSpace(propertyName))
    {
throw ([&]() {
auto __ctor_arg_000000CC = "Property name is required.";
auto __ctor_arg_000000CD = "propertyName";
return new ArgumentException(__ctor_arg_000000CC, __ctor_arg_000000CD);
})();
    }
const int32_t propertyNameIndex = FindPropertyNameIndex(json, propertyName);
    if (propertyNameIndex < 0)
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' was not found in the JSON object."));
    }
int32_t cursor = propertyNameIndex + static_cast<int32_t>(propertyName.size()) + 2;
cursor = SkipWhitespace(json, cursor);
    if (cursor >= static_cast<int32_t>(json.size()) || json[cursor] != ':')
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' was not followed by a JSON value."));
    }
cursor = SkipWhitespace(json, cursor + 1);
    if (cursor >= static_cast<int32_t>(json.size()))
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' was not followed by a JSON value."));
    }
return cursor;}

bool RuntimeManifestJsonReader::IsJsonPrimitiveTerminator(char value)
{
    if (value == ',' || value == ']' || value == '}' || value == ' ' || value == '\t' || value == '\r' || value == '\n')
    {
return true;    }
return false;}

bool RuntimeManifestJsonReader::IsJsonWhitespace(char value)
{
    if (value == ' ' || value == '\t' || value == '\r' || value == '\n')
    {
return true;    }
return false;}

int32_t RuntimeManifestJsonReader::ReadJsonHexDigit(char value)
{
    if (value >= '0' && value <= '9')
    {
return value - '0';    }
    if (value >= 'a' && value <= 'f')
    {
return (value - 'a') + 10;    }
    if (value >= 'A' && value <= 'F')
    {
return (value - 'A') + 10;    }
throw new InvalidOperationException("JSON unicode escape contained a non-hexadecimal digit.");
}

std::string RuntimeManifestJsonReader::ReadJsonPrimitiveValue(std::string json, int32_t valueStart, int32_t& valueLength)
{
int32_t cursor = valueStart;
while (cursor < static_cast<int32_t>(json.size()) && !IsJsonPrimitiveTerminator(json[cursor])) {
cursor++;
}
valueLength = cursor - valueStart;
return String::Substring(json, valueStart, valueLength);}

std::string RuntimeManifestJsonReader::ReadJsonStringValue(std::string json, int32_t valueStart, int32_t& valueLength)
{
    if (valueStart < 0 || valueStart >= static_cast<int32_t>(json.size()) || json[valueStart] != '"')
    {
throw new InvalidOperationException("JSON string value was expected.");
    }
StringBuilder *builder = new StringBuilder();
bool escaping = false;
for (int32_t index = valueStart + 1; index < static_cast<int32_t>(json.size()); index++) {
const char current = json[index];
    if (escaping)
    {
    if (current == '"')
    {
builder->Append('"');
    }
else     if (current == '\\')
    {
builder->Append('\\');
    }
else     if (current == '/')
    {
builder->Append('/');
    }
else     if (current == 'b')
    {
builder->Append('\b');
    }
else     if (current == 'f')
    {
builder->Append('\f');
    }
else     if (current == 'n')
    {
builder->Append('\n');
    }
else     if (current == 'r')
    {
builder->Append('\r');
    }
else     if (current == 't')
    {
builder->Append('\t');
    }
else     if (current == 'u')
    {
    if (index + 4 >= static_cast<int32_t>(json.size()))
    {
throw new InvalidOperationException("JSON unicode escape was truncated.");
    }
int32_t codePoint = 0;
for (int32_t digitIndex = 1; digitIndex <= 4; digitIndex++) {
codePoint = (codePoint * 16) + ReadJsonHexDigit(json[index + digitIndex]);
}
builder->Append(static_cast<char>(codePoint));
index += 4;
    }
else {
throw new InvalidOperationException(std::string("Unsupported JSON escape sequence '\\") + std::string(1, current) + std::string("'."));
}
escaping = false;
continue;
    }
    if (current == '\\')
    {
escaping = true;
continue;
    }
    if (current == '"')
    {
valueLength = index - valueStart + 1;
return builder->ToString();    }
builder->Append(current);
}
throw new InvalidOperationException("JSON string value was not terminated.");
}

int32_t RuntimeManifestJsonReader::ReadJsonValueLength(std::string json, int32_t valueStart)
{
    if (valueStart < 0 || valueStart >= static_cast<int32_t>(json.size()))
    {
throw new InvalidOperationException("JSON value start was out of range.");
    }
const char firstCharacter = json[valueStart];
    if (firstCharacter == '"')
    {
int32_t valueLength = 0;
ReadJsonStringValue(json, valueStart, valueLength);
return valueLength;    }
    if (firstCharacter == '{')
    {
return FindMatchingJsonDelimiter(json, valueStart, '{', '}') - valueStart + 1;    }
    if (firstCharacter == '[')
    {
return FindMatchingJsonDelimiter(json, valueStart, '[', ']') - valueStart + 1;    }
int32_t cursor = valueStart;
while (cursor < static_cast<int32_t>(json.size())) {
const char current = json[cursor];
    if (IsJsonWhitespace(current) || current == ',' || current == ']' || current == '}')
    {
break;
    }
cursor++;
}
return cursor - valueStart;}

std::string RuntimeManifestJsonReader::ReadRequiredArrayProperty(std::string json, std::string propertyName)
{
const int32_t valueStart = FindRequiredPropertyValueStart(json, propertyName);
const int32_t valueLength = ReadJsonValueLength(json, valueStart);
    if (valueLength <= 0 || json[valueStart] != '[')
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' did not contain a JSON array."));
    }
return String::Substring(json, valueStart, valueLength);}

int32_t RuntimeManifestJsonReader::ReadRequiredIntegerProperty(std::string json, std::string propertyName)
{
const int32_t valueStart = FindRequiredPropertyValueStart(json, propertyName);
int32_t valueLength = 0;
const std::string valueText = ReadJsonPrimitiveValue(json, valueStart, valueLength);
int32_t value = 0;
    if (!Number::TryParse(valueText, value))
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' did not contain a valid integer value."));
    }
return value;}

std::string RuntimeManifestJsonReader::ReadRequiredObjectProperty(std::string json, std::string propertyName)
{
const int32_t valueStart = FindRequiredPropertyValueStart(json, propertyName);
const int32_t valueLength = ReadJsonValueLength(json, valueStart);
    if (valueLength <= 0 || json[valueStart] != '{')
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' did not contain a JSON object."));
    }
return String::Substring(json, valueStart, valueLength);}

Array<std::string>* RuntimeManifestJsonReader::ReadRequiredStringArrayProperty(std::string json, std::string propertyName)
{
const std::string arrayJson = ReadRequiredArrayProperty(json, propertyName);
List<std::string> *values = new List<std::string>();
int32_t elementStart = 0;
int32_t elementLength = 0;
int32_t cursor = 1;
while (TryReadNextArrayElement(arrayJson, cursor, elementStart, elementLength)) {
int32_t consumedLength = 0;
const std::string value = ReadJsonStringValue(arrayJson, elementStart, consumedLength);
    if (consumedLength != elementLength)
    {
throw new InvalidOperationException(std::string("Property '") + propertyName + std::string("' contained an invalid string array value."));
    }
values->Add(value);
}
return values->ToArray();}

std::string RuntimeManifestJsonReader::ReadRequiredStringProperty(std::string json, std::string propertyName)
{
const int32_t valueStart = FindRequiredPropertyValueStart(json, propertyName);
int32_t valueLength = 0;
const std::string value = ReadJsonStringValue(json, valueStart, valueLength);
return value;}

Array<::RuntimeCodeModuleManifestEntry*>* RuntimeManifestJsonReader::ReadRuntimeCodeModuleEntries(std::string json)
{
const std::string entriesJson = ReadRequiredArrayProperty(json, "Entries");
List<::RuntimeCodeModuleManifestEntry*> *entries = new List<::RuntimeCodeModuleManifestEntry*>();
int32_t elementStart = 0;
int32_t elementLength = 0;
int32_t cursor = 1;
while (TryReadNextArrayElement(entriesJson, cursor, elementStart, elementLength)) {
const std::string entryJson = String::Substring(entriesJson, elementStart, elementLength);
entries->Add(ReadRuntimeCodeModuleManifestEntry(entryJson));
}
return entries->ToArray();}

::RuntimeCodeModuleManifestEntry* RuntimeManifestJsonReader::ReadRuntimeCodeModuleManifestEntry(std::string json)
{
const std::string moduleId = ReadRequiredStringProperty(json, "ModuleId");
const std::string runtimeSpecializationId = ReadRequiredStringProperty(json, "RuntimeSpecializationId");
::RuntimeCodeModuleLoadState loadState = static_cast<RuntimeCodeModuleLoadState>(ReadRequiredIntegerProperty(json, "LoadState"));
Array<std::string> *dependencyModuleIds = ReadRequiredStringArrayProperty(json, "DependencyModuleIds");
return new ::RuntimeCodeModuleManifestEntry(moduleId, runtimeSpecializationId, loadState, dependencyModuleIds);}

int32_t RuntimeManifestJsonReader::SkipWhitespace(std::string json, int32_t startIndex)
{
int32_t cursor = startIndex;
while (cursor < static_cast<int32_t>(json.size()) && IsJsonWhitespace(json[cursor])) {
cursor++;
}
return cursor;}

bool RuntimeManifestJsonReader::TryReadNextArrayElement(std::string json, int32_t& cursor, int32_t& valueStart, int32_t& valueLength)
{
cursor = SkipWhitespace(json, cursor);
while (cursor < static_cast<int32_t>(json.size()) && json[cursor] == ',') {
cursor++;
cursor = SkipWhitespace(json, cursor);
}
    if (cursor >= static_cast<int32_t>(json.size()) || json[cursor] == ']')
    {
valueStart = 0;
valueLength = 0;
return false;    }
valueStart = cursor;
valueLength = ReadJsonValueLength(json, cursor);
cursor += valueLength;
return true;}

