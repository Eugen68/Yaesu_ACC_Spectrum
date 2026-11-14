# clip.py <prefix>...: where the ACC floor sits on the 0-255 scale. Share of points at
# step 0, floor percentiles, rows touching 255. Used to find the scope LEVEL clipping.
import sys,glob,os,numpy as np,load
D=os.environ.get('ACC_COMPARE_DATA','data')
for pre in sys.argv[1:]:
    for p in sorted(glob.glob(f'{D}/{pre}-acc.bin')):
        t,rx=load.load_acc(p); name=os.path.basename(p)[:-8]; out=[]
        for k in ('main','sub'):
            L=rx[k]['lv'][:,30:821].astype(int); m=L.mean(0); fl=L[:,m<=np.median(m)]
            out.append(f"{k}: floor med {np.median(fl):5.1f} p5 {np.percentile(fl,5):5.1f} p1 {np.percentile(fl,1):5.1f} zero {100*(L==0).mean():5.2f}% top-rows {100*(L==255).any(1).mean():5.1f}% max {L.max()}")
        print(f"{name:14s}", ' | '.join(out))
