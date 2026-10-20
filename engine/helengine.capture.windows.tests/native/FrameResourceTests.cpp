#include "NativeTestUtils.hpp"
#include "CaptureFrameQueue.hpp"
#include <cstddef>

/// <summary>Exercises real GPU copies, bounded leases, and frame lifetimes without capturing windows.</summary>
class FrameResourceTests {
public:
    /// <summary>Checks exact timestamp propagation and region-only BGRA readback.</summary>
    static void PreservesTimestampAndCopiesOnlyContent() {
        auto device = NativeTestUtils::Device(); auto source = NativeTestUtils::Texture(device.Get());
        CaptureFrameQueue queue(3, CaptureGpuContext::Create(device.Get()));
        NativeTestUtils::Require(queue.TryPush(source.Get(), 5, 3, 123456789), "Expected a source frame.");
        auto frame = queue.TryAcquire(); auto info = frame->Info();
        NativeTestUtils::Require(info.Width == 5 && info.Height == 3 && info.TimestampTicks == 123456789,
            "Dimensions or source timestamp were modified.");
        std::vector<uint8_t> bytes(5 * 3 * 4); frame->CopyPixels(bytes.data(), bytes.size()); NativeTestUtils::Pattern(bytes);
        NativeTestUtils::Require(!queue.TryAcquire(), "A frame was delivered twice.");
    }
    /// <summary>Includes both pending and consumer-held frames in the same hard capacity.</summary>
    static void BudgetCountsQueuedAndLeasedFrames() {
        auto device = NativeTestUtils::Device(); auto source = NativeTestUtils::Texture(device.Get());
        CaptureFrameQueue queue(3, CaptureGpuContext::Create(device.Get()));
        NativeTestUtils::Require(queue.TryPush(source.Get(), 5, 3, 1), "First push failed."); auto first = queue.TryAcquire();
        NativeTestUtils::Require(queue.TryPush(source.Get(), 5, 3, 2), "Second push failed.");
        NativeTestUtils::Require(queue.TryPush(source.Get(), 5, 3, 3), "Third push failed.");
        NativeTestUtils::Require(!queue.TryPush(source.Get(), 5, 3, 4), "Capacity exceeded three frames.");
        NativeTestUtils::Require(queue.Snapshot().OverflowDiscardedFrames == 1, "Overflow was hidden.");
        first.reset(); NativeTestUtils::Require(queue.TryPush(source.Get(), 5, 3, 5), "Released capacity was not returned.");
        auto second = queue.TryAcquire(); NativeTestUtils::Require(second->Info().SequenceNumber == 2, "FIFO order changed.");
    }
    /// <summary>A leased texture remains readable after size changes and destruction of its producing queue.</summary>
    static void HeldFramesSurviveQueueDestructionAndResize() {
        auto device = NativeTestUtils::Device(); auto source = NativeTestUtils::Texture(device.Get());
        auto queue = std::make_unique<CaptureFrameQueue>(3, CaptureGpuContext::Create(device.Get()));
        queue->TryPush(source.Get(), 5, 3, 1); auto held = queue->TryAcquire();
        queue->TryPush(source.Get(), 2, 2, 2); auto smallerFrame = queue->TryAcquire(); smallerFrame.reset();
        queue->TryPush(source.Get(), 7, 5, 3); queue.reset(); device.Reset(); source.Reset();
        auto info = held->Info(); NativeTestUtils::Require(info.Width == 5 && info.Height == 3, "Held dimensions changed.");
        std::vector<uint8_t> bytes(60); held->CopyPixels(bytes.data(), bytes.size()); NativeTestUtils::Pattern(bytes);
    }
    /// <summary>Rejects invalid layouts, size regions, and destination buffers before writing.</summary>
    static void RejectsInvalidSizesAndAbiLayouts() {
        static_assert(sizeof(he_capture_options) == 20 && sizeof(he_capture_error) == 1164, "Options/error ABI mismatch.");
        static_assert(sizeof(he_capture_frame_info) == 56 && sizeof(he_capture_statistics) == 48, "Frame/statistics ABI mismatch.");
        static_assert(offsetof(he_capture_frame_info, Texture) == 8 && offsetof(he_capture_frame_info, TimestampTicks) == 40,
            "Native x64 field offsets changed.");
        auto device = NativeTestUtils::Device(); auto context = CaptureGpuContext::Create(device.Get());
        bool rejected = false; try { CaptureFrameQueue invalid(0, context); } catch (const std::exception&) { rejected = true; }
        NativeTestUtils::Require(rejected, "Zero capacity must be rejected.");
        auto source = NativeTestUtils::Texture(device.Get()); CaptureFrameQueue queue(3, context);
        NativeTestUtils::Require(!queue.TryPush(source.Get(), 8, 5, 1), "Out-of-bounds source region accepted.");
        NativeTestUtils::Require(queue.Snapshot().ResizeDiscardedFrames == 1, "Invalid resize was not counted.");
        queue.TryPush(source.Get(), 5, 3, 2); auto frame = queue.TryAcquire(); std::vector<uint8_t> bytes(59, 0xEE);
        rejected = false; try { frame->CopyPixels(bytes.data(), bytes.size()); } catch (const std::exception&) { rejected = true; }
        NativeTestUtils::Require(rejected && bytes.front() == 0xEE && bytes.back() == 0xEE, "Insufficient destination was written.");
    }
};

/// <summary>Runs the native resource contract group and reports each completed assertion set.</summary>
void RunFrameResourceTests() {
    FrameResourceTests::PreservesTimestampAndCopiesOnlyContent(); std::cout << "PASS PreservesTimestampAndCopiesOnlyContent\n";
    FrameResourceTests::BudgetCountsQueuedAndLeasedFrames(); std::cout << "PASS BudgetCountsQueuedAndLeasedFrames\n";
    FrameResourceTests::HeldFramesSurviveQueueDestructionAndResize(); std::cout << "PASS HeldFramesSurviveQueueDestructionAndResize\n";
    FrameResourceTests::RejectsInvalidSizesAndAbiLayouts(); std::cout << "PASS RejectsInvalidSizesAndAbiLayouts\n";
}
