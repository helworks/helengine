#pragma once
#include <winrt/base.h>
#include <string>
#include <exception>
#include "helengine_capture_windows.h"

/// <summary>Retains a terminal HRESULT and its diagnostic independently of worker exception lifetimes.</summary>
class CaptureFailure {
public:
    /// <summary>Original native failure code.</summary>
    HRESULT Code = S_OK;
    /// <summary>Diagnostic containing the originating operation and failure.</summary>
    std::wstring Message;
    /// <summary>Captures an exception without retaining stack-local diagnostic pointers.</summary>
    static CaptureFailure Current() {
        CaptureFailure failure;
        try { throw; }
        catch (const winrt::hresult_error& error) { failure.Code = error.code(); failure.Message = error.message().c_str(); }
        catch (const std::bad_alloc&) { failure.Code = E_OUTOFMEMORY; failure.Message = L"Native capture allocation failed."; }
        catch (const std::invalid_argument& error) { failure.Code = E_INVALIDARG; failure.Message = winrt::to_hstring(error.what()).c_str(); }
        catch (const std::exception& error) { failure.Code = E_FAIL; failure.Message = winrt::to_hstring(error.what()).c_str(); }
        catch (...) { failure.Code = E_FAIL; failure.Message = L"Unknown native capture failure."; }
        return failure;
    }
    /// <summary>Restores a stored native failure at the acquisition boundary.</summary>
    void Throw() const { throw winrt::hresult_error(Code, Message); }
    /// <summary>Writes only into a valid caller-owned ABI error buffer and cannot throw across C.</summary>
    static int32_t Write(he_capture_error* error, const wchar_t* operation) noexcept {
        if (!error || error->Size != sizeof(he_capture_error) || error->Version != 1) { return -1; }
        error->HResult = E_FAIL;
        wcsncpy_s(error->Operation, operation, _TRUNCATE);
        try { auto failure = Current(); error->HResult = failure.Code; wcsncpy_s(error->Message, failure.Message.c_str(), _TRUNCATE); }
        catch (...) { error->HResult = E_OUTOFMEMORY; wcscpy_s(error->Message, L"Cannot allocate native capture error diagnostic."); }
        return -1;
    }
};
