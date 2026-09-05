# 梦境洋房概念评审 v2：住宅氛围增强

- 状态：已采用为洋房 v1 建模参考；原始评审稿仍保留在 `work/`
- 工具：Codex 内置图像生成
- 模式：`precise-object-edit`
- 编辑目标：`manor-interior-v1.png`（作为 Image 1）
- 沿用素材：`manor-exterior-v1.png`、`manor-floor-plan-v1.png`
- 后处理：Pillow 确定性排版、缩放与三联板拼装；未修改生成图的建筑几何

## 精确编辑提示词

```text
Use case: precise-object-edit
Asset type: revised interior gameplay-view concept for a 3D game environment
Input image: Image 1 is the edit target
Primary request: add restrained residential details so this reads unmistakably as the central hall of an old private English Arts and Crafts country manor rather than an institutional gallery
Add only these elements:
- one cold, unlit Arts and Crafts fireplace or shallow inglenook on a genuinely blank section of the near-left foyer wall, with dark oak mantle and muted handmade tile surround; do not replace or cover any existing door
- one long, worn low-pile runner rug centered along the hall, in faded rust, deep teal, and muted cream, stopping clear of the front double door and all room-door thresholds
- one narrow dark-oak console table against a blank wall, with a ceramic vase, a small closed book, and no keys
- one modest upholstered oak bench near the entrance foyer, placed fully outside the movement path
- two or three faded rural landscape paintings in simple dark-wood frames, no portraits and no readable text
- a small brass umbrella stand and subtle lived-in scuffs, while keeping the house sealed and dusty
Composition and architecture invariants: preserve the exact camera angle, FOV, perspective, room proportions, 5.6-metre apparent hall width, 18-metre apparent length, 4.2-metre ceiling, ceiling beams, floor geometry, lighting direction, closed double-leaf front door and its sidelights, every existing functional room door, and the two solid decorative future-expansion wall bays; preserve all door locations and counts; future bays remain handleless solid wall panels or shallow alcoves, not doors
Usability constraints: keep a broad unobstructed central route at least visually equivalent to 2.2 metres; do not place furniture or rug over a doorway, interaction point, or main circulation route; no staircase; no new door or window; no open doorway
Style and mood: realistic polished 3D game environment render, aged but structurally intact, restrained dusk amber with blue-green shadows, private domestic memory, quiet and mysterious but not horror
Avoid: people, character silhouettes, school objects, signs, text, room numbers, extra doors, extra windows, modern furniture, excessive clutter, bright fire, candles, logos, watermark, gothic-horror decoration, collapsed or broken structure
Change only the residential dressing described above; keep everything else unchanged.
```

## 评审约束

- 当前仍只有四个可用房间入口与一扇锁闭外门。
- 两个未来扩展模数保持为无把手的实墙板或浅壁龛，本轮不是门。
- 壁炉不点火；家具与地毯不占用主要通路、门槛或交互站位。
- 2026-08-22 已获得负责人确认并进入 Blender 建模；精确尺寸以 v1 资产 manifest 为准。
