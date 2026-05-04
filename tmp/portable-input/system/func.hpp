#pragma once

template <typename TArg, typename TResult>
class Func {
public:
    using FuncType = TResult(*)(TArg);

    Func()
        : func(nullptr) {
    }

    explicit Func(FuncType value)
        : func(value) {
    }

    TResult operator()(TArg arg) const {
        return func(arg);
    }

private:
    FuncType func;
};
