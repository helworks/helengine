#include "NativeTestUtils.hpp"
#include "CaptureSession.hpp"
#include "CaptureBorderPolicy.hpp"
#include <future>
#include <chrono>

/// <summary>Verifies native error isolation, terminal state, callback fencing and optional border policy.</summary>
class SessionContractTests {
public:
    /// <summary>Returns a versioned error buffer owned by the current test invocation.</summary>
    static he_capture_error Error() { return { sizeof(he_capture_error), 1 }; }
    /// <summary>Returns the approved defaults without opening a graphics capture session.</summary>
    static he_capture_options Options() { return { sizeof(he_capture_options), 1, 3, 0, 0 }; }
    /// <summary>Invalid windows never create handles or masquerade as successful empty sessions.</summary>
    static void OpenRejectsInvalidWindow() {
        auto error = Error(); auto options = Options(); void* session = reinterpret_cast<void*>(1);
        auto result = he_capture_open(reinterpret_cast<void*>(-1), nullptr, &options, &session, &error);
        NativeTestUtils::Require(result == -1 && !session && error.HResult < 0 && std::wstring(error.Operation) == L"open",
            "Invalid window diagnostics or output handle are incorrect.");
    }
    /// <summary>Version and option validation happens before attempting to initialize capture.</summary>
    static void RejectsAbiVersionAndFlags() {
        auto error = Error(); auto options = Options(); void* session = nullptr;
        options.Version = 7;
        NativeTestUtils::Require(he_capture_open(nullptr, nullptr, &options, &session, &error) == -1 && error.HResult == E_INVALIDARG,
            "Unknown ABI version was accepted.");
        options = Options(); options.CaptureCursor = 2;
        NativeTestUtils::Require(he_capture_open(nullptr, nullptr, &options, &session, &error) == -1 && error.HResult == E_INVALIDARG,
            "Unknown cursor flag was accepted.");
    }
    /// <summary>Separate calls cannot overwrite each other's operation diagnostics.</summary>
    static void ErrorsRemainPerOperation() {
        auto first = Error(); auto second = Error(); uint32_t state = 0;
        auto pending = std::async(std::launch::async, [&first]() { void* frame = nullptr; he_capture_frame_info info { sizeof(info), 1 };
            he_capture_try_acquire(nullptr, &frame, &info, &first); });
        he_capture_get_state(nullptr, &state, &second); pending.get();
        NativeTestUtils::Require(std::wstring(first.Operation) == L"try_acquire" && std::wstring(second.Operation) == L"get_state",
            "Concurrent operations shared their error buffers.");
    }
    /// <summary>A callback failure terminates delivery without invalidating a previously transferred frame.</summary>
    static void TerminalFailureClearsPendingWithoutInvalidatingLease() {
        auto device = NativeTestUtils::Device(); auto texture = NativeTestUtils::Texture(device.Get());
        CaptureSession session(CaptureGpuContext::Create(device.Get()), Options());
        session.Shared->PublishTexture(texture.Get(), 5, 3, 1); std::unique_ptr<CaptureFrame> held;
        NativeTestUtils::Require(session.TryAcquire(held) == 1, "Synthetic frame was not delivered.");
        session.Shared->PublishTexture(texture.Get(), 5, 3, 2);
        session.Fail(DXGI_ERROR_DEVICE_REMOVED, L"capture", L"Injected device loss");
        NativeTestUtils::Require(session.State() == he_capture_state::Faulted && session.Shared->Queue.Snapshot().ShutdownDiscardedFrames == 1,
            "Terminal failure did not clean pending frames.");
        std::vector<uint8_t> bytes(60); held->CopyPixels(bytes.data(), bytes.size()); NativeTestUtils::Pattern(bytes);
        bool rejected = false; try { std::unique_ptr<CaptureFrame> next; session.TryAcquire(next); } catch (...) { rejected = true; }
        NativeTestUtils::Require(rejected, "Terminal callback error was hidden as an empty poll.");
    }
    /// <summary>Stopping waits for the publication gate and rejects callbacks after the terminal transition.</summary>
    static void CloseWaitsForInFlightPublication() {
        auto device = NativeTestUtils::Device(); auto texture = NativeTestUtils::Texture(device.Get());
        CaptureSession session(CaptureGpuContext::Create(device.Get()), Options());
        std::unique_lock<std::mutex> gate(session.Shared->Gate); std::promise<void> entered;
        auto closed = std::async(std::launch::async, [&]() { entered.set_value(); session.Stop(); });
        entered.get_future().get();
        NativeTestUtils::Require(closed.wait_for(std::chrono::milliseconds(20)) == std::future_status::timeout,
            "Shutdown did not fence active publication.");
        gate.unlock(); closed.get(); session.Shared->PublishTexture(texture.Get(), 5, 3, 1);
        NativeTestUtils::Require(session.Shared->Queue.Snapshot().ObservedFrames == 0, "A frame was published after stop.");
    }
    /// <summary>Final accounting is obtained after pending frames have actually been released.</summary>
    static void StopReturnsPostCleanupStatistics() {
        auto device = NativeTestUtils::Device(); auto texture = NativeTestUtils::Texture(device.Get());
        CaptureSession session(CaptureGpuContext::Create(device.Get()), Options());
        session.Shared->PublishTexture(texture.Get(), 5, 3, 1); session.Shared->PublishTexture(texture.Get(), 5, 3, 2);
        session.Stop(); session.Stop();
        NativeTestUtils::Require(session.Statistics().ShutdownDiscardedFrames == 2, "Final shutdown snapshot missed discarded frames.");
    }
    /// <summary>Border denial and absence are explicit only when the caller requested border removal.</summary>
    static void BorderPolicyRejectsDeniedStatus() {
        using Status = winrt::Windows::Security::Authorization::AppCapabilityAccess::AppCapabilityAccessStatus;
        for (auto status : { Status::DeniedByUser, Status::DeniedBySystem }) {
            bool rejected = false; try { CaptureBorderPolicy::RequireAllowed(status); } catch (...) { rejected = true; }
            NativeTestUtils::Require(rejected, "Border authorization denial was ignored.");
        }
        CaptureBorderPolicy::RequireAllowed(Status::Allowed); CaptureBorderPolicy::RequireProperty(false, false);
        bool rejected = false; try { CaptureBorderPolicy::RequireProperty(true, false); } catch (...) { rejected = true; }
        NativeTestUtils::Require(rejected, "Unsupported explicit border removal was ignored.");
    }
};
/// <summary>Runs the native session contract group, without capturing application windows.</summary>
void RunSessionContractTests() {
    SessionContractTests::OpenRejectsInvalidWindow(); std::cout << "PASS OpenRejectsInvalidWindow\n";
    SessionContractTests::RejectsAbiVersionAndFlags(); std::cout << "PASS RejectsAbiVersionAndFlags\n";
    SessionContractTests::ErrorsRemainPerOperation(); std::cout << "PASS ErrorsRemainPerOperation\n";
    SessionContractTests::TerminalFailureClearsPendingWithoutInvalidatingLease(); std::cout << "PASS TerminalFailureClearsPendingWithoutInvalidatingLease\n";
    SessionContractTests::CloseWaitsForInFlightPublication(); std::cout << "PASS CloseWaitsForInFlightPublication\n";
    SessionContractTests::StopReturnsPostCleanupStatistics(); std::cout << "PASS StopReturnsPostCleanupStatistics\n";
    SessionContractTests::BorderPolicyRejectsDeniedStatus(); std::cout << "PASS BorderPolicyRejectsDeniedStatus\n";
}
