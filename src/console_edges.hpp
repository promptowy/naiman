#pragma once
#include <algorithm>
#include <array>
#include <cmath>

// Seven scanlines through the picture and two just inside its upper/lower edges.
// Use only a narrow, symmetric, stable border plus evidence of a dark title strip.
struct EdgeTrim { int x=0,y=0,w=0,h=0; };
inline EdgeTrim findConsoleEdges(const unsigned char* pixels,unsigned pitch,int w,int h){
    EdgeTrim original{0,0,w,h};
    if(w<200||h<120)return original;
    auto lit=[](const unsigned char* p){return std::max({p[0],p[1],p[2]})>11;};
    std::array<int,7> left{},right{};
    const int limit=w/22;
    for(int i=0;i<7;i++){
        auto row=pixels+i*pitch;
        while(left[i]<limit&&!lit(row+left[i]*4))left[i]++;
        while(right[i]<limit&&!lit(row+(w-1-right[i])*4))right[i]++;
    }
    auto med=[](std::array<int,7> a){std::sort(a.begin(),a.end());return a[3];};
    const int l=med(left),r=med(right);
    if(l<3||r<3||l>=limit||r>=limit||std::abs(l-r)>3)return original;
    int votes=0;for(int i=0;i<7;i++)if(std::abs(left[i]-l)<=3&&std::abs(right[i]-r)<=3)votes++;
    if(votes<5)return original;
    int vw=w-l-r,vh=(vw*9+8)/16;
    int excess=h-vh;if(excess<3||excess>h/14)return original;
    auto brightness=[&](int line){auto row=pixels+line*pitch;int count=0,n=0;
        for(int x=w/8;x<w-w/8;x+=3){count+=lit(row+x*4);n++;}return count/(double)n;};
    double top=brightness(7),bottom=brightness(8);
    if(top<.045 && bottom>.11)return {l,excess,vw,vh};
    if(bottom<.045 && top>.11)return {l,0,vw,vh};
    return original;
}
