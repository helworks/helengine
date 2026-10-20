#include <exception>
#include <iostream>
/// <summary>Executes the GPU resource contract group.</summary>
void RunFrameResourceTests();
/// <summary>Returns a failing process status for any native assertion or unhandled native error.</summary>
int main() {
    try { RunFrameResourceTests(); return 0; }
    catch (const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
}
