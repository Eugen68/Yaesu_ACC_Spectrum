"""Tables for the write-up from res/*.json. Usage: summ.py <res dir>"""
import sys,json,glob,os,numpy as np
R={}
for f in glob.glob(os.path.join(sys.argv[1],os.environ.get('RUNS','r')+'[123]_*.json')):
    d=json.load(open(f)); run,band,sp=d['prefix'].split('_'); R.setdefault((band,int(sp)),[]).append(d)
SP=['1k','2k','5k','10k','20k','50k','100k','200k','500k','1M']; BANDS=['160','bc','20','10']
def fmt(v): return '–' if v is None or (isinstance(v,float) and np.isnan(v)) else f'{v:.2f}'
def rng(xs):
    xs=[x for x in xs if x is not None and not np.isnan(x)]
    if not xs: return '–'
    return f'{np.mean(xs):.2f} ({min(xs):.2f}–{max(xs):.2f})' if len(xs)>1 else f'{xs[0]:.2f}'
print('## Clarity ratio, ACC Main ÷ RSP1B, same capture. mean (min–max) over runs. * = no signal 10 dB above the long-term floor')
for lab in ('R0','R1','R3','R5'):
    print(f'\n### Refine {lab[1]}\n\n| span | '+' | '.join(BANDS)+' |\n|---|'+'---|'*len(BANDS))
    for s in range(10):
        row=[]
        for b in BANDS:
            L=R.get((b,s),[]); xs=[d['ACC Main '+lab]['clarity']/d['RSP1B']['clarity'] for d in L]
            flag='*' if L and np.median([d['ltpeak_RSP1B'] for d in L])<10 else ''
            row.append(rng(xs)+flag)
        print(f'| {SP[s]} | '+' | '.join(row)+' |')
print('\n## RSP1 ÷ RSP1B clarity\n\n| span | '+' | '.join(BANDS)+' |\n|---|'+'---|'*len(BANDS))
for s in range(10):
    print(f'| {SP[s]} | '+' | '.join(rng([d['RSP1']['clarity']/d['RSP1B']['clarity'] for d in R.get((b,s),[])]) for b in BANDS)+' |')
print('\n## ACC Sub R3 ÷ ACC Main R3 clarity\n\n| span | '+' | '.join(BANDS)+' |\n|---|'+'---|'*len(BANDS))
for s in range(10):
    print(f'| {SP[s]} | '+' | '.join(rng([d['ACC Sub R3']['clarity']/d['ACC Main R3']['clarity'] for d in R.get((b,s),[])]) for b in BANDS)+' |')
print('\n## Detection at 5% false alarms, pooled over runs (signals tested). Main pair: ACC Main R0/R3/R5, RSP1B')
print('\n| span | band | signals | R0 | R1 | R3 | R5 | RSP1B | Sub R3 | RSP1 |\n|---|---|---|---|---|---|---|---|---|---|')
for s in range(10):
    for b in BANDS:
        L=[d['detect'] for d in R.get((b,s),[]) if d.get('detect')]
        if not L: continue
        def pool(k):
            n=sum(x[k]['n'] for x in L if x.get(k) and x[k]['pd'] is not None)
            return (sum(x[k]['pd']*x[k]['n'] for x in L if x.get(k) and x[k]['pd'] is not None)/n) if n else None
        ns=sum(x.get('signals_Sub',0) for x in L)
        print(f'| {SP[s]} | {b} | {ns} | '+' | '.join(fmt(pool(k)) for k in ('ACC Main raw','ACC Main R1','ACC Main R3','ACC Main R5','RSP1B','ACC Sub R3','RSP1'))+' |')
print('\n## Strongest steady signal, dB over median of long-term mean (RSP1B), per band/span, mean over runs')
print('\n| span | '+' | '.join(BANDS)+' |\n|---|'+'---|'*len(BANDS))
for s in range(10):
    print(f'| {SP[s]} | '+' | '.join(f"{np.mean([d['ltpeak_RSP1B'] for d in R[(b,s)]]):.0f}" if (b,s) in R else '–' for b in BANDS)+' |')
print('\n## Floor spread dB (ACC dB scale approx), mean over runs: R0 / R3 / RSP1B')
print('\n| span | '+' | '.join(BANDS)+' |\n|---|'+'---|'*len(BANDS))
for s in range(10):
    print(f'| {SP[s]} | '+' | '.join('/'.join(f"{np.mean([d[k]['spread'] for d in R[(b,s)]]):.1f}" for k in ('ACC Main R0','ACC Main R3','RSP1B')) if (b,s) in R else '–' for b in BANDS)+' |')
print('\n## Raw clarity R3 and RSP1B, mean over runs')
print('\n| span | '+' | '.join(BANDS)+' |\n|---|'+'---|'*len(BANDS))
for s in range(10):
    print(f'| {SP[s]} | '+' | '.join('/'.join(f"{np.mean([d[k]['clarity'] for d in R[(b,s)]]):.1f}" for k in ('ACC Main R3','RSP1B')) if (b,s) in R else '–' for b in BANDS)+' |')
