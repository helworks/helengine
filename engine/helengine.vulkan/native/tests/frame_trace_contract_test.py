"""Verify the real C++ recorder's public JSON and interval contract without a GPU."""
import json
import subprocess
import sys

result = subprocess.run([sys.argv[1], "enabled"], check=True, capture_output=True, text=True)
frames = [json.loads(line.removeprefix("HELENGINE_FRAME_TRACE ")) for line in result.stdout.splitlines() if line.startswith("HELENGINE_FRAME_TRACE ")]
assert len(frames) == 2
for frame in frames:
    assert set(frame) == {"frame", "unit", "thread", "spans", "metrics", "cpuSamples", "originEpochUs", "cpuSampleResolutionNs", "spanCapacity", "complete", "sources", "subjects", "batches"}
    assert frame["unit"] == "ns" and frame["thread"] == "player"
    spans = frame["spans"]
    assert spans[0]["id"] == 0 and spans[0]["parent"] is None and spans[0]["name"] == "Frame" and spans[0]["start"] == 0
    children = {}
    for index, span in enumerate(spans):
        assert set(span) - {"counters", "sourceId", "subjectId", "batchId", "phase"} == {"id", "parent", "name", "start", "end"}
        assert span["id"] == index
        assert type(span["start"]) is int and type(span["end"]) is int
        assert 0 <= span["start"] <= span["end"] <= 9007199254740991
        parent = span["parent"]
        if parent is not None:
            assert 0 <= parent < index
            assert spans[parent]["start"] <= span["start"] <= span["end"] <= spans[parent]["end"]
            siblings = children.setdefault(parent, [])
            if siblings:
                assert siblings[-1]["end"] <= span["start"]
            siblings.append(span)
    assert set(frame["metrics"]) == {"drawCalls", "meshSamples", "rootHits", "rootMisses", "width", "height", "ready"}
    assert type(frame["metrics"]["ready"]) is bool
assert frames[0]["metrics"] == {"drawCalls":23, "meshSamples":19, "rootHits":17, "rootMisses":3, "width":1920, "height":1080, "ready":True}
assert [span["name"] for span in frames[0]["spans"]] == ["Frame", "Core.Update", "Update.Child", "Update.OtherTranslationUnit", "Core.Draw"]
assert frames[0]["spans"][3]["parent"] == 1
assert frames[0]["spans"][2]["counters"] == {"items": 3, "bytes": 224}
assert "counters" not in frames[0]["spans"][0]
assert "counters" not in frames[0]["spans"][1]
assert frames[0]["spans"][4]["counters"] == {"items": 0}
sample = frames[0]["cpuSamples"][0]
assert sample["name"] == 'Type"quoted\\name'
assert sample["start"] == sample["end"]
assert -999 <= sample["start"] <= frames[0]["spans"][0]["end"]
assert sample["typeHash"] == 123 and sample["registrationId"] == 1 and sample["entityId"] == 2 and sample["updateIndex"] == 0
assert frames[0]["cpuSampleResolutionNs"] == 1000 and frames[0]["spanCapacity"] == 262144 and frames[0]["complete"] is True
assert frames[1]["cpuSamples"] == []
result = subprocess.run([sys.argv[1], "disabled"], check=True, capture_output=True, text=True)
assert "HELENGINE_FRAME_TRACE" not in result.stdout
print("PASS frame trace JSON schema nested bounds sibling ordering shared archive disabled no export")

first = frames[0]
assert first["subjects"] == [
    {"id":0,"objectId":0,"entityId":42,"modelId":'model"quoted',"materialId":'material\\path',"meshId":8,"submeshIndex":0,"indexCount":6},
    {"id":1,"objectId":0,"entityId":42,"modelId":'model"quoted',"materialId":'material\\path',"meshId":8,"submeshIndex":1,"indexCount":3}]
assert first["batches"] == [{"id":0,"subjectIds":[0,1]}]
for span in first["spans"][1:4]:
    assert span["subjectId"] == 0 and span["phase"] == "preparation"
    assert "batchId" not in span
shared = first["spans"][4]
assert shared["batchId"] == 0 and shared["phase"] == "emission" and "subjectId" not in shared
for span in first["spans"][1:]:
    source = first["sources"][span["sourceId"]]
    assert source["line"] > 0 and source["file"].endswith(".cpp")
    assert "VulkanFrameTraceScope" not in source["function"]
assert len({(s["file"],s["function"],s["line"]) for s in first["sources"]}) == len(first["sources"])
assert frames[1]["subjects"] == frames[1]["batches"] == frames[1]["sources"] == []
print("PASS source locations escaped identities inherited subjects shared membership frame reset")
