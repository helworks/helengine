#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderConditionalFrame
{
public:
    bool BranchMatched;

    bool get_BranchMatched();
    void set_BranchMatched(bool value);

    bool CurrentIncluded;

    bool get_CurrentIncluded();
    void set_CurrentIncluded(bool value);

    bool HasElseBranch;

    bool get_HasElseBranch();
    void set_HasElseBranch(bool value);

    bool ParentIncluded;

    bool get_ParentIncluded();

    ShaderConditionalFrame(bool parentIncluded, bool branchMatched, bool currentIncluded);
};
