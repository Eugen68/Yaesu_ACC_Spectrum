"""Probability of detecting weak signals at a fixed 5% false-alarm rate, per source.

Signals are chosen from one pair of sources and measured in the other pair, so the
noise that picked them is independent of the noise they are measured in:
  pick on ACC Sub + RSP1  -> measure ACC Main (raw, Refine 3, Refine 5) and RSP1B
  pick on ACC Main + RSP1B -> measure ACC Sub (Refine 3) and RSP1
For each measured source the statistic in a window round a frequency is
  s = (highest point in the window - frame floor median) / frame floor spread,
and its threshold is the 95th percentile of s at signal-free places in the same
source's own frames (5% false alarms). The window is the same width in Hz for every
source. Detection = share of (signal, frame) pairs with s above the threshold.
Usage: [MODE=middle|noskirt] detect.py <prefix> <span Hz>   -> one JSON line
"""
import sys,json,os,numpy as np
MODE=os.environ.get('MODE','all')
from an import sources, ema
pre=sys.argv[1]; span=float(sys.argv[2]); src=sources(pre)
REF={'ACC Main':{'raw':1,'R1':.6,'R3':.3,'R5':.12},'ACC Sub':{'R3':.3},'RSP1B':{'':None},'RSP1':{'':None}}
def axis(n): return (np.arange(n)-(395 if n==791 else n//2))*span/n
lo,hi=-0.45*span,0.45*span
step=span/791
grid=np.arange(lo,hi,step/2)
# frequency offset of each source against RSP1B, from long-term means
fine=np.arange(lo,hi,span/20000)
def lt(n): F,_=src[n]; return np.interp(fine,axis(F.shape[1]),F.mean(0))
r=lt('RSP1B'); r=r-r.mean(); off={}
for n in src:
    x=lt(n); x=x-x.mean(); L=400
    c=[np.dot(x[L+l:len(x)-L+l],r[L:len(r)-L]) for l in range(-L,L+1)]
    off[n]=(int(np.argmax(c))-L)*span/20000
W=1.5*max(span/src[n][0].shape[1] for n in src)          # same window in Hz for all
def frames(n,w):
    F,t=src[n]; G=F if w in (None,1) else ema(F,w); return G,t,axis(F.shape[1])-off[n]
def block_mean_on_grid(n,m):
    F,t=src[n]; return np.interp(grid,axis(F.shape[1])-off[n],F[m].mean(0))
def peaks(mean):
    med=np.median(mean); fl=mean[mean<=med]; sd=(np.percentile(fl,50)-np.percentile(fl,16))
    sd=max(sd,1e-6); z=(mean-med)/sd; k=int(max(1,W/(step/2)))
    pk=[i for i in range(k,len(z)-k) if z[i]==z[i-k:i+k+1].max()]
    return z,pk
def pick(a,b,m_a,m_b):
    za,pa=peaks(block_mean_on_grid(a,m_a)); zb,pb=peaks(block_mean_on_grid(b,m_b))
    busy=(za>3)|(zb>3)
    sel=[]
    for i in pa:
        if za[i]<6: continue
        j=[j for j in pb if abs(j-i)*step/2<=W and zb[j]>=6]
        if not j: continue
        fa=src[b][0][m_b].mean(0); 
        # weak to moderate: no more than 20 dB above the floor in the RSP's block mean
        rsp=block_mean_on_grid(b,m_b) if b.startswith('RSP') else block_mean_on_grid(a,m_a)
        if rsp[i]-np.median(rsp)>20: continue
        if MODE=='middle' and abs(grid[i])>0.225*span: continue
        if MODE=='noskirt':
            strong=np.where(rsp-np.median(rsp)>25)[0]
            if strong.size and np.min(np.abs(strong-i))*step/2<=1000: continue
        sel.append(grid[i])
    # signal-free places: grid points at least 2W from anything busy in either picker
    bi=np.where(busy)[0]; free=[]
    for i in range(0,len(grid),max(1,int(W/(step/2)))):
        if bi.size==0 or np.min(np.abs(bi-i))*step/2>=2*W: free.append(grid[i])
    return sel,free
def stat(fr,ax,fp,f):
    med=np.median(fr[fp]); sp=(np.percentile(fr[fp],84)-np.percentile(fr[fp],16))/2
    return (fr[np.abs(ax-f)<=W].max()-med)/sp
out={'prefix':pre,'span':span,'window_hz':W,'offsets':off}
T=src['RSP1B'][1]; blocks=np.arange(T[0],T[-1]-4.9,5.0)
for pickers,measured in ((('ACC Sub','RSP1'),('ACC Main','RSP1B')),(('ACC Main','RSP1B'),('ACC Sub','RSP1'))):
    sig={};noise={}; nsig=0
    prepared={(n,lab):frames(n,w) for n in measured for lab,w in REF[n].items()}
    for b0 in blocks:
        m=lambda n:(src[n][1]>=b0)&(src[n][1]<b0+5)
        sel,free=pick(pickers[0],pickers[1],m(pickers[0]),m(pickers[1]))
        nsig+=len(sel)
        for (n,lab),(G,t,ax) in prepared.items():
            mm=(t>=b0)&(t<b0+5); ltm=G.mean(0); fp=ltm<=np.percentile(ltm,50)
            for fr in G[mm]:
                sig.setdefault((n,lab),[]).extend(stat(fr,ax,fp,f) for f in sel)
                noise.setdefault((n,lab),[]).extend(stat(fr,ax,fp,f) for f in free[::max(1,len(free)//40)])
    for k in prepared:
        s=np.array(sig.get(k,[])); z=np.array(noise.get(k,[]))
        if len(z)<50: continue
        th=np.percentile(z,95)
        out[(k[0]+(' '+k[1] if k[1] else '')).strip()]=dict(pd=float((s>th).mean()) if len(s) else None, n=int(len(s)))
    out['signals_'+pickers[0].split()[-1]]=nsig
print(json.dumps(out))
