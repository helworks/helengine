#include "EncoderSurfacePool.h"
#include "EncoderFailure.h"
#include <d3d11_4.h>
#include <libavutil/mem.h>
extern "C" {
#include <libavutil/hwcontext_d3d11va.h>
}
using Microsoft::WRL::ComPtr;
EncoderSurfacePool::~EncoderSurfacePool() { av_buffer_unref(&Frames); av_buffer_unref(&Hardware); }
void EncoderSurfacePool::Free(void*,uint8_t* data) {
    auto* frame=reinterpret_cast<AVD3D11FrameDescriptor*>(data); frame->texture->Release(); av_free(frame);
}
AVBufferRef* EncoderSurfacePool::Allocate(void* opaque,size_t) {
    auto* owner=static_cast<EncoderSurfacePool*>(opaque);
    if(owner->Allocated.fetch_add(1)>=8) { owner->Allocated.fetch_sub(1); return nullptr; }
    D3D11_TEXTURE2D_DESC desc{}; desc.Width=(owner->Width+1)&~1; desc.Height=(owner->Height+1)&~1; desc.MipLevels=1; desc.ArraySize=1; desc.Format=DXGI_FORMAT_NV12; desc.SampleDesc.Count=1; desc.BindFlags=D3D11_BIND_RENDER_TARGET;
    auto* frame=static_cast<AVD3D11FrameDescriptor*>(av_mallocz(sizeof(AVD3D11FrameDescriptor)));
    if(!frame) { owner->Allocated.fetch_sub(1); return nullptr; }
    if(FAILED(owner->Device->CreateTexture2D(&desc,nullptr,&frame->texture))) { av_free(frame); owner->Allocated.fetch_sub(1); return nullptr; }
    auto* buffer=av_buffer_create(reinterpret_cast<uint8_t*>(frame),sizeof(*frame),Free,nullptr,0);
    if(!buffer) { Free(nullptr,reinterpret_cast<uint8_t*>(frame)); owner->Allocated.fetch_sub(1); }
    return buffer;
}
void EncoderSurfacePool::Open(ID3D11Device* device,int width,int height) {
    Device=device; Width=width; Height=height; Device->GetImmediateContext(&Context);
    ComPtr<ID3D11Multithread> multithread; EncoderFailure::CheckHr(Context.As(&multithread),"D3D11 multithread context"); multithread->SetMultithreadProtected(TRUE);
    EncoderFailure::CheckHr(Device.As(&VideoDevice),"D3D11 video device"); EncoderFailure::CheckHr(Context.As(&VideoContext),"D3D11 video context");
    D3D11_VIDEO_PROCESSOR_CONTENT_DESC content{}; content.InputFrameFormat=D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE; content.InputWidth=(width+1)&~1; content.InputHeight=(height+1)&~1; content.OutputWidth=(width+1)&~1; content.OutputHeight=(height+1)&~1; content.InputFrameRate={60,1}; content.OutputFrameRate={60,1}; content.Usage=D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;
    EncoderFailure::CheckHr(VideoDevice->CreateVideoProcessorEnumerator(&content,&Enumerator),"Create GPU conversion enumerator");
    UINT inputSupport=0; UINT outputSupport=0; Enumerator->CheckVideoProcessorFormat(DXGI_FORMAT_B8G8R8A8_UNORM,&inputSupport); Enumerator->CheckVideoProcessorFormat(DXGI_FORMAT_NV12,&outputSupport);
    EncoderFailure::Require((inputSupport&D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_INPUT) && (outputSupport&D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_OUTPUT),"GPU does not support BGRA to NV12 video processing.");
    EncoderFailure::CheckHr(VideoDevice->CreateVideoProcessor(Enumerator.Get(),0,&Processor),"Create GPU converter");
    D3D11_VIDEO_PROCESSOR_COLOR_SPACE inputColor{}; inputColor.RGB_Range=0; inputColor.YCbCr_Matrix=1; inputColor.Nominal_Range=2;
    D3D11_VIDEO_PROCESSOR_COLOR_SPACE outputColor{}; outputColor.YCbCr_Matrix=1; outputColor.Nominal_Range=1;
    VideoContext->VideoProcessorSetStreamColorSpace(Processor.Get(),0,&inputColor); VideoContext->VideoProcessorSetOutputColorSpace(Processor.Get(),&outputColor);
    // Disable driver image enhancements so conversion does not modify menu colors.
    VideoContext->VideoProcessorSetStreamAutoProcessingMode(Processor.Get(),0,FALSE);
    VideoContext->VideoProcessorSetStreamFrameFormat(Processor.Get(),0,D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE);
    RECT sourceRect{0,0,(width+1)&~1,(height+1)&~1}; VideoContext->VideoProcessorSetStreamSourceRect(Processor.Get(),0,TRUE,&sourceRect); VideoContext->VideoProcessorSetStreamDestRect(Processor.Get(),0,TRUE,&sourceRect);
    RECT outputRect{0,0,(width+1)&~1,(height+1)&~1}; VideoContext->VideoProcessorSetOutputTargetRect(Processor.Get(),TRUE,&outputRect);
    D3D11_VIDEO_COLOR black{}; black.YCbCr={16.0f/255.0f,128.0f/255.0f,128.0f/255.0f,1}; VideoContext->VideoProcessorSetOutputBackgroundColor(Processor.Get(),TRUE,&black);
    if((width&1) || (height&1)) {
        D3D11_TEXTURE2D_DESC desc{}; desc.Width=sourceRect.right; desc.Height=sourceRect.bottom; desc.MipLevels=1; desc.ArraySize=1; desc.Format=DXGI_FORMAT_B8G8R8A8_UNORM; desc.SampleDesc.Count=1; desc.BindFlags=D3D11_BIND_RENDER_TARGET;
        EncoderFailure::CheckHr(Device->CreateTexture2D(&desc,nullptr,&PaddedInput),"Create even BGRA padding surface");
        EncoderFailure::CheckHr(Device->CreateRenderTargetView(PaddedInput.Get(),nullptr,&PaddedTarget),"Create padding target");
    }
    Hardware=av_hwdevice_ctx_alloc(AV_HWDEVICE_TYPE_D3D11VA); if(!Hardware) { throw std::bad_alloc(); }
    auto* context=reinterpret_cast<AVHWDeviceContext*>(Hardware->data); auto* d3d=reinterpret_cast<AVD3D11VADeviceContext*>(context->hwctx); d3d->device=Device.Get(); d3d->device->AddRef();
    EncoderFailure::Check(av_hwdevice_ctx_init(Hardware),"Initialize encoder D3D11 device");
    Frames=av_hwframe_ctx_alloc(Hardware); if(!Frames) { throw std::bad_alloc(); }
    auto* frames=reinterpret_cast<AVHWFramesContext*>(Frames->data); frames->format=AV_PIX_FMT_D3D11; frames->sw_format=AV_PIX_FMT_NV12; frames->width=outputRect.right; frames->height=outputRect.bottom;
    // NV12 render-target arrays are rejected by this driver; a custom allocator
    // provides independent render targets while enforcing the same hard budget.
    frames->pool=av_buffer_pool_init2(sizeof(AVD3D11FrameDescriptor),this,Allocate,nullptr); if(!frames->pool) { throw std::bad_alloc(); }
    auto* nativeFrames=reinterpret_cast<AVD3D11VAFramesContext*>(frames->hwctx); nativeFrames->BindFlags=D3D11_BIND_RENDER_TARGET;
    EncoderFailure::Check(av_hwframe_ctx_init(Frames),"Initialize bounded NV12 surface pool");
}
void EncoderSurfacePool::Convert(ID3D11Texture2D* source,AVFrame* destination) {
    EncoderFailure::Require(source!=nullptr,"A BGRA texture is required.");
    D3D11_TEXTURE2D_DESC description{}; source->GetDesc(&description);
    EncoderFailure::Require(description.Width==Width && description.Height==Height && description.Format==DXGI_FORMAT_B8G8R8A8_UNORM && description.ArraySize==1 && description.MipLevels==1 && description.SampleDesc.Count==1,"Texture format or dimensions differ from the encoder input.");
    ComPtr<ID3D11Device> owner; source->GetDevice(&owner); ComPtr<IUnknown> identity; ComPtr<IUnknown> expected; owner.As(&identity); Device.As(&expected); EncoderFailure::Require(identity.Get()==expected.Get(),"Texture belongs to a different D3D11 device.");
    EncoderFailure::Check(av_hwframe_get_buffer(Frames,destination,0),"Acquire fixed GPU surface (maximum eight)");
    if(PaddedInput) {
        const float black[]={0,0,0,1}; Context->ClearRenderTargetView(PaddedTarget.Get(),black);
        D3D11_BOX content{0,0,0,static_cast<UINT>(Width),static_cast<UINT>(Height),1}; Context->CopySubresourceRegion(PaddedInput.Get(),0,0,0,0,source,0,&content); source=PaddedInput.Get();
    }
    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputDescription{}; inputDescription.ViewDimension=D3D11_VPIV_DIMENSION_TEXTURE2D;
    ComPtr<ID3D11VideoProcessorInputView> input; EncoderFailure::CheckHr(VideoDevice->CreateVideoProcessorInputView(source,Enumerator.Get(),&inputDescription,&input),"Create BGRA conversion view");
    D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputDescription{}; outputDescription.ViewDimension=D3D11_VPOV_DIMENSION_TEXTURE2D; outputDescription.Texture2D.MipSlice=0;
    ComPtr<ID3D11VideoProcessorOutputView> output; EncoderFailure::CheckHr(VideoDevice->CreateVideoProcessorOutputView(reinterpret_cast<ID3D11Texture2D*>(destination->data[0]),Enumerator.Get(),&outputDescription,&output),"Create NV12 conversion view");
    D3D11_VIDEO_PROCESSOR_STREAM stream{}; stream.Enable=TRUE; stream.pInputSurface=input.Get();
    EncoderFailure::CheckHr(VideoContext->VideoProcessorBlt(Processor.Get(),output.Get(),0,1,&stream),"Convert RGB full to BT709 limited");
    // Submit GPU commands before returning the caller's borrowed source lease.
    Context->Flush();
}
