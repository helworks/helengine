#pragma once
#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif
/** Borrowed UTF-8 identity plus fixed-width legacy brush data. No pointer is retained. */
typedef struct HPBrush {
    uint32_t version;
    uint32_t eraser;
    const char* id;
    double diameter;
    double red;
    double green;
    double blue;
    double opacity;
} HPBrush;

/** Fixed binary64 authored sample. Reserved must be zero; index is local to its stroke. */
typedef struct HPSample {
    int32_t index;
    uint32_t reserved;
    double x, y, pressure, time, altitude, azimuth;
} HPSample;
/** Explicit document/pixel dimensions and admission ceilings. */
typedef struct HPCanvas {
    double width, height;
    int32_t pixel_width, pixel_height, max_samples, max_stamps, max_strokes;
    uint32_t reserved;
} HPCanvas;
/** Borrowed stroke input copied by snapshot creation. */
typedef struct HPStrokeInput {
    const char* id;
    HPBrush brush;
    const HPSample* samples;
    int32_t count;
    uint32_t reserved;
} HPStrokeInput;
/** Document-space geometry and straight input RGBA for portable consumers. */
typedef struct HPStamp { double x, y, radius, red, green, blue, opacity; } HPStamp;
/** Opaque independently owned session, snapshot and sampler lifetimes. */
typedef struct HPSession HPSession;
typedef struct HPSnapshot HPSnapshot;
typedef struct HPSampler HPSampler;

/** Creates a session borrowing an IPaintSurfaceBackend native interface; provider outlives the session. */
int32_t hp_session_create(void* borrowed_backend,const HPCanvas* canvas,int32_t history_limit,HPSession** result);
/** Reallocates/replays an idle surface at a local origin, preserving CPU history and authored sample coordinates. */
int32_t hp_session_reconfigure(HPSession* session,const HPCanvas* canvas,double origin_x,double origin_y);
/** Releases idle GPU resources while retaining CPU content and history; inactive navigation enqueues no raster work. */
int32_t hp_session_suspend(HPSession* session);
/** Destroys one owned session; null is accepted, other handles must be live and owned. */
void hp_session_destroy(HPSession* session);
/** Starts a stroke with copied identity and brush settings. */
int32_t hp_session_begin(HPSession* session,const char* id,const HPBrush* brush);
/** Copies a complete consecutive input batch before forwarding any raster marks. */
int32_t hp_session_append(HPSession* session,const HPSample* samples,int32_t count);
/** Applies pressure/orientation estimates by existing sample index. */
int32_t hp_session_correct(HPSession* session,const HPSample* samples,int32_t count);
/** Commits accepted input. Status 4 means CPU commit succeeded (token is valid) but rendering failed. */
int32_t hp_session_commit(HPSession* session,int64_t* token);
/** Aborts active input. A rendering failure does not restore discarded input. */
int32_t hp_session_cancel(HPSession* session);
/** Moves to preceding content; status 4 means CPU history moved but rendering failed. */
int32_t hp_session_undo(HPSession* session,int64_t token);
/** Reapplies content; status 4 means CPU history moved but rendering failed. */
int32_t hp_session_redo(HPSession* session,int64_t token);
/** Drops only undone records when host metadata creates a new branch. */
int32_t hp_session_discard_redo(HPSession* session);
/** Evicts an applied transition's undo eligibility without deleting artwork. */
int32_t hp_session_forget(HPSession* session,int64_t token);
/** Returns revision and flags: active=1, undo=2, redo=4, render-failed=8. */
int32_t hp_session_state(HPSession* session,int64_t* revision,uint32_t* flags);
/** Captures an independently owned immutable CPU snapshot. */
int32_t hp_session_snapshot(HPSession* session,HPSnapshot** result);
/** Validates/copies a whole snapshot before replacing the session base. */
int32_t hp_session_load(HPSession* session,const HPSnapshot* snapshot);
/** Copies portable records; no input pointer is retained. */
int32_t hp_snapshot_create(const HPStrokeInput* strokes,int32_t count,HPSnapshot** result);
/** Releases one immutable CPU snapshot. */
void hp_snapshot_destroy(HPSnapshot* snapshot);
/** Returns committed record count and producing session revision. */
int32_t hp_snapshot_info(const HPSnapshot* snapshot,int32_t* count,int64_t* revision);
/** Reads record metadata. Brush id is a static v1 literal; required_id includes its trailing zero. */
int32_t hp_snapshot_stroke(const HPSnapshot* snapshot,int32_t index,HPBrush* brush,int32_t* count,char* id,size_t capacity,size_t* required_id);
/** Copies one complete record's samples into caller storage of at least its reported count. */
int32_t hp_snapshot_samples(const HPSnapshot* snapshot,int32_t index,HPSample* samples,int32_t capacity);
/** Creates the same generated sampler used by painting, for thumbnails and other CPU consumers. */
int32_t hp_sampler_create(const HPBrush* brush,HPSampler** result);
/** Releases a sampler and its last borrowed output batch. */
void hp_sampler_destroy(HPSampler* sampler);
/** Returns borrowed marks valid until the next append/destroy on this sampler. */
int32_t hp_sampler_append(HPSampler* sampler,const HPSample* sample,const HPStamp** marks,int32_t* count);

/** Version of this C ABI, independent of authored brush behavior versions. */
uint32_t hp_abi_version(void);
/** Returns 0 valid, 1 invalid data, 2 unsupported behavior, or 3 native failure. */
int32_t hp_validate_brush(const HPBrush* brush);
/** Copies the last error on this thread, including a trailing zero when capacity permits. Returns required bytes. */
size_t hp_last_error(char* destination, size_t capacity);
#ifdef __cplusplus
}
#endif
