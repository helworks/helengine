#include "helgi/VoxelGi.h"
#include "Math.h"
#include "Work.h"
#include <limits>
#include <stdexcept>
#include <utility>

namespace helgi {
/// <summary>Single-threaded retained scene and staged finite-grid transport implementation.</summary>
class VoxelGi::Impl {
public:
    /// <summary>Validated explicit volume description.</summary>
    Config Settings;
    /// <summary>World dimensions of one voxel.</summary>
    Vec3 Size;
    /// <summary>Latest caller input generation.</summary>
    std::uint64_t Generation = 0;
    /// <summary>Latest validated sun; zero irradiance is dark before the first explicit sun.</summary>
    Sun Light{{0,1,0},{0,0,0}};
    /// <summary>Latest immutable contributor payloads.</summary>
    std::map<std::uint64_t,std::shared_ptr<const Contributor>> Inputs;
    /// <summary>Coverage belonging to the last completed generation.</summary>
    std::map<std::uint64_t,std::shared_ptr<Coverage>> Cached;
    /// <summary>Last complete atlas exposed to consumers.</summary>
    Field Published;
    /// <summary>Occupancy synchronized with the published atlas.</summary>
    std::vector<Cell> PublishedCells;
    /// <summary>Private in-flight generation, if any.</summary>
    std::unique_ptr<Work> Pending;
    /// <summary>Cumulative actual work measurements.</summary>
    Stats Counters{};

    /// <summary>Validates finite storage bounds before allocating the fixed volume.</summary>
    explicit Impl(const Config& config):Settings(config) {
        if(!Math::Finite(config.Minimum)||!Math::Finite(config.Maximum)) throw std::invalid_argument("GI bounds must be finite");
        if(config.CountX<1||config.CountX>128||config.CountY<1||config.CountY>128||config.CountZ<1||config.CountZ>128||config.RaysPerLobe<1||config.RaysPerLobe>256) throw std::invalid_argument("GI dimensions or rays outside supported bounds");
        if(Count()>262144) throw std::invalid_argument("GI volume exceeds 262144 cells");
        Vec3 extent=Math::Sub(config.Maximum,config.Minimum);
        Size={extent.X/config.CountX,extent.Y/config.CountY,extent.Z/config.CountZ};
        for(int axis=0;axis<3;++axis) {
            float bound=std::max(std::abs(Math::Axis(config.Minimum,axis)),std::abs(Math::Axis(config.Maximum,axis)));
            if(bound>1.0e6f||!(Math::Axis(Size,axis)>=std::max(1.0e-4f,bound*4*std::numeric_limits<float>::epsilon()))) throw std::invalid_argument("GI extent is inverted, too large or below float resolution");
        }
        Published=EmptyField(0);
        PublishedCells.resize(Count());
    }
    /// <summary>Returns the exact number of bounded cells.</summary>
    std::size_t Count() const { return static_cast<std::size_t>(Settings.CountX)*Settings.CountY*Settings.CountZ; }
    /// <summary>Converts integer coordinates into X-major storage order.</summary>
    std::size_t Index(int x,int y,int z) const { return static_cast<std::size_t>(x)+Settings.CountX*(static_cast<std::size_t>(y)+Settings.CountY*static_cast<std::size_t>(z)); }
    /// <summary>Returns a voxel center for an X-major index.</summary>
    Vec3 Center(std::size_t index) const {
        unsigned int x=static_cast<unsigned int>(index%Settings.CountX);
        unsigned int y=static_cast<unsigned int>((index/Settings.CountX)%Settings.CountY);
        unsigned int z=static_cast<unsigned int>(index/(Settings.CountX*Settings.CountY));
        return {Settings.Minimum.X+(x+0.5f)*Size.X,Settings.Minimum.Y+(y+0.5f)*Size.Y,Settings.Minimum.Z+(z+0.5f)*Size.Z};
    }
    /// <summary>Allocates an initially dark atlas, marking all probes empty.</summary>
    Field EmptyField(std::uint64_t generation) const {
        Field result{generation,Settings.CountX*Settings.CountY,Settings.CountZ*6,{}};
        result.AtlasRgba.resize(Count()*6*4,0);
        for(std::size_t i=3;i<result.AtlasRgba.size();i+=4) result.AtlasRgba[i]=1;
        return result;
    }
    /// <summary>Returns RGBA base offset for a cell and cardinal lobe in the flattened 2D atlas.</summary>
    std::size_t AtlasIndex(std::size_t cell,unsigned int lobe) const { return (cell+Count()*lobe)*4; }
    /// <summary>Checks a world position against the half-open volume.</summary>
    bool Inside(Vec3 p) const { return p.X>=Settings.Minimum.X&&p.X<Settings.Maximum.X&&p.Y>=Settings.Minimum.Y&&p.Y<Settings.Maximum.Y&&p.Z>=Settings.Minimum.Z&&p.Z<Settings.Maximum.Z; }
    /// <summary>Traverses bounded DDA; solar rays only exempt proven coplanar source coverage, while other rays treat every occupied cell as opaque.</summary>
    int Trace(const std::vector<Cell>& cells,Vec3 origin,Vec3 direction,float distance,const Cell* solarPlane=nullptr) const {
        if(!Inside(origin)) return -1;
        int coordinates[3];
        int steps[3];
        int limits[3]={static_cast<int>(Settings.CountX),static_cast<int>(Settings.CountY),static_cast<int>(Settings.CountZ)};
        float next[3],delta[3];
        for(int a=0;a<3;++a) {
            float o=Math::Axis(origin,a),d=Math::Axis(direction,a),size=Math::Axis(Size,a),minimum=Math::Axis(Settings.Minimum,a);
            coordinates[a]=std::min(limits[a]-1,static_cast<int>(std::floor((o-minimum)/size)));
            steps[a]=d>=0?1:-1;
            if(std::abs(d)<1.0e-20f) next[a]=delta[a]=std::numeric_limits<float>::infinity();
            else {
                float boundary=minimum+(coordinates[a]+(steps[a]>0?1:0))*size;
                next[a]=std::max(0.0f,(boundary-o)/d);
                delta[a]=size/std::abs(d);
            }
        }
        unsigned int maxSteps=Settings.CountX+Settings.CountY+Settings.CountZ+3;
        for(unsigned int iteration=0;iteration<maxSteps;++iteration) {
            std::size_t index=Index(coordinates[0],coordinates[1],coordinates[2]);
            const Cell& cell=cells[index];
            if(cell.Count>0) {
                bool sameSourcePlane=solarPlane!=nullptr&&cell.Coplanar&&Math::Coplanar(solarPlane->PlaneNormal,solarPlane->PlaneOffset,cell.PlaneNormal,cell.PlaneOffset,std::min({Size.X,Size.Y,Size.Z}));
                if(!sameSourcePlane) return static_cast<int>(index);
            }
            int axis=next[0]<=next[1]&&next[0]<=next[2]?0:(next[1]<=next[2]?1:2);
            if(next[axis]>distance) return -1;
            coordinates[axis]+=steps[axis];
            if(coordinates[axis]<0||coordinates[axis]>=limits[axis]) return -1;
            next[axis]+=delta[axis];
        }
        return -1;
    }
    /// <summary>Begins a stable snapshot without restarting an existing generation during continuous motion.</summary>
    bool Begin() {
        if(Pending||Generation==Published.Generation) return false;
        auto next=std::make_unique<Work>();
        next->Generation=Generation;
        next->Light=Light;
        next->Inputs=Inputs;
        next->Input=next->Inputs.begin();
        next->Cells.resize(Count());
        next->Output=EmptyField(Generation);
        Pending=std::move(next);
        return true;
    }
    /// <summary>Initializes clipped candidate bounds for a nondegenerate triangle, returning false when wholly outside.</summary>
    bool Candidates(Work& work,const Triangle& triangle) const {
        int limits[3]={static_cast<int>(Settings.CountX),static_cast<int>(Settings.CountY),static_cast<int>(Settings.CountZ)};
        for(int a=0;a<3;++a) {
            float low=std::min({Math::Axis(triangle.A,a),Math::Axis(triangle.B,a),Math::Axis(triangle.C,a)});
            float high=std::max({Math::Axis(triangle.A,a),Math::Axis(triangle.B,a),Math::Axis(triangle.C,a)});
            float minimum=Math::Axis(Settings.Minimum,a),maximum=Math::Axis(Settings.Maximum,a),size=Math::Axis(Size,a);
            if(high<minimum||low>maximum) return false;
            // Include both cells adjacent to an exactly touching triangle plane.
            work.Bounds[a]=std::max(0,static_cast<int>(std::floor((std::max(low,minimum)-minimum)/size-1.0e-5f)));
            work.Bounds[a+3]=std::min(limits[a]-1,static_cast<int>(std::floor((std::min(high,maximum)-minimum)/size+1.0e-5f)));
            work.Candidate[a]=work.Bounds[a];
        }
        return true;
    }
    /// <summary>Performs one bounded candidate SAT test or one contributor/triangle bookkeeping transition.</summary>
    void Voxelize(Work& work) {
        if(work.Input==work.Inputs.end()) {
            work.Phase=1;work.Input=work.Inputs.begin();work.Item=0;return;
        }
        auto id=work.Input->first;
        const Contributor& contributor=*work.Input->second;
        auto found=work.Coverages.find(id);
        if(found==work.Coverages.end()) {
            auto cached=Cached.find(id);
            if(cached!=Cached.end()&&cached->second->Revision==contributor.Revision) {
                work.Coverages[id]=cached->second;
                work.HitCount+=cached->second->Hits.size();
                if(work.HitCount>4194304) throw std::length_error("GI snapshot exceeds retained coverage budget");
                ++work.Input;return;
            }
            auto coverage=std::make_shared<Coverage>();coverage->Revision=contributor.Revision;
            work.Coverages[id]=coverage;
        }
        if(work.Item==contributor.Triangles.size()) {++work.Input;work.Item=0;return;}
        const Triangle& triangle=contributor.Triangles[work.Item];
        if(!work.CandidateReady) {
            if(!Candidates(work,triangle)) {++work.Item;return;}
            work.CandidateReady=true;
        }
        if(++work.CandidateTests>16777216) throw std::length_error("GI snapshot exceeds candidate-test budget");
        ++Counters.TriangleVoxelTests;
        std::size_t index=Index(work.Candidate[0],work.Candidate[1],work.Candidate[2]);
        if(Math::Intersects(triangle,Center(index),Math::Mul(Size,0.5f))) {
            if(++work.HitCount>4194304) throw std::length_error("GI snapshot exceeds retained coverage budget");
            Vec3 cross=Math::Cross(Math::Sub(triangle.B,triangle.A),Math::Sub(triangle.C,triangle.A));
            float weight=Math::Length(cross);
            Vec3 normal=Math::Mul(cross,1.0f/weight);
            work.Coverages[id]->Hits.push_back({index,normal,Math::Dot(normal,triangle.A),triangle.Albedo,weight});
        }
        if(++work.Candidate[0]>work.Bounds[3]) {
            work.Candidate[0]=work.Bounds[0];
            if(++work.Candidate[1]>work.Bounds[4]) {
                work.Candidate[1]=work.Bounds[1];
                if(++work.Candidate[2]>work.Bounds[5]) {work.CandidateReady=false;++work.Item;}
            }
        }
    }
    /// <summary>Rebuilds aggregate occupancy from retained independent coverage one intersection per unit, avoiding subtraction drift.</summary>
    void Aggregate(Work& work) {
        if(work.Input==work.Inputs.end()) {work.Phase=2;work.Item=0;return;}
        const auto& hits=work.Coverages.at(work.Input->first)->Hits;
        if(work.Item==hits.size()) {++work.Input;work.Item=0;return;}
        const Hit& hit=hits[work.Item++];
        Cell& cell=work.Cells[hit.Cell];
        if(cell.Count==0) {cell.PlaneNormal=hit.Normal;cell.PlaneOffset=hit.PlaneOffset;}
        else if(!Math::Coplanar(cell.PlaneNormal,cell.PlaneOffset,hit.Normal,hit.PlaneOffset,std::min({Size.X,Size.Y,Size.Z}))) cell.Coplanar=false;
        ++cell.Count;
        auto& surface=cell.Surfaces[Math::Lobe(hit.Normal)];
        surface[0]+=hit.Normal.X*hit.Weight;surface[1]+=hit.Normal.Y*hit.Weight;surface[2]+=hit.Normal.Z*hit.Weight;
        surface[3]+=hit.Albedo.X*hit.Weight;surface[4]+=hit.Albedo.Y*hit.Weight;surface[5]+=hit.Albedo.Z*hit.Weight;surface[6]+=hit.Weight;
    }
    /// <summary>Seeds one cell with sun-visible reflected radiance; direct-light rendering remains outside this module.</summary>
    void Illuminate(Work& work) {
        if(work.CellIndex==Count()) {work.Phase=3;work.CellIndex=0;return;}
        Cell& cell=work.Cells[work.CellIndex];
        for(unsigned int lobe=0;lobe<6;++lobe) {
            std::size_t offset=AtlasIndex(work.CellIndex,lobe);
            work.Output.AtlasRgba[offset+3]=cell.Count>0?(cell.Coplanar?0.0f:-1.0f):1.0f;
            auto& surface=cell.Surfaces[lobe];
            if(surface[6]<=0||!cell.Coplanar) continue;
            Vec3 n=Math::Unit({surface[0],surface[1],surface[2]});
            float cosine=std::max(0.0f,Math::Dot(n,work.Light.DirectionToSun));
            if(cosine==0) continue;
            ++Counters.RaysTraced;
            Vec3 center=Center(work.CellIndex);
            Vec3 source=Math::Add(center,Math::Mul(cell.PlaneNormal,cell.PlaneOffset-Math::Dot(cell.PlaneNormal,center)));
            if(Trace(work.Cells,source,work.Light.DirectionToSun,std::numeric_limits<float>::infinity(),&cell)>=0) continue;
            float factor=cosine/(3.14159265358979323846f*surface[6]);
            surface[7]=surface[3]*work.Light.Irradiance.X*factor;
            surface[8]=surface[4]*work.Light.Irradiance.Y*factor;
            surface[9]=surface[5]*work.Light.Irradiance.Z*factor;
        }
        if(cell.Count>0&&cell.Coplanar) {
            Vec3 normal=cell.PlaneNormal;
            float planeOffset=cell.PlaneOffset;
            // Canonicalize the unoriented plane by making its dominant normal component positive.
            if((Math::Lobe(normal)&1u)!=0) {normal=Math::Mul(normal,-1);planeOffset=-planeOffset;}
            std::size_t normalOffset=AtlasIndex(work.CellIndex,0);
            work.Output.AtlasRgba[normalOffset]=normal.X;
            work.Output.AtlasRgba[normalOffset+1]=normal.Y;
            work.Output.AtlasRgba[normalOffset+2]=normal.Z;
            work.Output.AtlasRgba[AtlasIndex(work.CellIndex,1)]=planeOffset;
        }
        ++work.CellIndex;
    }
    /// <summary>Integrates one cosine-distributed ray, whose irradiance weight is pi divided by the sample count.</summary>
    void Integrate(Work& work) {
        if(work.CellIndex==Count()) {work.Phase=4;return;}
        if(work.Cells[work.CellIndex].Count>0) {++work.CellIndex;return;}
        Vec3 direction=Math::Hemisphere(work.Ray,Settings.RaysPerLobe,work.Lobe);
        ++Counters.RaysTraced;
        int hit=Trace(work.Cells,Center(work.CellIndex),direction,std::numeric_limits<float>::infinity());
        if(hit>=0) {
            Vec3 outgoing{};float weight=0;
            for(unsigned int lobe=0;lobe<6;++lobe) {
                const auto& surface=work.Cells[hit].Surfaces[lobe];
                if(surface[6]<=0) continue;
                Vec3 normal=Math::Unit({surface[0],surface[1],surface[2]});
                float facing=std::max(0.0f,-Math::Dot(normal,direction));
                if(facing<=0) continue;
                outgoing=Math::Add(outgoing,Math::Mul({surface[7],surface[8],surface[9]},facing));weight+=facing;
            }
            if(weight>0) {
                outgoing=Math::Mul(outgoing,3.14159265358979323846f/(Settings.RaysPerLobe*weight));
                std::size_t offset=AtlasIndex(work.CellIndex,work.Lobe);
                work.Output.AtlasRgba[offset]+=outgoing.X;work.Output.AtlasRgba[offset+1]+=outgoing.Y;work.Output.AtlasRgba[offset+2]+=outgoing.Z;
            }
        }
        if(++work.Ray==Settings.RaysPerLobe) {work.Ray=0;if(++work.Lobe==6) {work.Lobe=0;++work.CellIndex;}}
    }
    /// <summary>Rejects invalid geometry instead of silently replacing required values.</summary>
    void ValidateTriangle(const Triangle& triangle) const {
        if(!Math::Finite(triangle.A)||!Math::Finite(triangle.B)||!Math::Finite(triangle.C)||!Math::Finite(triangle.Albedo)) throw std::invalid_argument("GI triangle must be finite");
        for(int a=0;a<3;++a) {
            if(std::abs(Math::Axis(triangle.A,a))>1.0e6f||std::abs(Math::Axis(triangle.B,a))>1.0e6f||std::abs(Math::Axis(triangle.C,a))>1.0e6f) throw std::invalid_argument("GI triangle position exceeds float contract");
            if(Math::Axis(triangle.Albedo,a)<0||Math::Axis(triangle.Albedo,a)>1) throw std::invalid_argument("GI albedo must be within zero and one");
        }
        if(Math::Length(Math::Cross(Math::Sub(triangle.B,triangle.A),Math::Sub(triangle.C,triangle.A)))<1.0e-12f) throw std::invalid_argument("GI triangle must have positive area");
    }
};

VoxelGi::VoxelGi(const Config& config):State(std::make_unique<Impl>(config)) {}
VoxelGi::~VoxelGi()=default;
void VoxelGi::SetContributor(std::uint64_t id,std::uint64_t revision,const std::vector<Triangle>& triangles) {
    auto old=State->Inputs.find(id);
    if(old!=State->Inputs.end()&&old->second->Revision==revision) return;
    std::size_t total=triangles.size();
    for(const auto& input:State->Inputs) if(input.first!=id) total+=input.second->Triangles.size();
    if(total>65536) throw std::length_error("GI scene exceeds 65536 triangles");
    for(const Triangle& triangle:triangles) State->ValidateTriangle(triangle);
    auto contributor=std::make_shared<Contributor>();contributor->Revision=revision;contributor->Triangles=triangles;
    State->Inputs[id]=std::move(contributor);++State->Generation;
}
void VoxelGi::RemoveContributor(std::uint64_t id) {if(State->Inputs.erase(id)>0) ++State->Generation;}
void VoxelGi::SetSun(const Sun& sun) {
    if(!Math::Finite(sun.DirectionToSun)||!Math::Finite(sun.Irradiance)||Math::Length(sun.DirectionToSun)<1.0e-12f||Math::Length(sun.DirectionToSun)>1.0e6f) throw std::invalid_argument("GI sun direction must be finite and nonzero");
    for(int a=0;a<3;++a) if(Math::Axis(sun.Irradiance,a)<0||Math::Axis(sun.Irradiance,a)>1.0e6f) throw std::invalid_argument("GI sun irradiance outside finite contract");
    Sun next{Math::Unit(sun.DirectionToSun),sun.Irradiance};
    if(Math::Dot(Math::Sub(next.DirectionToSun,State->Light.DirectionToSun),Math::Sub(next.DirectionToSun,State->Light.DirectionToSun))==0&&Math::Dot(Math::Sub(next.Irradiance,State->Light.Irradiance),Math::Sub(next.Irradiance,State->Light.Irradiance))==0) return;
    State->Light=next;++State->Generation;
}
bool VoxelGi::BeginUpdate() {return State->Begin();}
bool VoxelGi::AdvanceUpdate(std::uint32_t maxWorkItems) {
    if(maxWorkItems==0) throw std::invalid_argument("GI work budget must be positive");
    State->Begin();
    if(!State->Pending) return false;
    try {
        for(std::uint32_t unit=0;unit<maxWorkItems;++unit) {
            Work& work=*State->Pending;
            if(work.Phase==0) State->Voxelize(work);
            else if(work.Phase==1) State->Aggregate(work);
            else if(work.Phase==2) State->Illuminate(work);
            else if(work.Phase==3) State->Integrate(work);
            else {
                State->Published=std::move(work.Output);State->PublishedCells=std::move(work.Cells);State->Cached=std::move(work.Coverages);
                State->Pending.reset();++State->Counters.Publications;return true;
            }
        }
    } catch(...) {
        // Preserve the published field while allowing corrected input to create a fresh snapshot.
        State->Pending.reset();
        throw;
    }
    return false;
}
const Field& VoxelGi::GetField() const {return State->Published;}
Vec3 VoxelGi::SampleIrradiance(Vec3 position,Vec3 normal) const {
    if(!Math::Finite(position)||!Math::Finite(normal)||Math::Length(normal)<1.0e-12f||Math::Length(normal)>1.0e6f) throw std::invalid_argument("GI sample position and normal must be finite and normal nonzero");
    if(!State->Inside(position)) return {0,0,0};
    Vec3 q{(position.X-State->Settings.Minimum.X)/State->Size.X-0.5f,(position.Y-State->Settings.Minimum.Y)/State->Size.Y-0.5f,(position.Z-State->Settings.Minimum.Z)/State->Size.Z-0.5f};
    int base[3]={static_cast<int>(std::floor(q.X)),static_cast<int>(std::floor(q.Y)),static_cast<int>(std::floor(q.Z))};
    float fraction[3]={q.X-base[0],q.Y-base[1],q.Z-base[2]};
    float normalSum=std::abs(normal.X)+std::abs(normal.Y)+std::abs(normal.Z);
    Vec3 value{};float totalWeight=0;
    for(int z=0;z<2;++z) for(int y=0;y<2;++y) for(int x=0;x<2;++x) {
        int cx=base[0]+x,cy=base[1]+y,cz=base[2]+z;
        if(cx<0||cy<0||cz<0||cx>=static_cast<int>(State->Settings.CountX)||cy>=static_cast<int>(State->Settings.CountY)||cz>=static_cast<int>(State->Settings.CountZ)) continue;
        std::size_t index=State->Index(cx,cy,cz);
        if(State->PublishedCells[index].Count>0) continue;
        Vec3 segment=Math::Sub(State->Center(index),position);float length=Math::Length(segment);
        if(length>1.0e-8f&&State->Trace(State->PublishedCells,position,Math::Mul(segment,1/length),length)>=0) continue;
        float weight=(x?fraction[0]:1-fraction[0])*(y?fraction[1]:1-fraction[1])*(z?fraction[2]:1-fraction[2]);
        for(unsigned int lobe=0;lobe<6;++lobe) {
            float facing=std::max(0.0f,Math::Dot(normal,Math::Direction(lobe)))/normalSum;
            std::size_t offset=State->AtlasIndex(index,lobe);
            value=Math::Add(value,Math::Mul({State->Published.AtlasRgba[offset],State->Published.AtlasRgba[offset+1],State->Published.AtlasRgba[offset+2]},weight*facing));
        }
        totalWeight+=weight;
    }
    return totalWeight>0?Math::Mul(value,1/totalWeight):Vec3{0,0,0};
}
Stats VoxelGi::GetStats() const {
    Stats result=State->Counters;result.InputGeneration=State->Generation;result.UpdatePending=State->Pending!=nullptr||State->Published.Generation!=State->Generation;return result;
}
}





