#include "vulkan_mesh_renderer.hpp"
#include "vulkan_renderer_2d.hpp"
#include <fstream>
#include <cstring>
#include <iostream>
#include <stdexcept>
#include <limits>
#include <cstdlib>
#include <cmath>
#include <algorithm>
using namespace helengine::vulkan;
/// <summary>Selects the actual authored TransformBuffer layout when a compiled ForwardStandard VS is supplied explicitly.</summary>
size_t TransformBytes=64;
/// <summary>Locates WVP inside either the minimal GLSL fixture or the authored engine TransformBuffer.</summary>
size_t MatrixOffset=0;
/// <summary>Creates identity world/normal data and the exact serialized WVP bytes for either shader contract.</summary>
VulkanMeshRenderer::BufferBinding TransformBinding(const std::array<float,16>& matrix) {
    VulkanMeshRenderer::BufferBinding result{0,std::vector<uint8_t>(TransformBytes)};
    if(TransformBytes==224){std::array<float,16> identity{};identity[0]=identity[5]=identity[10]=identity[15]=1;std::memcpy(result.Bytes.data(),identity.data(),64);std::memcpy(result.Bytes.data()+128,identity.data(),64);}
    std::memcpy(result.Bytes.data()+MatrixOffset,matrix.data(),64);return result;
}
/// <summary>Rejects a numerical or ownership regression independently of release assertions.</summary>
void RequireTest(bool value,const char* message){if(!value)throw std::runtime_error(message);}
/// <summary>Loads the test's unchanged compiled graphics shader.</summary>
std::vector<uint32_t> LoadWords(const char* name){std::ifstream file(name[0]=='/'?std::string(name):std::string(HELENGINE_VULKAN_SHADER_ROOT)+"/"+name,std::ios::binary|std::ios::ate);if(!file)throw std::runtime_error("Missing indirect fixture shader.");const auto size=file.tellg();std::vector<uint32_t> words(size/4);file.seekg(0);file.read(reinterpret_cast<char*>(words.data()),size);return words;}
/// <summary>Constructs serialized column-major storage for the engine-style row-vector transform.</summary>
std::array<float,16> Camera(float offset){std::array<float,16> matrix{};matrix[0]=matrix[5]=matrix[10]=matrix[15]=1;matrix[3]=offset;return matrix;}
/// <summary>Computes a double-precision clip-space reference from actual triangle bounds for non-boundary camera fixtures.</summary>
size_t ReferenceVisible(std::span<const VulkanMeshRenderer::Vertex> vertices,std::span<const uint32_t> indices,std::span<const VulkanIndirectRange> ranges,const std::array<float,16>& matrix) {
    size_t visible=0;
    for(const auto& range:ranges){
        std::array<double,3> lower{INFINITY,INFINITY,INFINITY},upper{-INFINITY,-INFINITY,-INFINITY};
        for(size_t index=range.FirstIndex;index<size_t(range.FirstIndex)+range.IndexCount;++index)for(size_t axis=0;axis<3;++axis){const double value=vertices[indices[index]].Position[axis];lower[axis]=std::min(lower[axis],value);upper[axis]=std::max(upper[axis],value);}
        std::array<bool,6> outside{true,true,true,true,true,true};
        for(uint32_t corner=0;corner<8;++corner){
            const std::array<double,4> position{corner&1?upper[0]:lower[0],corner&2?upper[1]:lower[1],corner&4?upper[2]:lower[2],1};std::array<double,4> clip{};
            for(size_t column=0;column<4;++column)for(size_t row=0;row<4;++row)clip[column]+=position[row]*matrix[column*4+row];
            outside[0]=outside[0]&&clip[0]<-clip[3];outside[1]=outside[1]&&clip[0]>clip[3];outside[2]=outside[2]&&clip[1]<-clip[3];outside[3]=outside[3]&&clip[1]>clip[3];outside[4]=outside[4]&&clip[2]<0;outside[5]=outside[5]&&clip[2]>clip[3];
        }
        if(std::none_of(outside.begin(),outside.end(),[](bool rejected){return rejected;}))++visible;
    }
    return visible;
}
/// <summary>Renders ordinary or GPU-selected ranges and returns numerical pixels without writing images.</summary>
std::vector<uint8_t> Render(VulkanMeshRenderer& mesh,VulkanRenderer2D& target,size_t geometry,size_t material,const std::array<float,16>& matrix,bool indirect,bool repeat=false,uint32_t secondFirst=3){
    auto transform=TransformBinding(matrix);
    const std::array<VulkanMeshRenderer::BufferBinding,1> bindings{transform};
    if(indirect)mesh.QueueIndirectPage(geometry,material,bindings,{},matrix);
    else {mesh.QueueDraw(geometry,material,bindings,{},0,3);mesh.QueueDraw(geometry,material,bindings,{},secondFirst,3);}
    if(repeat){const auto other=Camera(-1.2f);std::memcpy(transform.Bytes.data()+MatrixOffset,other.data(),64);const std::array<VulkanMeshRenderer::BufferBinding,1> changed{transform};if(indirect)mesh.QueueIndirectPage(geometry,material,changed,{},other);else{mesh.QueueDraw(geometry,material,changed,{},0,3);mesh.QueueDraw(geometry,material,changed,{},secondFirst,3);}}
    mesh.RenderFrame(target.GetOutputImageView(),64,64,{0,0,0,1});target.BeginFrame({0,0,0,1},true);return target.EndFrameAndReadback();
}
/// <summary>Interleaves ordinary geometry and repeated indirect pages with distinct matrices to test the shared compute barrier boundary.</summary>
std::vector<uint8_t> RenderMixed(VulkanMeshRenderer& mesh,VulkanRenderer2D& target,size_t direct,size_t page,size_t material,bool indirect) {
    const std::array<float,4> offsets{0,1.2f,-1.2f,0};
    for(size_t step=0;step<offsets.size();++step){const auto camera=Camera(offsets[step]);const std::array<VulkanMeshRenderer::BufferBinding,1> bindings{TransformBinding(camera)};
        if(indirect && step%2)mesh.QueueIndirectPage(page,material,bindings,{},camera);
        else mesh.QueueDraw(direct,material,bindings,{},0,6);
    }
    mesh.RenderFrame(target.GetOutputImageView(),64,64,{0,0,0,1});target.BeginFrame({0,0,0,1},true);return target.EndFrameAndReadback();
}
/// <summary>Exercises real visibility restoration, matrix convention, pixel parity and pending-resource guards under Vulkan validation.</summary>
int main(int argc,char** argv){try{
    if(argc>1){TransformBytes=224;MatrixOffset=64;}
    VulkanContext context({},true,VK_API_VERSION_1_2);VulkanRenderer2D target(context,64,64);VulkanMeshRenderer renderer(context);
    const std::array<VulkanMeshRenderer::Vertex,6> vertices{{{{-.9f,-.2f,.5f},{0,0,1},{0,0}},{{-.3f,-.2f,.5f},{0,0,1},{0,0}},{{-.6f,.3f,.5f},{0,0,1},{0,0}},{{.3f,-.2f,.5f},{0,0,1},{0,0}},{{.9f,-.2f,.5f},{0,0,1},{0,0}},{{.6f,.3f,.5f},{0,0,1},{0,0}}}};
    const std::array<uint32_t,6> indices{0,1,2,3,4,5};const std::array<VulkanIndirectRange,2> ranges{{{0,3},{3,3}}};
    const auto page=renderer.UploadIndirectPage(vertices,indices,ranges);const auto direct=renderer.UploadMesh(vertices,indices);
    RequireTest(renderer.IsIndirectPage(page)&&!renderer.IsIndirectPage(direct),"Page identity lost.");RequireTest(renderer.GetIndirectPageBytes(page)==size_t(216+(context.IsGraphicsComputeEnabled()?104:0)),"Page payload accounting differs.");
    const std::array<VulkanMeshRenderer::BindingLayout,1> layout{{{0,VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,VK_SHADER_STAGE_VERTEX_BIT,TransformBytes}}};
    VulkanMeshRenderer::MaterialState state;state.CullMode=0;
    const auto material=renderer.UploadMaterial(LoadWords(argc>1?argv[1]:"indirect_page.vert.spv"),argc>1?"VS":"main",LoadWords("indirect_page.frag.spv"),"main",layout,state);
    const auto uploadedBytes=renderer.GetIndirectUploadBytes();
    const bool cullingEnabled=!std::getenv("HELENGINE_VULKAN_GPU_CULLING") || std::string(std::getenv("HELENGINE_VULKAN_GPU_CULLING"))!="0";
    for(float offset:{0.f,1.2f,-1.2f,3.f,0.f}){
        const auto camera=Camera(offset);const auto expected=Render(renderer,target,direct,material,camera,false);const auto actual=Render(renderer,target,page,material,camera,true);
        RequireTest(expected==actual,"GPU frustum page differs from direct numerical pixels.");RequireTest(renderer.GetLastIndirectCommandCount()==2,"Logical command count differs.");
        RequireTest(renderer.GetLastIndirectDispatchCount()==size_t(context.IsGraphicsComputeEnabled() && cullingEnabled),"Compute dispatch accounting differs.");
        if(context.IsGraphicsComputeEnabled())RequireTest(renderer.GetLastIndirectCallCount()==size_t(!cullingEnabled?0:context.IsMultiDrawIndirectEnabled()?1:2),"Indirect call accounting differs.");
        if(context.IsGraphicsComputeEnabled())RequireTest(renderer.ReadIndirectVisibleCommandCount(page)==size_t(!cullingEnabled?2:offset==3?0:offset==0?2:1),"GPU visibility slots differ from expected frustum selection.");
        if(!cullingEnabled || !context.IsGraphicsComputeEnabled())RequireTest(renderer.GetLastPageDirectCallCount()==1,"Contiguous fallback must merge into one direct API call.");
        if(offset==3)RequireTest(renderer.GetLastVisibleSamples()==0,"Outside page still produces samples.");else RequireTest(renderer.GetLastVisibleSamples()>0,"Visible page disappeared.");
    }
    for(float offset:{0.f,1.5f,3.f}) {
        auto complex=Camera(offset);complex[0]=.8f;complex[1]=-.6f;complex[4]=.6f;complex[5]=.8f;complex[7]=-.1f;complex[12]=.4f;complex[14]=.25f;
        const auto expected=Render(renderer,target,direct,material,complex,false);
        RequireTest(expected==Render(renderer,target,page,material,complex,true),"Rotated translated perspective culling differs from authored direct pixels.");
        if(cullingEnabled && context.IsGraphicsComputeEnabled())RequireTest(renderer.ReadIndirectVisibleCommandCount(page)==ReferenceVisible(vertices,indices,ranges,complex),"Perspective GPU commands differ from independent clip-space reference.");
    }
    RequireTest(renderer.GetIndirectUploadBytes()==uploadedBytes,"Warm frames reuploaded persistent page bytes.");
    RequireTest(Render(renderer,target,direct,material,Camera(1.2f),false,true)==Render(renderer,target,page,material,Camera(1.2f),true,true),"Repeated page with another camera must preserve both draws.");
    const auto mixedExpected=RenderMixed(renderer,target,direct,page,material,false);
    RequireTest(mixedExpected==RenderMixed(renderer,target,direct,page,material,true),"Mixed ordinary/indirect repeated-camera packets changed pixels.");
    const auto borrowedSource=renderer.UploadMesh(vertices,indices);
    const auto borrowed=renderer.UploadIndirectPageForMesh(borrowedSource,vertices,indices,ranges);
    RequireTest(renderer.GetIndirectPageBytes(borrowed)==size_t(context.IsGraphicsComputeEnabled()?104:0),"Shared page must account only its metadata.");
    renderer.ReleaseMesh(borrowedSource);
    RequireTest(Render(renderer,target,direct,material,Camera(-1.2f),false)==Render(renderer,target,borrowed,material,Camera(-1.2f),true),"Source retirement destroyed shared GPU geometry.");
    renderer.ReleaseMesh(borrowed);
    const std::array<uint32_t,9> gapIndices{0,1,2,0,1,2,3,4,5};const std::array<VulkanIndirectRange,2> gapRanges{{{0,3},{6,3}}};
    const auto gapPage=renderer.UploadIndirectPage(vertices,gapIndices,gapRanges),gapDirect=renderer.UploadMesh(vertices,gapIndices);
    const auto gapExpected=Render(renderer,target,gapDirect,material,Camera(1.2f),false,true,6);
    RequireTest(gapExpected==Render(renderer,target,gapPage,material,Camera(1.2f),true,true,6),"Gapped page fallback must not draw omitted triangles.");
    RequireTest(renderer.GetLastPageDirectCallCount()==size_t(cullingEnabled && context.IsGraphicsComputeEnabled()?2:4),"Gapped fallback must preserve separate direct calls.");
    renderer.ReleaseMesh(gapPage);renderer.ReleaseMesh(gapDirect);
    const auto camera=Camera(0);auto transform=TransformBinding(camera);const std::array<VulkanMeshRenderer::BufferBinding,1> bindings{transform};renderer.QueueIndirectPage(page,material,bindings,{},camera);
    bool rejected=false;try{renderer.ReleaseMesh(page);}catch(const std::logic_error&){rejected=true;}RequireTest(rejected,"Pending page release was accepted.");renderer.DiscardFrame();
    // Render with finite bindings while deliberately making culling arithmetic uncertain.
    auto uncertain=camera;uncertain[0]=std::numeric_limits<float>::infinity();
    renderer.QueueIndirectPage(page,material,bindings,{},uncertain);renderer.RenderFrame(target.GetOutputImageView(),64,64,{0,0,0,1});target.BeginFrame({0,0,0,1},true);target.EndFrameAndReadback();
    RequireTest(renderer.ReadIndirectVisibleCommandCount(page)==2,"Uncertain projection must retain every slot.");
    RequireTest(renderer.GetLastIndirectDispatchCount()==size_t(context.IsGraphicsComputeEnabled() && cullingEnabled),"Discard must reset the page borrowing flag.");
    rejected=false;try{const std::array<VulkanIndirectRange,1> invalid{{{4,3}}};renderer.UploadIndirectPage(vertices,indices,invalid);}catch(const std::invalid_argument&){rejected=true;}RequireTest(rejected,"Invalid page range accepted.");
    renderer.ReleaseMesh(page);renderer.ReleaseMesh(direct);renderer.ReleaseMaterial(material);RequireTest(context.GetValidationErrorCount()==0,"Vulkan validation reported an error.");
    std::cout<<"PASS GPU static pages: either/both/neither/restore, direct pixel parity, repeated camera and release guards\n";
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}}
