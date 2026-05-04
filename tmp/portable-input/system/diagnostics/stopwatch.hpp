#pragma once

#include "runtime/native_datetime.hpp"

namespace System {
namespace Diagnostics {

/// <summary>
/// Provides lightweight managed-style stopwatch timing for player runtime diagnostics.
/// </summary>
class Stopwatch {
public:
    /// <summary>
    /// Initializes a new stopwatch in the stopped state.
    /// </summary>
    Stopwatch()
        : Elapsed(), IsRunningValue(false), StartTime(), TotalElapsedMilliseconds(0.0) {
    }

    /// <summary>
    /// Creates and starts one stopwatch instance in a single call.
    /// </summary>
    /// <returns>Started stopwatch instance.</returns>
    static Stopwatch* StartNew() {
        Stopwatch* stopwatch = new Stopwatch();
        stopwatch->Start();
        return stopwatch;
    }

    /// <summary>
    /// Gets a value indicating whether the stopwatch is currently running.
    /// </summary>
    /// <returns>True when the stopwatch has been started and not stopped yet.</returns>
    bool get_IsRunning() const {
        return IsRunningValue;
    }

    /// <summary>
    /// Starts or resumes the stopwatch.
    /// </summary>
    void Start() {
        if (!IsRunningValue) {
            StartTime = DateTime::Now();
            IsRunningValue = true;
        }
    }

    /// <summary>
    /// Restarts the stopwatch from zero elapsed time.
    /// </summary>
    void Restart() {
        TotalElapsedMilliseconds = 0.0;
        StartTime = DateTime::Now();
        IsRunningValue = true;
        Elapsed = TimeSpan(0.0);
    }

    /// <summary>
    /// Stops the stopwatch and freezes the current elapsed time.
    /// </summary>
    void Stop() {
        if (IsRunningValue) {
            TotalElapsedMilliseconds += (DateTime::Now() - StartTime).TotalMilliseconds;
            Elapsed = TimeSpan(TotalElapsedMilliseconds);
            IsRunningValue = false;
        }
    }

    /// <summary>
    /// Gets the accumulated elapsed time.
    /// </summary>
    /// <returns>Elapsed time as a managed-style duration value.</returns>
    TimeSpan get_Elapsed() {
        if (IsRunningValue) {
            return TimeSpan(TotalElapsedMilliseconds + (DateTime::Now() - StartTime).TotalMilliseconds);
        }

        return Elapsed;
    }

    /// <summary>
    /// Gets the accumulated elapsed time in a field shape that matches the generated C++ property lowering.
    /// </summary>
    TimeSpan Elapsed;

private:
    /// <summary>
    /// Tracks whether the stopwatch is currently running.
    /// </summary>
    bool IsRunningValue;

    /// <summary>
    /// Captures the instant at which the current running interval started.
    /// </summary>
    DateTime StartTime;

    /// <summary>
    /// Accumulates elapsed time across stopped and running intervals.
    /// </summary>
    double TotalElapsedMilliseconds;
};

}
}

using Stopwatch = System::Diagnostics::Stopwatch;
