// Symmetric block matching on mipmapped luma. Vectors point from old to new.
static const char shaders[] = R"NAIMAN(
Texture2D<float4> oldFrame : register(t0);
Texture2D<float4> newFrame : register(t1);
Texture2D<float4> vectors : register(t2);
SamplerState linearClamp : register(s0);
cbuffer Params : register(b0) { float2 pixel; float2 unused; };
struct V { float4 p : SV_Position; float2 uv : TEXCOORD; };
V vs(uint id : SV_VertexID) {
    V o;
    o.uv = float2((id << 1) & 2, id & 2);
    o.p = float4(o.uv * float2(2,-2) + float2(-1,1),0,1);
    return o;
}
float y(float3 c) { return dot(c, float3(.299,.587,.114)); }
float4 decodeNV12(V i) : SV_Target {
    uint w, packedHeight;
    oldFrame.GetDimensions(w, packedHeight);
    uint h = packedHeight * 2 / 3;
    int2 pos = int2(i.p.xy);
    int2 uv = int2(pos.x & ~1, h + pos.y / 2);
    float l = (oldFrame.Load(int3(pos, 0)).r * 255 - 16) / 219;
    float u = (oldFrame.Load(int3(uv, 0)).r * 255 - 128) / 224;
    float v = (oldFrame.Load(int3(uv + int2(1,0), 0)).r * 255 - 128) / 224;
    float3 rgb = h > 576 ? float3(l + 1.5748*v,l - .187324*u - .468124*v,l + 1.8556*u)
                         : float3(l + 1.402*v,l - .344136*u - .714136*v,l + 1.772*u);
    return float4(saturate(rgb),1);
}
float err(float2 uv, float2 delta, float level) {
    float2 d = delta * pixel * .5;
    float2 stepUV = pixel * exp2(level) * 3;
    float total = 0;
    [unroll] for (int i=0; i<5; i++) {
        float2 off = i==0 ? 0 : (i==1 ? float2(1,0) : (i==2 ? float2(-1,0) : (i==3 ? float2(0,1) : float2(0,-1))));
        float2 q=uv+off*stepUV;
        total+=abs(y(oldFrame.SampleLevel(linearClamp,q-d,level).rgb)-y(newFrame.SampleLevel(linearClamp,q+d,level).rgb));
    }
    return total*.2;
}
float4 estimate(V i) : SV_Target {
    float2 v=0;
    [unroll] for(int level=3; level>=0; level--) {
        float scale=exp2((float)level);
        float2 center=v;
        float best=err(i.uv,v,level)+length(v)*.00014;
        [unroll] for(int j=-1;j<=1;j++) [unroll] for(int k=-1;k<=1;k++) {
            if(j==0 && k==0) continue;
            float2 test=center+float2(j,k)*scale*2;
            float score=err(i.uv,test,level)+length(test)*.00014;
            if(score<best) {best=score;v=test;}
        }
    }
    return float4(v,0,1);
}
float4 midpoint(V i) : SV_Target {
    float2 d=vectors.SampleLevel(linearClamp,i.uv,0).xy*pixel*.5;
    float3 a=oldFrame.SampleLevel(linearClamp,i.uv-d,0).rgb;
    float3 b=newFrame.SampleLevel(linearClamp,i.uv+d,0).rgb;
    float mismatch=max(abs(a.r-b.r),max(abs(a.g-b.g),abs(a.b-b.b)));
    // Reuse a real sample at boundaries where the two motion estimates disagree.
    float3 fallback=newFrame.SampleLevel(linearClamp,i.uv,0).rgb;
    return float4(lerp((a+b)*.5,fallback,smoothstep(.09,.27,mismatch)),1);
}
float4 copyNew(V i) : SV_Target { return float4(newFrame.SampleLevel(linearClamp,i.uv,0).rgb,1); }
float4 signature(V i) : SV_Target {
    return float4(newFrame.SampleLevel(linearClamp,i.uv,0).rgb,1);
}
)NAIMAN";
