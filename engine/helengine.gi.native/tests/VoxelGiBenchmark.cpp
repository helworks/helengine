#include "helgi/VoxelGi.h"
#include <algorithm>
#include <chrono>
#include <iostream>
using namespace helgi;
/// <summary>Measures total solve cost and maximum observed bounded update call for a fixed room aperture.</summary>
int main() {
 for(unsigned budget:{4096u,8192u}) {
  VoxelGi gi({{0,0,0},{8,6,8},32,24,32,8});
  gi.SetSun({{0.3f,1,0.1f},{4,4,4}});
  gi.SetContributor(1,1,{{{0,1,0},{0,1,8},{8,1,0},{1,0.3f,0.1f}},{{8,1,0},{0,1,8},{8,1,8},{1,0.3f,0.1f}}});
  gi.SetContributor(2,1,{{{0,5,0},{3,5,0},{0,5,8},{1,1,1}},{{3,5,0},{3,5,8},{0,5,8},{1,1,1}},{{6,5,0},{8,5,0},{6,5,8},{1,1,1}},{{8,5,0},{8,5,8},{6,5,8},{1,1,1}}});
  auto begin=std::chrono::steady_clock::now();double max=0;unsigned calls=0;
  while(gi.GetStats().UpdatePending) {auto a=std::chrono::steady_clock::now();gi.AdvanceUpdate(budget);double elapsed=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-a).count();max=std::max(max,elapsed);++calls;}
  double elapsed=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-begin).count();
  std::cout<<"budget="<<budget<<" calls="<<calls<<" total_ms="<<elapsed<<" max_call_ms="<<max<<" rays="<<gi.GetStats().RaysTraced<<" tests="<<gi.GetStats().TriangleVoxelTests<<'\n';
 }
}

