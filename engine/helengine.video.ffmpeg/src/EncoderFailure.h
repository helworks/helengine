#pragma once
#include <windows.h>
#include <stdexcept>
#include <string>
// Keeps the original HRESULT or FFmpeg code through the C boundary.
class EncoderFailure : public std::runtime_error {
public:
    int Code;
    EncoderFailure(int code,const std::string& message) : std::runtime_error(message),Code(code) {}
    static void Check(int result,const char* operation);
    static void CheckHr(HRESULT result,const char* operation);
    static void Require(bool condition,const char* message);
    static std::string Utf8(const wchar_t* path);
};
