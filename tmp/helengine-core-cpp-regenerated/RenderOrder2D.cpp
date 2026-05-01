#ifdef DrawText
#undef DrawText
#endif
#include "RenderOrder2D.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

uint8_t RenderOrder2D::FloatingPanelBias = 32;

uint8_t RenderOrder2D::ModalBackground = 224;

uint8_t RenderOrder2D::ModalForeground = 240;

uint8_t RenderOrder2D::ModalInput = 248;

uint8_t RenderOrder2D::OverlayBackground = 160;

uint8_t RenderOrder2D::OverlayForeground = 176;

uint8_t RenderOrder2D::OverlayInput = 192;

uint8_t RenderOrder2D::PanelBackground = 16;

uint8_t RenderOrder2D::PanelForeground = 48;

uint8_t RenderOrder2D::PanelInteractive = 64;

uint8_t RenderOrder2D::PanelSurface = 32;

