"""Shared offline interpretation of VillageLayout3D.Data, independent of Blender.

Ground samples are verified against actual GLB vertices by C# tests, so a drift
between this implementation and the runtime fails the content contract.
"""
from __future__ import annotations
import json
import math
from pathlib import Path
import re

LAYOUT=Path('scripts/presentation/VillageLayout3D.cs')

def read(root):
    text=(root/LAYOUT).read_text('utf-8')
    return json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";',text,re.S)[1])

def base(x,z):return (40-z)*.045
def stream(z):return 49+4*math.sin(z/38)
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)

def local(b,x,z):
    a=math.radians(b['yaw']);x-=b['position'][0];z-=b['position'][2]
    return x*math.cos(a)-z*math.sin(a),x*math.sin(a)+z*math.cos(a)

def world(b,x,z):
    a=math.radians(b['yaw'])
    return b['position'][0]+x*math.cos(a)+z*math.sin(a),b['position'][2]-x*math.sin(a)+z*math.cos(a)

def pad(b):
    if b['kind']=='home':return -7.5,-6,15,18
    w,_,d=b['size'];return -w/2-1.5,-d/2-1,w+3,d+7

def outside(p,r):
    x,z=p;a,b,w,d=r;return max(a-x,x-a-w,b-z,z-b-d)

def ground(v,x,z):
    height=base(x,z)-2.6*(1-smooth((abs(x-stream(z))-4.5)/4))
    for b in v['buildings']:
        dist=outside(local(b,x,z),pad(b))
        if dist<3:height+=(b['position'][1]-height)*(1-smooth(dist/3))
    for b in v['bridges']:
        dist=abs(x-stream(b['z']))
        if abs(z-b['z'])>b['width']/2+1 or dist<b['halfSpan']-1 or dist>b['halfSpan']+2.5:continue
        height+=(base(0,b['z'])+.06-height)*smooth((b['halfSpan']+2.5-dist)/1.5)
    a,b,w,d=v['bounds'];edge=min(x-a,a+w-x,z-b,b+d-z)
    height+=4.5*(1-smooth(edge/6))
    return height

def triangulated(v,x,z):
    g=v['grid'];x0=math.floor((x-v['bounds'][0])/g)*g+v['bounds'][0];z0=math.floor((z-v['bounds'][1])/g)*g+v['bounds'][1]
    u=(x-x0)/g;t=(z-z0)/g;a=ground(v,x0,z0);b=ground(v,x0+g,z0);c=ground(v,x0,z0+g);d=ground(v,x0+g,z0+g)
    return a+(b-a)*u+(c-a)*t if u+t<=1 else d+(c-d)*(1-u)+(b-d)*(1-t)

def on_bridge(v,x,z,margin=0):
    return next((b for b in v['bridges'] if abs(x-stream(b['z']))<=b['halfSpan']-margin and abs(z-b['z'])<=b['width']/2-margin),None)

def walk(v,x,z):
    b=on_bridge(v,x,z);return base(0,b['z'])+.06 if b else triangulated(v,x,z)

def segment_distance(p,a,b):
    dx=b[0]-a[0];dz=b[1]-a[1];dd=dx*dx+dz*dz
    t=0 if dd==0 else max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/dd))
    return math.hypot(p[0]-a[0]-dx*t,p[1]-a[1]-dz*t)

def trees(v):
    result=[]
    for ix in range(16):
        for iz in range(15):
            if (ix+iz)%3==0:continue
            h=(ix*7387+iz*1933+617)%997
            x=-94+ix*12+(h%7-3)*.6;z=-101+iz*13+(h%11-5)*.5
            if abs(x-stream(z))<14 or (x>5 and z>56) or (x>5 and z< -78):continue
            if any(outside(local(b,x,z),pad(b))<5 for b in v['buildings']):continue
            if any(segment_distance((x,z),a,b)<r['width']/2+3.5 for r in v['roads'] for a,b in zip(r['points'],r['points'][1:])):continue
            if any(math.hypot(x-l['position'][0],z-l['position'][2])<7 for l in v['landmarks']):continue
            result.append((x,z,5.5+h%5*.55,h%3))
    return result

def route_length(r):return sum(math.dist(a,b) for a,b in zip(r['points'],r['points'][1:]))
