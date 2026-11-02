#include "helgi/VoxelGi.h"
#include "../src/Math.h"
#include <cmath>
#include <iostream>
#include <stdexcept>
#include <utility>
using namespace helgi;

/// <summary>Throws a useful failure even when assertions are disabled in optimized builds.</summary>
void Require(bool condition,const char* message) {if(!condition) throw std::runtime_error(message);}
/// <summary>Completes staged work with a finite test guard.</summary>
void Complete(VoxelGi& gi) {
    for(unsigned int i=0;i<100000&&gi.GetStats().UpdatePending;++i) gi.AdvanceUpdate(4096);
    Require(!gi.GetStats().UpdatePending,"staged update failed to finish");
}
/// <summary>Returns two upward-facing triangles covering a horizontal reflector.</summary>
std::vector<Triangle> Floor(Vec3 color,float y) {
    return {{{0,y,0},{0,y,8},{8,y,0},color},{{8,y,0},{0,y,8},{8,y,8},color}};
}
/// <summary>Returns an opaque horizontal roof over a selected X interval.</summary>
std::vector<Triangle> Roof(float x0,float x1) {
    return {{{x0,5,0},{x1,5,0},{x0,5,8},{1,1,1}},{{x1,5,0},{x1,5,8},{x0,5,8},{1,1,1}}};
}
/// <summary>Sums RGB energy in an atlas to compare update behavior.</summary>
float Energy(const Field& field) {float result=0;for(std::size_t i=0;i<field.AtlasRgba.size();i+=4) result+=field.AtlasRgba[i]+field.AtlasRgba[i+1]+field.AtlasRgba[i+2];return result;}
/// <summary>Runs geometry, transport, staging, identity and numeric regression checks.</summary>
int main() {
    try {
        Config config{{0,0,0},{8,8,8},8,8,8,96};
        VoxelGi gi(config);
        Require(Energy(gi.GetField())==0,"initial field must be dark");
        Require(!gi.BeginUpdate(),"unchanged empty scene must not update");
        gi.SetSun({{0,1,0},{4,4,4}});
        gi.SetContributor(1,1,Floor({1,0.1f,0.02f},1));
        Require(gi.BeginUpdate(),"changed input must begin");
        Require(!gi.AdvanceUpdate(1)&&gi.GetField().Generation==0,"partial solve leaked its output");
        Complete(gi);
        float open=Energy(gi.GetField());Require(open>1,"sunlit reflector must bounce light");
        Vec3 sample=gi.SampleIrradiance({4.5f,3.5f,4.5f},{0,-1,0});
        Require(sample.X>sample.Y*5&&sample.Y>sample.Z*2,"bounce must preserve diffuse surface color");
        Vec3 secondCamera=gi.SampleIrradiance({4.5f,3.5f,4.5f},{0,-1,0});
        Require(sample.X==secondCamera.X&&sample.Y==secondCamera.Y&&sample.Z==secondCamera.Z,"identical world samples must be camera independent");
        Stats before=gi.GetStats();gi.SetContributor(1,1,Floor({0,1,0},2));gi.SetSun({{0,1,0},{4,4,4}});
        Require(!gi.AdvanceUpdate(4096),"unchanged contributor revision must be a no-op");
        Stats after=gi.GetStats();Require(before.TriangleVoxelTests==after.TriangleVoxelTests&&before.RaysTraced==after.RaysTraced&&before.Publications==after.Publications,"no-change update performed work");
        gi.SetContributor(2,1,Roof(0,8));Complete(gi);
        Vec3 blocked=gi.SampleIrradiance({4.5f,3.5f,4.5f},{0,-1,0});
        Require(blocked.X<sample.X*0.01f,"opaque roof failed to block sun from interior floor");
        gi.SetContributor(2,2,Roof(0,3));Complete(gi);
        Vec3 window=gi.SampleIrradiance({5.5f,3.5f,4.5f},{0,-1,0});
        Require(window.X>blocked.X+0.05f,"opening must admit sun and create reflected interior light");
        gi.RemoveContributor(2);Complete(gi);Require(std::abs(Energy(gi.GetField())-open)<0.01f,"removed roof left stale coverage");
        gi.SetContributor(3,1,Floor({1,0.1f,0.02f},1));Complete(gi);
        gi.RemoveContributor(1);Complete(gi);Require(std::abs(Energy(gi.GetField())-open)<0.01f,"removing overlapping contributor removed another contributor's voxels");
        gi.SetContributor(3,2,Floor({1,0.1f,0.02f},2));Complete(gi);
        std::size_t oldProbe=((0+8*(0+8*0))*4)+3;Require(gi.GetField().AtlasRgba[oldProbe]==1,"moving geometry left stale old occupancy");
        gi.RemoveContributor(3);Complete(gi);Require(Energy(gi.GetField())==0,"empty scene retained bounced light");
        for(float value:gi.GetField().AtlasRgba) Require(std::isfinite(value),"atlas contains NaN or infinity");
        Require(gi.SampleIrradiance({-1,0,0},{0,1,0}).X==0,"outside-volume sample must be dark");
        bool failed=false;try {VoxelGi invalid({{0,0,0},{0,1,1},8,8,8,1});}catch(const std::invalid_argument&) {failed=true;}Require(failed,"invalid extent was accepted");
        failed=false;try {gi.SetSun({{0,0,0},{1,1,1}});}catch(const std::invalid_argument&) {failed=true;}Require(failed,"zero sun direction was accepted");
        failed=false;try {gi.SampleIrradiance({1,1,1},{0,0,0});}catch(const std::invalid_argument&) {failed=true;}Require(failed,"zero sample normal was accepted");
        Triangle diagonal{{0,0,0},{8,0,0},{0,0,8},{1,1,1}};
        Require(!Math::Intersects(diagonal,{7.5f,0.5f,7.5f},{0.5f,0.5f,0.5f}),"voxelizer fills triangle AABB outside triangle");
        Require(Math::Intersects(diagonal,{0.5f,0.5f,0.5f},{0.5f,0.5f,0.5f}),"conservative touching triangle was lost");
        gi.SetContributor(1,3,Floor({1,1,1},1));gi.BeginUpdate();std::uint64_t snapshot=gi.GetStats().InputGeneration;
        gi.SetContributor(1,4,Floor({0,1,0},2));
        while(!gi.AdvanceUpdate(512)) {}
        Require(gi.GetField().Generation==snapshot&&gi.GetStats().UpdatePending,"moving scene starved snapshot or lost queued input");Complete(gi);
        before=gi.GetStats();gi.SetSun({{1,1,0},{4,4,4}});Complete(gi);after=gi.GetStats();
        Require(after.Publications==before.Publications+1&&after.TriangleVoxelTests==before.TriangleVoxelTests,"sun-only edit revoxelized unchanged geometry");
        VoxelGi partition(config);
        partition.SetSun({{0,1,0},{4,4,4}});
        partition.SetContributor(1,1,{{{0,1,0},{0,1,8},{3,1,0},{1,0,0}},{{3,1,0},{0,1,8},{3,1,8},{1,0,0}}});
        Complete(partition);
        float unobstructed=partition.SampleIrradiance({6.5f,3.5f,4.5f},{-1,0,0}).X;
        Require(unobstructed>0.01f,"partition scene must have a visible red reflector");
        partition.SetContributor(2,1,{{{4,0,0},{4,8,0},{4,0,8},{0,0,0}},{{4,8,0},{4,8,8},{4,0,8},{0,0,0}}});
        Complete(partition);
        Require(partition.SampleIrradiance({6.5f,3.5f,4.5f},{-1,0,0}).X==0,"opaque wall leaked reflected radiance");
        partition.SetContributor(2,2,{{{4,0,0},{4,8,0},{4,0,3},{0,0,0}},{{4,8,0},{4,8,3},{4,0,3},{0,0,0}},{{4,0,6},{4,8,6},{4,0,8},{0,0,0}},{{4,8,6},{4,8,8},{4,0,8},{0,0,0}}});
        Complete(partition);
        Require(partition.SampleIrradiance({6.5f,3.5f,4.5f},{-1,0,0}).X>0.01f,"wall aperture failed to transmit reflected light");
        VoxelGi clipped({{0,0,0},{0.01f,0.01f,0.01f},2,2,2,1});
        clipped.SetSun({{0,1,0},{1,1,1}});
        clipped.SetContributor(1,1,{{{-1000000,0.0025f,-1000000},{0,0.0025f,1000000},{1000000,0.0025f,-1000000},{1,1,1}}});
        Complete(clipped);
        for(float value:clipped.GetField().AtlasRgba) Require(std::isfinite(value),"clipped large triangle produced nonfinite output");
        VoxelGi grazing(config);
        grazing.SetContributor(1,1,Floor({1,1,1},1));
        grazing.SetSun({{0,1,0},{4,4,4}});Complete(grazing);
        float vertical=grazing.SampleIrradiance({4.5f,3.5f,4.5f},{0,-1,0}).X;
        grazing.SetSun({{1,0.25f,0},{4,4,4}});Complete(grazing);
        float grazingOpen=grazing.SampleIrradiance({4.5f,3.5f,4.5f},{0,-1,0}).X;
        Require(std::abs(grazingOpen/vertical-0.242535625f)<0.02f,"grazing sun self-shadowed the interior of an unobstructed planar floor");
        grazing.SetContributor(2,1,{{{7.5f,0,0},{7.5f,8,0},{7.5f,0,8},{0,0,0}},{{7.5f,8,0},{7.5f,8,8},{7.5f,0,8},{0,0,0}}});
        Complete(grazing);
        Require(grazing.SampleIrradiance({4.5f,3.5f,4.5f},{0,-1,0}).X==0,"grazing self-coverage exemption leaked through a real wall");
        VoxelGi grazingRoof({{0,0,0},{8,8,8},8,32,8,96});
        grazingRoof.SetContributor(1,1,Floor({1,1,1},1));
        grazingRoof.SetSun({{1,0.25f,0},{4,4,4}});Complete(grazingRoof);
        float beforeRoof=grazingRoof.SampleIrradiance({1.5f,1.625f,4.5f},{0,-1,0}).X;
        grazingRoof.SetContributor(2,1,{{{0,1.75f,0},{8,1.75f,0},{0,1.75f,8},{0,0,0}},{{8,1.75f,0},{8,1.75f,8},{0,1.75f,8},{0,0,0}}});
        Complete(grazingRoof);
        float afterRoof=grazingRoof.SampleIrradiance({1.5f,1.625f,4.5f},{0,-1,0}).X;
        Require(beforeRoof>0.1f&&afterRoof<beforeRoof*0.1f,"a parallel displaced roof failed to block grazing sun");
        Require(!Math::Coplanar({0,1,0},1,Math::Unit({0.0001f,1,0}),1,1),"coplanar exemption merged a distinct nearly parallel tilted plane");
        Require(!Math::Coplanar({0,1,0},1,{0,1,0},1.0001f,1),"coplanar exemption merged a distinct thin roof plane");
        Require(Math::Coplanar({0,1,0},1,{0,-1,0},-1,1),"coplanar exemption failed for reversed winding on the same plane");
        VoxelGi metadata(config);
        std::vector<Triangle> reversedFloor=Floor({1,1,1},1);
        for(Triangle& triangle:reversedFloor) std::swap(triangle.B,triangle.C);
        metadata.SetContributor(1,1,reversedFloor);Complete(metadata);
        const std::size_t metadataCount=static_cast<std::size_t>(config.CountX)*config.CountY*config.CountZ;
        const std::size_t metadataCell=4+8*(1+8*4);
        const Field& planeField=metadata.GetField();
        for(unsigned int lobe=0;lobe<6;++lobe) Require(planeField.AtlasRgba[(metadataCell+metadataCount*lobe)*4+3]==0,"uniform occupied atlas alpha must be zero");
        Require(planeField.AtlasRgba[metadataCell*4]==0&&planeField.AtlasRgba[metadataCell*4+1]==1&&planeField.AtlasRgba[metadataCell*4+2]==0,"occupied atlas normal must be canonical despite reversed winding");
        Require(planeField.AtlasRgba[(metadataCell+metadataCount)*4]==1,"occupied atlas must retain the matching canonical plane offset");
        Require(planeField.AtlasRgba[(metadataCell+metadataCount)*4+1]==0&&planeField.AtlasRgba[(metadataCell+metadataCount)*4+2]==0,"unused occupied atlas RGB must remain zero");
        metadata.SetContributor(2,1,Floor({1,1,1},1));Complete(metadata);
        Require(metadata.GetField().AtlasRgba[metadataCell*4+3]==0,"reversed coplanar overlap must retain uniform metadata");
        metadata.SetContributor(3,1,Floor({1,1,1},1.25f));Complete(metadata);
        for(unsigned int lobe=0;lobe<6;++lobe) {
            std::size_t offset=(metadataCell+metadataCount*lobe)*4;
            Require(metadata.GetField().AtlasRgba[offset+3]==-1,"different planes sharing one voxel must publish mixed alpha");
            Require(metadata.GetField().AtlasRgba[offset]==0&&metadata.GetField().AtlasRgba[offset+1]==0&&metadata.GetField().AtlasRgba[offset+2]==0,"mixed occupied atlas must not publish usable plane metadata");
        }
        Require(metadata.SampleIrradiance({4.5f,1.5f,4.5f},{0,1,0}).X==0,"CPU sampling must continue rejecting mixed occupied probes");
        std::cout<<"PASS: portable voxel GI geometry, window visibility, colored bounce, incremental coverage, staging and validation\n";
        return 0;
    } catch(const std::exception& error) {std::cerr<<"FAIL: "<<error.what()<<'\n';return 1;}
}






