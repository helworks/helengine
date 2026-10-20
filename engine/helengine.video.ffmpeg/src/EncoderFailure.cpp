#include "EncoderFailure.h"
extern "C" {
#include <libavutil/error.h>
}
void EncoderFailure::Check(int result,const char* operation) {
    if(result<0) { char text[AV_ERROR_MAX_STRING_SIZE]{}; av_strerror(result,text,sizeof(text)); throw EncoderFailure(result,std::string(operation)+": "+text); }
}
void EncoderFailure::CheckHr(HRESULT result,const char* operation) {
    if(FAILED(result)) { throw EncoderFailure(static_cast<int>(result),std::string(operation)+" (HRESULT "+std::to_string(static_cast<unsigned long>(result))+")"); }
}
void EncoderFailure::Require(bool condition,const char* message) { if(!condition) { throw EncoderFailure(E_INVALIDARG,message); } }
std::string EncoderFailure::Utf8(const wchar_t* path) {
    Require(path && path[0],"A UTF-16 output path is required.");
    int count=WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,path,-1,nullptr,0,nullptr,nullptr); Require(count>1,"Invalid output path.");
    std::string result(count,'\0'); WideCharToMultiByte(CP_UTF8,WC_ERR_INVALID_CHARS,path,-1,result.data(),count,nullptr,nullptr); result.resize(count-1); return result;
}
