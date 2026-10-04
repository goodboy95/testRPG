"""Read-only NPC active-cost and matched village-matrix comparison.

python scripts/tools/summarize_village_npc.py npc-probe/results.json npc-matrix/results.json baseline-matrix/results.json
"""
import json
import math
import statistics
import sys
from pathlib import Path


def p95(values):
    return sorted(values)[math.ceil(len(values) * .95) - 1] if values else None


def matrix(raw, width):
    samples = [s for s in raw['samples'] if s['width'] == width]
    gpu = [s['gpuMs'] for s in samples if s['gpuMs'] is not None]
    frames = [f['frameMs'] for f in raw['frames'] if f.get('width') == width and f['phase'] in ('settled', 'moving-orbit')]
    return dict(samples=len(samples), peakPrimitives=max(s['primitives'] for s in samples),
                peakDraws=max(s['drawCalls'] for s in samples), gpuMeanMs=statistics.mean(gpu) if gpu else None,
                gpuP95Ms=p95(gpu), frameP95Ms=p95(frames), averageFps=1000 / statistics.mean(frames),
                minimumInstantFps=1000 / max(frames), peakVideoMemoryMiB=max(s['videoMemory'] for s in samples) / 1024**2)


def summarize(probe, npcs, baseline):
    active = json.loads(probe.read_text(encoding='utf-8'))
    actual = json.loads(npcs.read_text(encoding='utf-8'))
    before = json.loads(baseline.read_text(encoding='utf-8'))
    rows = []
    for width in (1280, 1600, 1920):
        samples = [s for s in active['samples'] if s['width'] == width]
        cost = p95([s['updateMs'] for s in samples])
        gpu = [s['gpuMs'] for s in samples if s['gpuMs'] is not None]
        a, b = matrix(actual, width), matrix(before, width)
        rows.append(dict(width=width, activeSamples=len(samples), npcUpdateP95Ms=cost, npcCpuTargetMet=cost <= .5,
                         activeGpuMeanMs=statistics.mean(gpu) if gpu else None, activeGpuP95Ms=p95(gpu),
                         withNpcs=a, withoutNpcs=b,
                         peakPrimitiveDifference=a['peakPrimitives']-b['peakPrimitives'],
                         peakDrawDifference=a['peakDraws']-b['peakDraws']))
    return dict(sources=[str(probe), str(npcs), str(baseline)], adapter=actual['adapter'],
                functionalChecks=len(active['checks']), npcMatrixChecks=len(actual['checks']),
                baselineMatrixChecks=len(before['checks']), resolutions=rows,
                targetHardwareAcceptance='pending', humanAcceptance='pending',
                timingScope='NPC instrumentation is active update cost; matrix freezes locomotion for repeatable views. Startup, loading and captures are separate.')


if __name__ == '__main__':
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    print(json.dumps(summarize(*(Path(p) for p in sys.argv[1:])), indent=2, ensure_ascii=False))
