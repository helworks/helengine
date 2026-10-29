#pragma once
#include <span>
#include <vector>
#include <stdexcept>
namespace helengine::vulkan {
/// <summary>Borrows immutable ordered inputs while keeping profiler subjects owned by the current pass.</summary>
template<class TInput> class VulkanBatchInputView final {
public:
    /// <summary>Borrows a legacy contiguous input vector for the duration of planning.</summary>
    explicit VulkanBatchInputView(const std::vector<TInput>& inputs):Contiguous(inputs) { }
    /// <summary>Borrows stable retained input pointers and equally sized current-pass subject storage.</summary>
    VulkanBatchInputView(std::span<const TInput* const> inputs,std::span<const int> subjects):Borrowed(inputs),Subjects(subjects) {
        if(inputs.size()!=subjects.size()) throw std::invalid_argument("Batch input subjects must match input count.");
    }
    /// <summary>Reports the number of ordered original inputs.</summary>
    size_t size() const { return Borrowed.empty()?Contiguous.size():Borrowed.size(); }
    /// <summary>Borrows one immutable payload; owners must outlive the entire planning call.</summary>
    const TInput& operator[](size_t index) const { return Borrowed.empty()?Contiguous[index]:*Borrowed[index]; }
    /// <summary>Reads profiler ownership from the current pass rather than a retained frame.</summary>
    int Subject(size_t index) const { return Subjects.empty()?Contiguous[index].TraceSubject:Subjects[index]; }
private:
    /// <summary>Borrows legacy storage without copying its inputs.</summary>
    std::span<const TInput> Contiguous;
    /// <summary>Borrows pointers into independently retained immutable runs.</summary>
    std::span<const TInput* const> Borrowed;
    /// <summary>Borrows profiler subjects from the active pass.</summary>
    std::span<const int> Subjects;
};
}
