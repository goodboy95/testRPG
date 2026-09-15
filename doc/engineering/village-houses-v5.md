# 四户住宅贴图与门前生活样板 v5

> 2026-09-15 正式采用：负责人已确认将 v6（四户住宅 v5、主角家暖色外观、全村树木）与配套住宅外观启用。正式游戏和默认村庄预览现用 v6，沿用修复后的 v4 布局／地形；独立预览 `--village-v4` 保留旧外观回归。第一章委托未接入。[采用与验证](../production/village-v6-adoption.md)。下述样板轮“尚未切换”是当轮交付状态。


2026-09-15。`[已实现]` house02、house03、house13、house15 外观独立派生自包含院外接坡修复的 v4；只用于独立走查。正式醒后探索及默认预览继续使用 v4，第一章委托尚未接入。实际测试和负责人确认分别见[验收记录](../production/village-houses-v5-validation.md)。

## 启动与版本边界

```powershell
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageHouseArtPreview.tscn
```

[独立入口](../../scenes/testing/VillageHouseArtPreview.tscn)复用 v4 布局、现有修复后的 Terrain3D、上午光照、Orbit 镜头与室外 4.2 米/秒。住宅门灯、双向推门、暂停、住宅往返及缺失角色 billboard 回退继续沿用。样板不访问正式存档；正式控制器不读取该美术选择。旧 v1～v4 与住宅源保留。

## 四户外观

| 房屋 | 材料与构造 | 独立生活物件 |
| --- | --- | --- |
| house02 | 米白石灰墙、灰瓦、木门和补过的门框；窗台、薄檐边、院墙同色 | 门廊旧木凳、靠墙收纳架 |
| house03 | 浅灰翻修墙、灰蓝门窗及柜面；新旧抹灰交接、金属排水构件 | 院侧晾衣、阳台搭布、封闭侧板储物柜 |
| house13 | 暖灰抹灰、前墙及侧墙砖墙脚、局部露砖、深木门 | 侧边晾晒、两只收拢筐具、靠墙收纳 |
| house15 | 浅墙、石质墙脚、暗绿门；低处少量色差，落水管伸到容器上方 | 竹架菜畦、雨水桶、薄收货垫板 |

房屋原有尺寸、门洞、屋顶主体、院墙与院地顶点高度不变；替换原重复菜畦、桶具和晾晒陈列。新增窗框、窗台与檐边只为近看构造，不扩大建筑或新增室内。没有住户姓名、任务交互、森林、NPC 或章末工具箱。

## 布局与碰撞

[共用物件定义](../../scripts/presentation/VillageHouseArtLayout3D.cs)使用建筑局部米制坐标、底部位置，供 Blender 和 Godot 同时读取。11 个独立物件根，其中 9 个大件采用简化碰撞；阳台布和 1.8 厘米薄垫无独立碰撞。

既有院内重复小物件原本没有独立碰撞，本次移除其几何不遗留隐形障碍。建筑和院墙碰撞不变；新增物件碰撞命名为所属 `Collision_houseXX/V5_物件`。中间约 2.8 米净通道和 3×3 米卸货区不计入家具占地；阳台布在 3.44 米以上，不占用地面净空。菜畦、晾衣架等占地由简单实体表达，玩家不能从摆设内部穿过。

## 材质与保存

- [v5 Blender 源](../../art/blender/village/village-v5.blend)、[GLB](../../assets/generated/village/3d/village-v5.glb)、[实际资源清单](../../assets/generated/village/3d/village-v5-manifest.json)。
- [三张新增 1K 颜色图和来源记录](../../art/blender/village/v5-textures/provenance.json)复用已采用的 CC0 抹灰、木材原图；砖、瓦、石材及法线／粗糙度沿用[v3 来源](../../art/sources/cc0/village-art-v3/provenance.json)。本轮无新下载、采购或图像生成。
- 四种墙面材质共用一张抹灰图，由导出的颜色因子区别；修补表面和木材另两张图。纹理按实际尺寸投影，避免共享原型缩放导致砖瓦拉伸；不烘焙固定日照。近墙位置使用局部几何分区表现修补，不增加运行时生成。
- 四户单独复制房壳／院落网格和材质槽，未修改其他共享实例。新增色彩材质只出现在四户和 `V5_` 构件上。27 张 GLB 提取图沿用 VRAM Compressed、mipmaps；源图保持可编辑，不降低既有地形质量。

```powershell
# 保存手工修改后导出
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v5.py -- export
# 临时目录复导；不覆盖源或运行资源
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v5.py -- check
```

`init` 只从保存的 v4 派生，v5 已存在时拒绝覆盖；不得删除源来绕过保护。[贴图配方](../../scripts/tools/prepare_village_houses.py)也拒绝重建已有目录。后续编辑前备份 v5 Blend、纹理和共用物件定义，不运行旧建村工具。只读检查核对源／GLB 哈希、253 个保留网格、有限法线和 UV、地形哈希与复导一致性。

没有 Core、存档字段、内容版本或公共游戏 API 改动。美术选择和物件记录仅服务表现层。负责人确认后再另批采用，其他住宅及公共建筑仍待推广；本机角色与含角色图片／录像只限本机非商业使用，不提交、推送、分发或导出游戏。
