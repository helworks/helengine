// Compile against the generated unity unit; never patch generated source.
#include "UiBinaryCodec.hpp"
#include "UiDocument.hpp"
#include "UiTemplate.hpp"
#include "UiNode.hpp"
#include "UiComponent.hpp"
#include "system/io/binary-reader.hpp"
#include "system/io/binary-writer.hpp"
#include "system/io/memory-stream.hpp"
#include <fstream>
#include <iostream>
#include <iterator>
#include <memory>
#include <vector>
#if defined(_MSC_VER) && defined(_DEBUG)
#include <crtdbg.h>
#endif

/// Releases the native subtree before deleting its root allocation.
struct DocumentDeleter {
    void operator()(UiDocument* document) const {
        if (document) { document->Dispose(); delete document; }
    }
};
using OwnedDocument = std::unique_ptr<UiDocument, DocumentDeleter>;

/// Exercises successful reads, exact bytes, replacement and repeated disposal.
void RoundTrip(Array<uint8_t>& bytes) {
    OwnedDocument document(UiBinaryCodec::Read(&bytes));
    std::unique_ptr<Array<uint8_t>> output(UiBinaryCodec::Write(document.get()));
    if (output->Length != bytes.Length) { throw std::runtime_error("Wire length changed."); }
    for (int32_t i = 0; i < bytes.Length; ++i) {
        if ((*output)[i] != bytes[i]) { throw std::runtime_error("Wire bytes changed."); }
    }
    document->set_Templates(document->get_Templates());
    document->set_Templates(new Array<UiTemplate*>(2));
    (*document->get_Templates())[1] = new UiTemplate();
    (*document->get_Templates())[1]->set_Nodes(new Array<UiNode*>(2));
    (*(*document->get_Templates())[1]->get_Nodes())[1] = new UiNode();
    document->Dispose(); document->Dispose();
}

#if HE_CPP_USE_EXCEPTIONS
/// Keeps the payload header consistent so truncated prefixes reach partially built owners.
void RejectPrefixes(Array<uint8_t>& bytes) {
    for (int32_t length = 20; length < bytes.Length; ++length) {
        Array<uint8_t> cut(length);
        for (int32_t i = 0; i < length; ++i) { cut[i] = bytes[i]; }
        uint32_t payload = static_cast<uint32_t>(length - 16);
        for (int32_t i = 0; i < 4; ++i) { cut[12 + i] = static_cast<uint8_t>(payload >> (8 * i)); }
        bool rejected = false;
        try { OwnedDocument unexpected(UiBinaryCodec::Read(&cut)); }
        catch (FormatException* error) { delete error; rejected = true; }
        catch (const FormatException&) { rejected = true; }
        if (!rejected) { throw std::runtime_error("Truncated document accepted."); }
    }
    // Semantic failure after allocating the entire document must also release it.
    Array<uint8_t> trailing(bytes.Length + 1);
    for (int32_t i = 0; i < bytes.Length; ++i) { trailing[i] = bytes[i]; }
    uint32_t payload = static_cast<uint32_t>(trailing.Length - 16);
    for (int32_t i = 0; i < 4; ++i) { trailing[12 + i] = static_cast<uint8_t>(payload >> (8 * i)); }
    bool rejected = false;
    try { OwnedDocument unexpected(UiBinaryCodec::Read(&trailing)); }
    catch (FormatException* error) { delete error; rejected = true; }
        catch (const FormatException&) { rejected = true; }
    if (!rejected) { throw std::runtime_error("Trailing payload accepted."); }
}
#endif

/// Measures live CRT allocations around repeated independent fixture operations after warm-up.
void CheckFixture(Array<uint8_t>& bytes) {
    RoundTrip(bytes);
#if HE_CPP_USE_EXCEPTIONS
    RejectPrefixes(bytes);
#endif
#if defined(_MSC_VER) && defined(_DEBUG)
    _CrtMemState before{}, after{}, delta{}; _CrtMemCheckpoint(&before);
#endif
    for (int iteration = 0; iteration < 20; ++iteration) {
        RoundTrip(bytes);
#if HE_CPP_USE_EXCEPTIONS
        RejectPrefixes(bytes);
#endif
    }
#if defined(_MSC_VER) && defined(_DEBUG)
    _CrtMemCheckpoint(&after);
    if (_CrtMemDifference(&delta, &before, &after) &&
        (delta.lCounts[_NORMAL_BLOCK] != 0 || delta.lSizes[_NORMAL_BLOCK] != 0)) {
        std::cerr << "Live allocation delta: " << delta.lCounts[_NORMAL_BLOCK] << " blocks, " << delta.lSizes[_NORMAL_BLOCK] << " bytes.\n";
        throw std::runtime_error("Native ownership leaked allocations.");
    }
#endif
}

/// Reads fixture paths independently of the generated codec and verifies native behavior.
int main(int argc, char** argv) {
    try {
        if (argc < 2) { throw std::runtime_error("Expected binary fixture paths."); }
        for (int index = 1; index < argc; ++index) {
            std::ifstream input(argv[index], std::ios::binary);
            if (!input) { throw std::runtime_error("Cannot open fixture."); }
            std::vector<uint8_t> expected((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
            Array<uint8_t> bytes(static_cast<int32_t>(expected.size()));
            for (int32_t i = 0; i < bytes.Length; ++i) { bytes[i] = expected[static_cast<size_t>(i)]; }
            CheckFixture(bytes);
            std::cout << "PASS: C++ bytes and ownership: " << bytes.Length << " bytes; exceptions=" << HE_CPP_USE_EXCEPTIONS << ".\n";
        }
        MemoryStream stream; BinaryWriter writer(&stream);
        writer.WriteByte(72); writer.WriteByte(69); stream.SetPosition(0);
        BinaryReader reader(&stream); std::unique_ptr<Array<uint8_t>> shortRead(reader.ReadBytes(int32_t(4)));
        if (shortRead->Length != 2 || (*shortRead)[0] != 72 || (*shortRead)[1] != 69) { throw std::runtime_error("ReadBytes EOF contract failed."); }
        return 0;
    } catch (Exception* error) { std::cerr << "FAIL: " << error->what() << "\n"; delete error; return 1; }
    catch (const std::exception& error) { std::cerr << "FAIL: " << error.what() << "\n"; return 1; }
    catch (...) { std::cerr << "FAIL: native binary conformance.\n"; return 1; }
}
