#pragma once
#include "helgi/Triangle.h"
#include <algorithm>
#include <cmath>
#include <limits>
namespace helgi {
/// <summary>Fixed-float geometry operations shared by voxelization and transport.</summary>
class Math {
public:
    /// <summary>Adds vectors component by component.</summary>
    static Vec3 Add(Vec3 a, Vec3 b) { return {a.X+b.X,a.Y+b.Y,a.Z+b.Z}; }
    /// <summary>Subtracts vectors component by component.</summary>
    static Vec3 Sub(Vec3 a, Vec3 b) { return {a.X-b.X,a.Y-b.Y,a.Z-b.Z}; }
    /// <summary>Scales each component by a scalar.</summary>
    static Vec3 Mul(Vec3 a, float b) { return {a.X*b,a.Y*b,a.Z*b}; }
    /// <summary>Computes the Euclidean inner product.</summary>
    static float Dot(Vec3 a, Vec3 b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
    /// <summary>Computes an oriented cross product.</summary>
    static Vec3 Cross(Vec3 a, Vec3 b) { return {a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X}; }
    /// <summary>Computes Euclidean length within the validated numeric range.</summary>
    static float Length(Vec3 a) { return std::sqrt(Dot(a,a)); }
    /// <summary>Normalizes a nonzero vector; callers validate length before use.</summary>
    static Vec3 Unit(Vec3 a) { return Mul(a,1.0f/Length(a)); }
    /// <summary>Checks all three components for finite values.</summary>
    static bool Finite(Vec3 a) { return std::isfinite(a.X)&&std::isfinite(a.Y)&&std::isfinite(a.Z); }
    /// <summary>Reads an axis without relying on structure aliasing or packing.</summary>
    static float Axis(Vec3 a, int axis) { return axis==0?a.X:(axis==1?a.Y:a.Z); }
    /// <summary>Tests a single separating axis of a triangle against a centered box.</summary>
    static bool Separated(Vec3 axis, Vec3 a, Vec3 b, Vec3 c, Vec3 half) {
        float p=Dot(a,axis),q=Dot(b,axis),r=Dot(c,axis);
        float extent=half.X*std::abs(axis.X)+half.Y*std::abs(axis.Y)+half.Z*std::abs(axis.Z);
        float epsilon=1.0e-6f*(extent+std::abs(p)+std::abs(q)+std::abs(r)+1.0e-12f);
        return std::min({p,q,r})>extent+epsilon || std::max({p,q,r}) < -extent-epsilon;
    }
    /// <summary>Runs the full thirteen-axis triangle-box SAT; touching boundaries are occupied conservatively.</summary>
    static bool Intersects(const Triangle& triangle, Vec3 center, Vec3 half) {
        Vec3 a=Sub(triangle.A,center),b=Sub(triangle.B,center),c=Sub(triangle.C,center);
        Vec3 edges[3]={Sub(b,a),Sub(c,b),Sub(a,c)};
        Vec3 axes[3]={{1,0,0},{0,1,0},{0,0,1}};
        for(Vec3 axis:axes) if(Separated(axis,a,b,c,half)) return false;
        if(Separated(Cross(edges[0],edges[1]),a,b,c,half)) return false;
        for(Vec3 edge:edges) for(Vec3 axis:axes) if(Separated(Cross(edge,axis),a,b,c,half)) return false;
        return true;
    }
    /// <summary>Recognizes the same unoriented plane within float precision, without exempting parallel displaced occluders.</summary>
    static bool Coplanar(Vec3 firstNormal,float firstOffset,Vec3 secondNormal,float secondOffset,float voxelScale) {
        float alignment=Dot(firstNormal,secondNormal);
        if(alignment<0) {secondNormal=Mul(secondNormal,-1);secondOffset=-secondOffset;}
        Vec3 difference=Sub(firstNormal,secondNormal);
        float normalEpsilon=8*std::numeric_limits<float>::epsilon();
        if(std::max({std::abs(difference.X),std::abs(difference.Y),std::abs(difference.Z)})>normalEpsilon) return false;
        float magnitude=std::max({std::abs(firstOffset),std::abs(secondOffset),1.0e-3f});
        float epsilon=2*std::numeric_limits<float>::epsilon()*magnitude+voxelScale*1.0e-6f;
        return std::abs(firstOffset-secondOffset)<=epsilon;
    }    /// <summary>Maps a normal to its dominant signed cardinal hemisphere.</summary>
    static unsigned int Lobe(Vec3 n) {
        if(std::abs(n.X)>=std::abs(n.Y)&&std::abs(n.X)>=std::abs(n.Z)) return n.X>=0?0:1;
        if(std::abs(n.Y)>=std::abs(n.Z)) return n.Y>=0?2:3;
        return n.Z>=0?4:5;
    }
    /// <summary>Returns cardinal lobe directions in the public atlas order.</summary>
    static Vec3 Direction(unsigned int lobe) {
        const Vec3 directions[6]={{1,0,0},{-1,0,0},{0,1,0},{0,-1,0},{0,0,1},{0,0,-1}};
        return directions[lobe];
    }
    /// <summary>Builds a deterministic cosine-weighted hemisphere sample around a cardinal lobe.</summary>
    static Vec3 Hemisphere(unsigned int ray, unsigned int count, unsigned int lobe) {
        float u=(static_cast<float>(ray)+0.5f)/static_cast<float>(count);
        float phi=2.399963229728653f*static_cast<float>(ray);
        float radius=std::sqrt(u),height=std::sqrt(1-u);
        Vec3 n=Direction(lobe),t=std::abs(n.Y)<0.5f?Vec3{0,1,0}:Vec3{1,0,0};
        Vec3 b=Cross(n,t);
        return Add(Mul(n,height),Add(Mul(t,radius*std::cos(phi)),Mul(b,radius*std::sin(phi))));
    }
};
}


