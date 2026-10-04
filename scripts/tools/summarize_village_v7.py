"""Summarize recorded Godot matrices; no engine or third-party dependencies.

python scripts/tools/summarize_village_v7.py outputs/.../results.json [baseline/results.json]
Capture frames are reported separately and never included in gameplay FPS.
"""
import json
import math
import statistics
import sys
from pathlib import Path


def percentile(values, fraction):
    return sorted(values)[max(0, math.ceil(len(values) * fraction) - 1)]


def timing(values):
    if not values:
        return None
    mean = statistics.mean(values)
    return dict(frames=len(values), meanMs=mean, p95Ms=percentile(values, .95),
                maxMs=max(values), averageFps=1000 / mean, minimumInstantFps=1000 / max(values))


def summarize(path):
    raw = json.loads(path.read_text(encoding='utf-8'))
    result = dict(source=str(path), baseline=raw['baseline'], adapter=raw['adapter'],
                  renderer=raw['renderer'], loadMs=raw['loadMs'], checks=len(raw['checks']), resolutions=[])
    result['profile'] = 'v6' if raw['baseline'] else raw['samples'][0]['stream'].get('profile', 'integrated')
    for width in sorted({s['width'] for s in raw['samples']}):
        samples = [s for s in raw['samples'] if s['width'] == width]
        frames = [f for f in raw['frames'] if f.get('width') == width]
        gpu = [s['gpuMs'] for s in samples if s['gpuMs'] is not None]
        row = dict(width=width, height=samples[0]['height'], samples=len(samples),
                   maximumPrimitives=max(s['primitives'] for s in samples),
                   maximumDrawCalls=max(s['drawCalls'] for s in samples),
                   maximumRenderObjects=max(s['objects'] for s in samples),
                   maximumVideoMemoryMiB=max(s['videoMemory'] for s in samples) / 1024 ** 2,
                   gpuMeanMs=statistics.mean(gpu) if gpu else None,
                   gpuP95Ms=percentile(gpu, .95) if gpu else None,
                   gameplay=timing([f['frameMs'] for f in frames if f['phase'] != 'capture']),
                   phases={phase: timing([f['frameMs'] for f in frames if f['phase'] == phase])
                           for phase in sorted({f['phase'] for f in frames})})
        if not raw['baseline']:
            row['sceneryTiers'] = {}
            for tier, limit in [('near', 220000), ('wide', 300000)]:
                selected = [s for s in samples if s['stream']['budgetTier'] == tier]
                row['sceneryTiers'][tier] = dict(samples=len(selected),
                    maximumPrimitives=max((s['primitives'] for s in selected), default=0),
                    triangleTargetFailures=sum(s['primitives'] > s['stream'].get('frameBudget', {}).get('Triangles', limit) for s in selected),
                    drawTargetFailures=sum(s['drawCalls'] > s['stream'].get('frameBudget', {}).get('Batches', 24 if tier == 'near' else 32) for s in selected),
                    maximumSelectedTriangles=max((s['stream']['scenery']['Triangles'] for s in selected), default=0),
                    maximumSelectedMeshes=max((s['stream']['scenery']['Meshes'] for s in selected), default=0),
                    maximumSelectedSurfaces=max((s['stream']['scenery']['Batches'] for s in selected), default=0))
        result['resolutions'].append(row)
    result['allPhases'] = {phase: timing([f['frameMs'] for f in raw['frames'] if f['phase'] == phase])
                          for phase in sorted({f['phase'] for f in raw['frames']})}
    result['maximumControllerMs'] = max((f['controllerMs'] or 0 for f in raw['frames']), default=0)
    result['maximumFramePrimitives'] = max((f['primitives'] for f in raw['frames']), default=0)
    if raw['frames'] and 'interior' in raw['frames'][0]:
        result['maximumOutdoorFramePrimitives'] = max((f['primitives'] for f in raw['frames'] if not f['interior']), default=0)
    return result


if __name__ == '__main__':
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    print(json.dumps([summarize(Path(p)) for p in sys.argv[1:]], indent=2, ensure_ascii=False))
