# spur.py <prefix> [offset kHz]: level above the median floor at an offset, 1 MHz span, each source
import sys,numpy as np
from an import sources
pre=sys.argv[1]; off=float(sys.argv[2]) if len(sys.argv)>2 else -451
for n,(F,t) in sources(pre).items():
    lt=F.mean(0); N=len(lt); x=(np.arange(N)/(N-1)-0.5)*1000
    w=np.abs(x-off)<=3; print(f'{n:9s} {lt[w].max()-np.median(lt):6.1f} dB')
