# 梦境洋房多角度概念图 v1

- 状态：已采用为洋房 v1 建模参考；原始评审稿仍保留在 `work/`
- 工具：Codex 内置图像生成
- 后处理：Pillow 确定性排版、缩放和 4K 评审板拼装
- 结构权威：`manor-floor-plan-v1.png`
- 视觉锚点：`manor-interior-v2.png`、`manor-exterior-v1.png`
- 共通镜头：约 60° FOV、约 1.65m 眼高、约 19° 固定俯角（外观视图除外）

生成视图都使用 `stylized-concept`；门位纠偏使用 `precise-object-edit`。以下记录最终采用图对应的完整请求。

## B · 南端反打，向北看房间四

```text
Render the same dream manor interior from a reverse axial camera, standing just inside the locked south double entrance and looking north through the foyer and the full 5.6m-wide, 18m-long, 4.2m-high central hall toward Room 4's north-end double doors. Use the interior v2 image as the identity/material/furniture/lighting reference, the deterministic floor plan as the authoritative geometry and door-location reference, and the exterior as secondary architectural-language reference. Camera: 60-degree perspective FOV, 1.65m eye height, about 19-degree downward pitch, centered third-person game composition, landscape 16:9, no character. Preserve dark beams, aged plaster, dark-oak wainscot, terrazzo floor, brass fittings, runner rug, cold fireplace, console, bench, paintings and umbrella stand in spatially consistent positions. Room 1 and Room 2 oppose each other near the south portion; Room 3 is only on the east/north side; Room 4 is the centered double-door terminus. Two future modules are handleless solid panels/alcoves, never doors. Keep at least 2.2m circulation. No extra doors, windows, stairs, side corridors, people, text, modern objects, bright fire, horror, logos or watermark.
```

Correction applied to the selected image:

```text
Correct only the door architecture. Remove glass sidelights from the far Room 4 interior double door and infill with aged plaster and oak wainscot. Show exactly three side single doors before Room 4: Room 1 near-left/south, Room 2 opposing near-right/south, Room 3 farther north/right. Convert every additional side door to a handleless solid decorative panel/alcove. Preserve camera, perspective, lighting, materials, furniture and path exactly.
```

## C · 前厅西北角，锁闭大门近景

```text
Render the same manor foyer from its northwest inner corner, looking diagonally southeast toward the inside face of the permanently locked south double entrance. Use interior v2 for identity and the floor plan for authoritative geometry. Camera: 60-degree FOV, 1.65m eye height, about 19-degree downward pitch, landscape 16:9, no character. Establish the closed double leaves, narrow leaded-glass sidelights, brass handles and lock; cold Arts and Crafts fireplace; umbrella stand; runner stopping before the threshold; bench and console against walls; at least 2.2m turning area. Preserve 6.4x6.0m foyer, 4.2m height, beams, plaster, wainscot and terrazzo. The entrance is the only exterior door. No extra opening, stairs, people, text, keys, fire, horror, logos or watermark.
```

## D · 南段西侧斜视，相对房门

```text
Render the same manor south hall from a west-side transverse oblique viewpoint around 2.5m north of the foyer threshold, looking east and slightly south. Use interior v2 and the corrected reverse view for continuity; use the floor plan as door authority. Camera: 60-degree FOV, 1.65m eye height, about 19-degree downward pitch, landscape 16:9, no character. The near Room 1 door must be visible at the near-left edge and the opposing Room 2 door across the hall on the east/right wall. Communicate the 5.6m clear width, continuous beams and wainscot, runner and unobstructed thresholds. Keep residential furniture against walls. Whole-plan count remains four rooms, one locked exterior door and two solid future modules. No extra doors/windows, stairs, open passages, people, text, school objects, horror, logos or watermark.
```

## E · 北段东侧斜视，扩展墙与房间四

```text
Render the same manor from the east side of the north half of the central hall, looking diagonally northwest toward the north terminus. Use interior v2 and the corrected reverse view for continuity; use the floor plan as door authority. Camera: 60-degree FOV, 1.65m eye height, 19-degree downward pitch, landscape 16:9, no character. Show one Room 3 single door on the near-right/east wall, the centered solid Room 4 double door with no glass or sidelights, and two clearly solid handleless expansion panels/alcoves. Preserve runner, beams, plaster, wainscot, terrazzo and clear path. No locked exterior door in this north-facing shot, no additional side door, window, stair, branch, people, text, fire, horror, logos or watermark.
```

Correction applied to the selected image:

```text
Remove all foyer-only items from this north zone, including fireplace, umbrella stand, console, vase and book; infill with aged plaster, oak wainscot, one rural painting and simple sconces. Keep exactly one Room 3 door at the near-right foreground and the centered Room 4 double door. Convert every other single door to wall or a handleless solid panel. Preserve exact camera, floor, runner, ceiling, lighting and image quality.
```

## 东北后侧外观

```text
Render the same single-storey English Arts and Crafts manor from a northeast rear three-quarter viewpoint, using the front exterior as identity, the floor plan as massing authority and interior v2 as secondary material reference. Show the larger north-end former-salon mass and side wings, with the same rough plaster, brick, half timber, slate roofs, leaded windows, two principal chimneys and dusk palette. Rear and side facades may have windows but absolutely no exterior door, service door, porch, stair, basement entrance or open passage. Restrained overgrown garden, intact sealed structure, realistic buildable PBR geometry. No people, text, modern additions, upper storey, garage, horror, logos or watermark.
```

## 东南侧长轴外观

```text
Render the same manor from a southeast side three-quarter viewpoint looking northwest along the long east facade. Use the south-front and north-rear views for identity and the floor plan for axial massing. Human eye height about 1.7m, 50–60-degree FOV, landscape 16:9. Connect the locked south front entrance to the long hall mass and broader north former-salon volume. The front entrance is the one and only exterior door; the east facade has windows only. Preserve single-storey slate roofs, plaster, brick, half timber, leaded windows, two principal chimneys and dusk palette. Keep wall lengths, roof junctions and window rhythm legible for Blender. No side/rear door, stairs, garage, basement, modern extension, upper storey, people, signs, vehicles, horror, logos or watermark.
```

## 使用边界

- 生成透视图负责气氛、材质、家具语言和镜头构图，不作为精确施工图。
- 尺寸、房间相对位置、当前门数量和未来扩展模数以确定性平面图为准。
- 用户确认前，不启动 Blender 建模或 Godot 场景迁移。
