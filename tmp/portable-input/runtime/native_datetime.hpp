#pragma once

#include <chrono>
#include <cstdint>

/// <summary>
/// Represents a lightweight managed-style duration expressed in milliseconds.
/// </summary>
class TimeSpan {
public:
    double TotalMilliseconds;

    TimeSpan()
        : TotalMilliseconds(0.0) {
    }

    explicit TimeSpan(double totalMilliseconds)
        : TotalMilliseconds(totalMilliseconds) {
    }
};

/// <summary>
/// Represents a lightweight managed-style point in time expressed as Unix milliseconds.
/// </summary>
class DateTime {
public:
    int64_t UnixMilliseconds;

    DateTime()
        : UnixMilliseconds(0) {
    }

    explicit DateTime(int64_t unixMilliseconds)
        : UnixMilliseconds(unixMilliseconds) {
    }

    static DateTime Now() {
        return DateTime(CurrentUnixMilliseconds());
    }

    static DateTime UtcNow() {
        return DateTime(CurrentUnixMilliseconds());
    }

private:
    static int64_t CurrentUnixMilliseconds() {
        using namespace std::chrono;
        return duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
    }
};

inline TimeSpan operator-(const DateTime& left, const DateTime& right) {
    return TimeSpan(static_cast<double>(left.UnixMilliseconds - right.UnixMilliseconds));
}
