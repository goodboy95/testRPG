# 住宅双向推门与院外接坡修复

2026-09-15。对应负责人批准的门交互与读档后院门通行修复，实际结果见[验收记录](../production/home-access-validation.md)。

## 门操作与保存

`[已实现]` 住宅四扇室内门及独立住宅预览的双扇正门，根据玩家在关闭门平面的哪侧，向对侧打开 90°。正式住宅正门仍是室内外转场入口。交互距离 1.65 米、F／A 操作及 0.45 秒动画不变。

取消原先覆盖整段开门区域的矩形拒绝判定和临时扫掠碰撞。`HomeDoorMotion` 按真实门轴、门宽、厚度及角色胶囊半径，分段检查门叶运动；遇到玩家停在安全角度，让开后继续。可见门叶与简化叶片碰撞同步，关门沿此次开启的方向返回。运动中再次输入无效，暂停／转场冻结运动，不推开或瞬移角色。

`[已实现]` 用户明确批准新增可选 `VillageExploration.DoorOpenSides` 字典；键沿用五处门名，值为 -1 或 +1，表示门平面法向侧（x 轴门对应 Z，z 轴门对应 X）。`Doors` 仍表示上次稳定开闭，方向也仅记录稳定端点；半开角度、阻挡和动画进度不入档。SaveEnvelope v1 和梦境内容版本 6 不变。

旧档缺少方向时使用 `HomeLayout3D` 原方向；若已打开门叶覆盖旧档玩家位置，先尝试相反方向，另一侧安全才采用，然后执行原有落点校验。恢复快照优先于住宅缓存，出入住宅保留方向、门灯；完成稳定操作后沿用原自动保存。独立预览仍不写正式存档。

## 院外阻挡与局部修整

实际连续输入复现到 `(-56.0564,-0.9579,58.2138)`：从院门走到院外低处，再回到村路时停住。碰撞对象为 Terrain3D，接触法线约 `(0.0328,0.8334,0.5517)`；查询和物理射线高度一致。v4 保留的家门道路带与移动后的对面院地接合过陡。未发现需要降低整片地形或重复院门碰撞的依据。

`[已实现]` 仅平顺 X=-62～-50、Z=52～62 范围内的接坡，276 个 0.5 米间距高度样本改变，中心及左右一米主通行线纵坡低于 20°。Z≤52 的自家庭院、住宅位置、院门 2.8 米净宽、桥流、其他建筑和角色参数保持不变。此处属于本次明确批准的局部接坡修复，替代此前该小段道路保护高度不变的约束；不扩大为全村地形重做。

同步保存 Terrain3D 原生区域和浮点／EXR 高度，投影三个受影响道路网格的相关顶点，维持原路面偏移、UV 和材质。没有提高 35° 爬坡上限、增加跳跃、移动住宅或把角色传到院外。

## 编辑、备份与复验

- 当前地形：`assets/generated/village/terrain-v4/data/`，浮点工作源：`art/terrain/village-v4/height.bin`。
- 修改前地形与保护区：`art/terrain/village-v4/edits/home-access/height.bin`、`protected.bin`；`repair-mask.bin` 标明本轮局部变化。
- 修改前 v4 Blend／GLB／manifest 保存在本机 `outputs/home-access/before/`；v1～v3 与住宅原资产不修改。
- `scripts/tools/repair_home_access.py` 记录这次显式局部编辑。已有备份时拒绝重放，不能当作日常构建器覆盖后续精修。日常编辑原生区域和已保存 Blend，按 v4 导出流程同步。

```powershell
& 'D:\ProgramFiles\Blender\blender.exe' --background --python-exit-code 1 --python scripts/tools/build_village_v4.py -- check
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --script scripts/tools/village_v4_terrain.gd -- --home-access
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --audio-driver Dummy --fixed-fps 60 --path . --scene res://scenes/testing/HomeAccessProbe.tscn -- --prologue-e2e
```

专项需本机隔离 fixture `outputs/home-access/user-save-original.json`。首次载入前从副本生成卧室起点；之后通过真实移动动作与 F／菜单输入，连续走到院外 Z=61、村路左右段，再原路回卧室。正式用户存档不写入。30／120 FPS 分别另加 `--access-30`／`--access-120`，输出三档分辨率图片与连续片段。该测试的场景建立、旧档迁移 fixture 与连续行走必须区分，不以测试设置坐标代替过门。

`[固定约束]` 本机角色和含角色画面仍仅限本机非商业；没有提交、推送或游戏导出。负责人手感与视觉评价单列，第一章任务仍未接入。
