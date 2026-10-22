# KKS Coordinate Exporter 源码讲解

> 对应版本 1.0.2。改代码前先读完第 1 节（设计取舍），再看要改的文件。

## 1. 设计取舍：为什么不自己序列化

游戏本身就能写服装卡：`ChaFileCoordinate.SaveFile(string path)`。它做三件事：

1. 有 `pngData` 就写 PNG（`PngFile.SkipPng` 在读取时按魔术字跳过）；
2. 写标记 `【KoiKatuClothes】`、版本字符串、卡片名；
3. 调 `SaveBytes()` 用 MessagePack 序列化 `clothes` / `accessory` / `makeup`。

而 **ExtensibleSaveFormat（ESF）** 用 Harmony transpiler 在 `SaveFile` 末尾注入扩展数据块写入。
所以只要老老实实调 `SaveFile`，ABMX、DBDE、其它 `CharaCustomFunctionController` 的数据就有机会被写进去，
格式也一定和本体一致。反过来自己写二进制要复刻 MessagePack 结构与 ESF 块，既容易错也没必要。

本项目只做三件“绕路”的事：

- **复制一份 `ChaFileCoordinate`**：导出不应该改动角色身上的数据，用 `SaveBytes` + `LoadBytes` 深拷贝。
- **临时伪造 `MakerAPI.GetCharacterControl()`**：见第 3 节，这是 Studio 里唯一拿不到扩展数据的原因。
- **自己拍缩略图**：不直接抄角色卡 PNG，而是用临时相机拍一张 252×352 的全身像（见 4.4）。
  尺寸沿用本编 `ChaCustom.CustomCapture.CapCoordinateCard` 的硬编码值，保证卡片列表里比例一致。

## 2. 文件一览

| 文件 | 作用 |
| --- | --- |
| `src/CoordinateExporterPlugin.cs` | 插件身份、配置绑定、F8 热键、Harmony 生命周期 |
| `src/CoordinateExportService.cs` | 选中角色 → 服装槽位 → 深拷贝 → 命名 → 保存 → 日志 |
| `src/MakerApiExportScope.cs` | 导出作用域 + `MakerAPI.GetCharacterControl` 前缀补丁 |
| `src/ThumbnailCapture.cs` | 临时相机拍 252×352 缩略图，失败回退角色卡 PNG |
| `src/ExportToast.cs` | 屏幕左上角的 IMGUI 导出提示（成功绿条 / 失败红条） |
| `KKS.CoordinateExporter.csproj` | net46、`bin/` 引用、`CheckRefs`、部署 target |

## 3. 关键点：Studio 里为什么扩展数据会丢

KKAPI 的保存链是这样的：

```
ChaFileCoordinate.SaveFile(...)
  └─ ESF transpiler 在方法末尾调用 CoordinateWriteEvent
       └─ KKAPI 订阅的 lambda（CharacterApi/<>c::<Init>b__15_1）
            ├─ ctrl = MakerAPI.GetCharacterControl()   // Studio 里恒为 null
            ├─ ctrl == null → LogError("...outside chara maker...") 并 return
            └─ ctrl != null → CharacterApi.OnCoordinateBeingSaved(ctrl, file)
                 └─ 每个 CharaCustomFunctionController.OnCoordinateBeingSaved(...) 写扩展数据
```

也就是说：**工作室里根本走不到控制器那一步**。于是

```csharp
using (MakerApiExportScope.Enter(chaCtrl))
{
    coordinate.SaveFile(destPath);   // 期间 GetCharacterControl() 返回 chaCtrl
}
```

`MakerApiExportScope` 是个静态的“当前角色”槽位，`Enter` 时压入、`Dispose` 时恢复（支持嵌套）。
补丁 `MakerApiGetCharacterControlPatch` 是 Harmony **Prefix**：作用域里有角色就写 `__result` 并返回 `false`
（跳过原方法），否则返回 `true` 走原逻辑。这样不会污染 `MakerAPI.InsideMaker` 状态，
也不会影响工作室里其它正常调用。

### 3.1 为什么还要一个反射兜底

Harmony 在 Mono 上只改 JIT 后的方法入口。如果调用方在补丁生效前就被 JIT 并且把 `GetCharacterControl`
**内联**了，补丁对它无效——`PatchAll` 成功也不代表真能拦到。所以导出前会实测一次：

```csharp
bool patchActive = makerApiPatchApplied && IsMakerApiPatchActive(chaCtrl);
```

`IsMakerApiPatchActive` 在作用域里调用一次 `MakerAPI.GetCharacterControl()`，看返回值是不是目标角色。
不是的话就走 `InvokeKkapiSaveHook`：反射调用 `CharacterApi.OnCoordinateBeingSaved`（这个方法是 `private static`，
没有公开入口）。此时 ESF 的钩子之后还会因为拿不到角色打一条 KKAPI 错误日志，属于预期现象。

## 4. 逐文件说明

### 4.1 CoordinateExporterPlugin.cs

- 特性：`BepInPlugin(com.kkcsproj.kks.coordinateexporter, KKS Coordinate Exporter, 1.0.2)`（版本来自 csproj 的 `<Version>`，由生成的 `PluginVersion` 常量提供）、
  `BepInProcess("CharaStudio.exe")`、KKSAPI 1.48 与 ESF 硬依赖、ConfigurationManager 软依赖。
  少了 `BepInProcess` 或写错成 `KoikatsuSunshine.exe`，工作室就不会加载。
- 配置：`General/ExportHotkey`（`KeyboardShortcut`，默认 F8）、`ExportDirectory`、`VerboseLog`。
- `Awake`：绑定配置 → `new Harmony(PluginGuid)` → `PatchAll`。补丁失败只记错误并置
  `_makerApiPatchApplied = false`，插件仍可用。
- `Update`：先用 `Input.GetKeyDown(hotkey.MainKey)` 过滤，再 `hotkey.IsDown()` 判组合键
  （省掉每帧 LINQ 分配）；再查 `StudioAPI.InsideStudio`，最后调服务。
- `OnDestroy`：`UnpatchSelf()`，避免热重载后补丁重复。
- GUID 变化会让配置文件名跟着变，旧 cfg 不会自动迁移。

### 4.2 CoordinateExportService.cs

流程（`ExportOne`）：

1. 取 `chaCtrl.chaFile.coordinate[fileStatus.coordinateType]`；越界回落到 0，槽位为空直接报错。
2. `new ChaFileCoordinate().LoadBytes(source.SaveBytes(), source.loadVersion)` 深拷贝；
   `LoadBytes` 返回 `false` 表示数据损坏，放弃这一个角色。
3. `coordinateName = 角色名 + " [" + 类型 + "]"`；`pngData = ThumbnailCapture.Capture(chaCtrl, chaFile)`
   （现拍的 252×352 缩略图，拍不到时由 `Capture` 自己回退，可能为 null）。
4. 文件名：`清理后的角色名_类型_yyyyMMdd_HHmmss_fff.png`，重名加 `_1`、`_2`。
   `SanitizeFileName` 去掉控制字符与 `Path.GetInvalidFileNameChars()`，截到 48 字符。
5. 保存：补丁生效 → 在作用域里 `SaveFile`；否则先反射触发保存链再 `SaveFile`。
6. `LogExtendedData` 用 `ExtendedSave.GetAllExtendedData(coordinate)` 读回扩展数据：
   为空时警告 ABMX/DBDE 可能没生效，`VerboseLog` 下打印每个键的 version 与字节数。

其它细节：

- `ResolveDirectory`：空值回落默认目录；相对路径用 `BepInEx.Paths.GameRootPath` 拼，最后 `Path.GetFullPath` 规范化。
- `IsBusy` 防止重入（导出途中又按 F8）。一次导出的异常按角色隔离，最后汇总成功/失败数量。
- 角色名优先 `chaFile.parameter.fullname`，其次 `charaFileName`，再兜底 `"Chara"`。

### 4.3 MakerApiExportScope.cs

```csharp
internal static class MakerApiExportScope
{
    internal static ChaControl Current { get; set; }
    internal static IDisposable Enter(ChaControl chaCtrl);   // 记录旧值、压入新值
}

[HarmonyPatch(typeof(MakerAPI), nameof(MakerAPI.GetCharacterControl))]
internal static class MakerApiGetCharacterControlPatch
{
    private static bool Prefix(ref ChaControl __result) { ... }
}
```

注意：

- Prefix 必须 `static bool`，`ref ChaControl __result` 对应原方法返回值；返回 `false` 表示跳过原方法。
- 只在 `Current != null` 时拦截，否则 `return true`，避免影响游戏正常流程。
- 作用域只包住 `SaveFile` 那一行，越短越好。

### 4.4 ThumbnailCapture.cs

目标：导出的卡在服装列表里有和本体一样比例的预览图。

```csharp
byte[] png = ThumbnailCapture.Capture(chaCtrl, chaFile);  // 252x352，失败回退 chaFile.pngData
```

步骤（`Render`）：

1. 角色 `visibleAll == false` 直接跳过（拍出来只有背景）。
2. `FindSourceCamera`：优先 `Studio.Studio.Instance.cameraCtrl.mainCmaera`（注意拼写少个 e），
   其次 `Camera.main`，最后在 `Camera.allCameras` 里挑没有 `targetTexture` 且 `depth` 最大的。
3. `ConfigureCamera`：`cam.CopyFrom(source)` 继承渲染设置，再覆盖 fov=35、aspect=252/352、
   `clearFlags=SolidColor`、纯色背景。**cullingMask 必须去掉 `Studio/Camera` 层**，
   否则工作室的选中框、IK 手柄会被拍进卡里（`GameScreenShot.CreatePngScreen` 也是这么做的）。
4. `ComputeBounds` 合并角色所有 Renderer 的 `bounds`，跳过粒子/拖尾/线渲染器
   （`ParticleSystemRenderer` 只能按类型名判断，`bin/` 没有 ParticleSystemModule），
   再用 `objHead` 位置修正头顶，防止超长头发把镜头推飞。
5. 取景是**逐角投影精确解**（不是按高度/宽度估算）：朝 +direction（水平方向）后退距离 d 时，
   某个角在相机空间的深度是 depth + d；要求它落在视锥内
   （|x| <= tanH * z、|y| <= tanV * z），解出该角需要的最小 d，取 8 个角的最大值。
   这样包围盒会**刚好贴住画面较紧的那条边**，不再有 FitMargin 那种整体留白。
   拍摄方向取**当前相机位置减包围盒中心**的水平分量——用户把视角转到正面，卡就是正面。
   注意 252×352 是固定纵横比：只有较紧的一轴能同时贴住两边，另一轴按比例留白
   （站姿角色通常上下贴边、左右留白），两轴同时贴边只能靠裁切或拉伸。
6. 渲染到 756×1056（3 倍超采样）→ `Graphics.Blit` 缩到 252×352 → `ReadPixels` →
   `EncodeToPNG`。直接 252×352 渲染锯齿很明显，所以先大后小。
7. `finally` 里释放 `RenderTexture`、销毁临时 `GameObject` / `Texture2D`；
   任何异常都 `LogWarning` 后回退 `chaFile.pngData`。

### 4.5 ExportToast.cs

导出结果直接显示在屏幕左上角，不依赖游戏 UI：

- `Success(中文, 英文)` / `Failure(中文, 英文)` 各压一条进静态列表，最多保留 4 条；
  每条记录 `Born/Expires`，4.5 秒后淡出（`Time.unscaledTime`，暂停游戏也能正常计时）。
- `Draw()` 由插件的 `OnGUI` 每帧调用；列表为空时立刻返回，平时没有开销。
  每项画一个半透明黑底 `GUI.Box`，左侧 3px 色条区分成功/失败，然后 `GUI.Label` 文字。
- 中文字体：`Font.GetOSInstalledFontNames()` 里找微软雅黑/黑体/宋体/Noto Sans CJK 等，
  用 `Font.CreateDynamicFontFromOSFont` 建动态字体；一个都没有时改为显示英文，
  避免显示一屏方块。
- 绘制异常只记一次日志（`_lastError` 去重），避免 `OnGUI` 每帧刷屏。

## 5. csproj 要点

- `TargetFramework=net46`；引用全部来自仓库根 `bin/`，`<Private>false</Private>`（不复制到输出）。
- `<Version>` 是版本号的唯一来源；`GeneratePluginVersion` 在编译前生成 `obj/.../PluginVersion.g.cs` 里的 `partial class` 常量，源码不写死版本号。
- `CheckRefs` 在 `BeforeBuild` 检查关键 DLL（BepInEx / KKSAPI / ESF / 0Harmony /
  Assembly-CSharp，以及 IMGUI、TextRendering、ImageConversion 三个 Unity 模块），
  缺了直接给人话错误，避免一屏 `CS0246`。
- `DeployToGame`：`Deploy=true` 时把 `$(TargetPath)` 复制到 `$(DeployDir)`，默认
  `<GameDir>\BepInEx\plugins\KKS_CoordinateExporter`。
- 新增引用时记得同步 `bin/MANIFEST.md` 与 `AGENTS.md`。

## 6. 常见坑

| 坑 | 说明 |
| --- | --- |
| `StudioAPI.InsideStudio` 在 `Awake` 为 false | 只在热键/导出时判断，不要在 `Awake` 里判 |
| `nowCoordinate` 不可靠 | 工作室里可能是 null，不要用它，改用 `chaFile.coordinate[...]` |
| `CurrentCoordinate` 不随切换刷新 | 控制器 `Awake` 时固定；ABMX 可能按旧槽位序列化 |
| `pngData` 为 null | 合法；`SaveFile` 只是不写 PNG，读取侧 `PngFile.GetPngSize` 会返回 0 继续解析 |
| 直接改角色身上的 `coordinate` | 会污染存档，必须深拷贝 |
| `CharacterApi.OnCoordinateBeingSaved` 不是 public | 反射调用，方法名或签名变了要同步改 |
| 导出目录写成相对路径 | 以游戏根目录为基准，不是插件目录 |
| `Texture2D.EncodeToPNG` 找不到 | 它在 `UnityEngine.ImageConversionModule`，要在 csproj 里显式引用 |
| `CameraControl` 类型冲突 | Studio 里有两个同名类；写全名 `Studio.CameraControl`，否则会解析到全局那个 |
| 缩略图里出现选中框/IK 手柄 | 拍之前必须把 `Studio/Camera` 层从 cullingMask 里去掉 |

## 7. 修改后怎么验证

1. `dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj`：0 警告 0 错误。
2. 如果本地保留了预编译的 `tools/verify/.../verify.exe`，用它检查 TFM、`BepInProcess`、依赖 GUID、引用解析；源码目前不在工作区。
3. 关游戏 → `-p:Deploy=true` 部署 → 开游戏，看 `BepInEx\LogOutput.log` 有加载日志。
4. 工作室选中角色按 F8：日志出现“已导出 …（N 字节，用时 … ms）”，
   `UserData/ExportedCoordinate` 下出现新卡，屏幕左上角出现绿色提示并在约 4.5 秒后淡出。
5. 用图片查看器打开新卡：缩略图应是**当前视角方向的全身像**，不是原角色卡图；
   把角色隐藏后再导出一次，应回退成角色卡 PNG（日志有“缩略图回退到角色卡 PNG”）。
6. 打开 `VerboseLog`，确认扩展数据里出现 `KKABMPlugin.ABMData` / `org.njaecha.plugins.dbde`。
7. 用本编或工作室的服装载入界面读这张卡，确认身体/服装/ABMX 与 DBDE 效果都在。