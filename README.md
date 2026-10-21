# KKS Coordinate Exporter

在 **CharaStudio**（`CharaStudio.exe`）里按一个键，把**选中角色**当前的服装导出成
**服装卡**（`.png`），包含：

- Tops / Bottoms / Bra / Shorts / Gloves / Pantyhose / Socks / Indoor Shoes / Accessories
- 妆容（如果该服装槽位启用了 makeup）
- **KKSABMX** 的骨骼修正数据（扩展数据键 `KKABMPlugin.ABMData`）
- **DBDE（Dynamic Bone Distribution Editor）** 的动态骨骼数据（扩展数据键 `org.njaecha.plugins.dbde`）
- 其它注册了 `CharaCustomFunctionController` 的插件按同样的机制写进卡里的数据

导出走游戏自己的 `ChaFileCoordinate.SaveFile`，所以文件格式与本体的服装卡完全一致，
ExtensibleSaveFormat 会自动追加扩展数据块。

## 依赖

| 组件 | 版本 | 说明 |
| --- | --- | --- |
| BepInEx | 5.4.23.5 | 必须 5.x，不支持 BepInEx 6 |
| KKSAPI | 1.48 | 硬依赖（`marco.kkapi`） |
| KKS_ExtensibleSaveFormat | 21.1.4 | 硬依赖（`com.bepis.bepinex.extendedsave`） |
| ConfigurationManager | 19.0 | 软依赖，装了才能在游戏里按 F1 改配置 |
| KKSABMX / DBDE | 任意 | 可选；装了且角色用了，导出时才会带上对应数据 |

## 安装

把 `KKS.CoordinateExporter.dll` 放到：

```
<游戏目录>\BepInEx\plugins\KKS_CoordinateExporter\KKS.CoordinateExporter.dll
```

启动 `CharaStudio.exe`，BepInEx 控制台里应出现：

```
Loading [KKS Coordinate Exporter 1.0.0]
[Info   :KKS Coordinate Exporter] KKS Coordinate Exporter 1.0.0 已加载。快捷键 F8，导出目录 ...\UserData\ExportedCoordinate。
```

## 使用

1. 在工作室里**选中**要导出的角色（可多选，按住 Ctrl 点选）。
2. 按 **F8**（可在 F1 配置里改）。
3. 导出成功后屏幕**左上角**会出现一条提示（绿色色条），显示文件名，约 4.5 秒后淡出；
   没有选中角色、目录不可写、导出失败时会出现红色提示。同一时间最多叠 4 条。
4. 导出的卡在 `<游戏目录>\UserData\ExportedCoordinate\` 下，文件名形如：

```
<角色名>_<服装类型>_<yyyyMMdd_HHmmss_fff>.png
```

例如 `小春_Plain_20261021_181530_012.png`。重名会自动追加 `_1`、`_2`。

服装类型取自角色的当前服装槽位（Plain / Swim / Pajamas / Bathing）。
导出的卡可以在本编或工作室的服装载入界面直接读取。
## 配置

文件：`BepInEx\config\com.kkcsproj.kks.coordinateexporter.cfg`

| 段 / 键 | 默认值 | 说明 |
| --- | --- | --- |
| `General / ExportHotkey` | `F8` | 导出快捷键；支持组合键（ConfigurationManager 里点一下就能改） |
| `General / ExportDirectory` | `UserData/ExportedCoordinate` | 导出目录；相对路径以游戏根目录为基准，也可填绝对路径 |
| `General / VerboseLog` | `false` | 打开后日志里会列出每张卡写入的扩展数据键与大致字节数 |

改完配置即时生效（目录在每次导出时重新解析）。

## 工作原理（简述）

1. `StudioAPI.GetSelectedCharacters()` 拿到选中角色。
2. 按 `chaCtrl.fileStatus.coordinateType` 取当前服装槽位 `chaCtrl.chaFile.coordinate[type]`。
3. 用 `new ChaFileCoordinate().LoadBytes(src.SaveBytes(), src.loadVersion)` 做深拷贝，
   不修改角色身上的数据。
4. 补上 `coordinateName`，并**现拍一张 252×352 的缩略图**：临时相机按当前视角的水平方向
   后退到角色包围盒的 8 个角刚好贴住画面边界（较紧的一轴齐边），3 倍超采样后缩小
   （`ThumbnailCapture`）。拍不到时（角色隐藏、渲染异常）回退到角色卡的 `pngData`，
   再没有就留空。
5. 触发 KKAPI 的 `CharacterApi.OnCoordinateBeingSaved` 链，让 ABMX / DBDE 等控制器把扩展数据
   写到这张卡上（Studio 里 `MakerAPI.GetCharacterControl()` 返回 `null`，需要
   `MakerApiExportScope` 的 Harmony 前缀补丁或反射兜底，详见 `docs/CODE-WALKTHROUGH.md`）。
6. 调 `coordinate.SaveFile(path)`；ESF 的 transpiler 在方法末尾写入扩展数据块。

## 构建

只依赖仓库根 `bin/`，**不需要游戏目录**：

```bash
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj
```

产物：`KKS.CoordinateExporter/bin/Debug/KKS.CoordinateExporter.dll`。

部署到游戏（只有这一步需要 `GameDir`）：

```bash
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj ^
  -p:GameDir="E:\kk\[ScrewThisNoise] Koikatsu Sunshine EX BetterRepack R12" ^
  -p:Deploy=true
```

默认部署到 `<GameDir>\BepInEx\plugins\KKS_CoordinateExporter\`；可用 `-p:DeployDir=...` 覆盖。

如果 `bin/` 缺 DLL，`CheckRefs` 会直接报“找不到编译引用”并指向 `bin/MANIFEST.md`。

## 产物自检

```bash
tools/verify/bin/Debug/net10.0/verify.exe ^
  KKS.CoordinateExporter/bin/Debug/KKS.CoordinateExporter.dll
```

应看到 `TargetFramework: .NETFramework,Version=v4.6`、`BepInProcess("CharaStudio.exe")`，
且所有引用都能解析（游戏程序集 `PublicKeyToken=!! NULL !!` 是正常的）。
## 已知限制

- **只导出当前服装槽位**，不会一次导出 Plain/Swim/Pajamas/Bathing 四套。
- 缩略图是**当前视角方向**的全身像，不是本编那种带背景/姿势的官方构图；角色被隐藏或渲染失败时
  回退到角色卡 PNG，角色卡也没有图时导出卡没有预览图。
- 左上角提示是纯 IMGUI，中文字体取自系统（微软雅黑/黑体等）；系统缺中文字体时自动改显示英文。
- `CharaCustomFunctionController.CurrentCoordinate` 只在控制器 `Awake` 时按当时的服装类型初始化，
  在工作室里切换服装类型后不会刷新；如果先切过服装再导出，ABMX 数据可能记录了**另一个槽位**的修正。
  需要绝对准确时，先在角色卡里切到目标服装再进工作室。
- 若 Harmony 补丁没生效（Mono 把目标方法内联了），日志里会出现 KKAPI 的
  `OnCoordinateBeingSaved fired outside chara maker for ...`；这是预期兜底路径，
  扩展数据仍会写入，可以忽略。`VerboseLog` 下能看到实际写入的键。
- 导出目录可写性由游戏进程权限决定；写到系统保护目录会失败并在日志里报错。

## 故障排查

| 现象 | 处理 |
| --- | --- |
| 按 F8 没反应 | 确认 BepInEx 控制台有加载日志；确认当前在工作室且已选中角色 |
| 日志提示“没有选中任何角色” | 用鼠标点选角色（出现选中框），再按 F8 |
| 日志提示“卡片里没有扩展数据” | ABMX / DBDE 未安装，或角色没用它们，或控制器未加载 |
| 卡里没有 ABMX 数据 | 检查 KKSABMX 是否加载；看 `VerboseLog` 日志里的扩展键 |
| 游戏里改不了快捷键 | 装 ConfigurationManager（19.0），按 F1 |

## 相关文档

- 源码逐段讲解：`docs/CODE-WALKTHROUGH.md`
- 构建/引用/工作区约定：仓库根 `AGENTS.md`
- 编译引用来源与校验和：`bin/MANIFEST.md`