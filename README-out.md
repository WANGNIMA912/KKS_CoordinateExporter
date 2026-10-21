# KKS Coordinate Exporter

[中文](#中文) | [English](#english)

在 CharaStudio 里按一个键，把选中角色当前的服装导出成标准服装卡。 / Export the selected character's current outfit as a standard coordinate card in CharaStudio with one keypress.

---

## 中文

**KKS Coordinate Exporter** 是一个 BepInEx 5 插件，为 **Koikatsu Sunshine（KKS）** 的 **CharaStudio**（`CharaStudio.exe`）提供一键服装卡导出：选中角色后按键，当前服装会被写成一个标准的 `.png` 服装卡，可直接在本编或工作室的服装载入界面读取。

### 功能

- 导出当前服装槽位的全部部件：Tops / Bottoms / Bra / Shorts / Gloves / Pantyhose / Socks / Indoor Shoes / Accessories
- 该槽位启用了妆容时，一并向导出的卡里写入 makeup
- 保留已安装插件写进卡里的扩展数据：**KKSABMX** 的骨骼修正、**DBDE** 的动态骨骼，以及其它通过 `CharaCustomFunctionController` 注册的插件数据
- 缩略图按你**当前视角的水平方向**实时拍摄（252×352 全身像），不是复用角色卡图片
- 导出结果在屏幕**左上角**提示：成功为绿色色条，失败为红色色条，约 4.5 秒后淡出

### 环境要求

| 组件 | 版本 | 必需 |
| --- | --- | --- |
| Koikatsu Sunshine（KKS） | 含 CharaStudio | 是（不兼容 Koikatu） |
| BepInEx | 5.4.x（仅 5.x，不支持 BepInEx 6） | 是 |
| KKSAPI | 1.48 及以上 | 是 |
| ExtensibleSaveFormat | 21.1.4 及以上 | 是 |
| ConfigurationManager | 19.0 及以上 | 否，装了才能在游戏里按 F1 改键 |
| KKSABMX / DBDE | 任意版本 | 否，装了且角色用过，导出时才会带上对应数据 |

### 安装

1. 关闭游戏。
2. 把 `KKS.CoordinateExporter.dll` 放到 `<游戏目录>/BepInEx/plugins/KKS_CoordinateExporter/`。
3. 启动 `CharaStudio.exe`，BepInEx 控制台出现 `Loading [KKS Coordinate Exporter ...]` 即加载成功。

### 使用

1. 在工作室里**选中**要导出的角色，按住 Ctrl 可多选。
2. 按 **F8**（可在 ConfigurationManager 里改）。
3. 左上角提示会显示结果，导出的卡保存到 `UserData/ExportedCoordinate/`。
4. 文件名格式为 `<角色名>_<槽位>_<yyyyMMdd_HHmmss_fff>.png`，例如 `Kasumi_Plain_20261021_181530_012.png`；重名时自动追加 `_1`、`_2`。

槽位取自角色当前穿着的那一套：**Plain / Swim / Pajamas / Bathing**。

### 配置

配置文件：`BepInEx/config/com.kkcsproj.kks.coordinateexporter.cfg`

| 段 / 键 | 默认值 | 说明 |
| --- | --- | --- |
| `General / ExportHotkey` | `F8` | 导出快捷键，支持组合键 |
| `General / ExportDirectory` | `UserData/ExportedCoordinate` | 导出目录；相对路径以游戏根目录为基准，也可填绝对路径 |
| `General / VerboseLog` | `false` | 打开后日志会列出每张卡写入的扩展数据键与大致字节数 |

修改后即时生效（导出目录在每次导出时重新解析）。

### 工作原理（简述）

1. 读取选中角色当前的服装槽位，做一份深拷贝，不修改角色身上的数据。
2. 用临时相机按当前视角方向拍一张 252×352 的全身缩略图；拍不到时回退到角色卡自带的图。
3. 触发 KKAPI 的服装保存链，让 KKSABMX / DBDE 等控制器把扩展数据写进这张卡。
4. 调用游戏自带的 `ChaFileCoordinate.SaveFile` 写文件，ExtensibleSaveFormat 会自动追加扩展数据块，所以文件格式与本编导出的服装卡完全一致。

### 注意事项

- 只导出**当前服装槽位**，不会一次导出 Plain / Swim / Pajamas / Bathing 四套。
- 缩略图是当前视角方向的全身像，不带官方构图背景；角色被隐藏或渲染失败时回退到角色卡图片。
- 在工作室里切换过服装类型后，部分插件（如 KKSABMX）记录的槽位索引可能不随切换刷新，扩展数据可能对应另一个槽位；需要绝对精确时，先在本编切好目标服装再进工作室。
- 日志里出现 `OnCoordinateBeingSaved fired outside chara maker` 属于兼容路径的提示，扩展数据仍会写入，可以忽略。
- 导出目录的可写性由游戏进程权限决定，写到受保护目录会失败并在日志里报错。

### 故障排查

| 现象 | 处理 |
| --- | --- |
| 按 F8 没反应 | 确认控制台有加载日志；确认当前在工作室且已选中角色 |
| 提示“没有选中任何角色” | 用鼠标点选角色，出现选中框后再按 F8 |
| 卡里没有 ABMX / DBDE 数据 | 确认对应插件已加载、角色确实用过；打开 `VerboseLog` 看写入的扩展键 |
| 游戏里改不了快捷键 | 安装 ConfigurationManager，按 F1 修改 |

### 基本信息

- 插件 GUID：`com.kkcsproj.kks.coordinateexporter`
- 版本：1.0.0
- 目标进程：`CharaStudio.exe`（BepInEx 5 / Mono / .NET Framework 4.6）

---

## English

**KKS Coordinate Exporter** is a BepInEx 5 plugin for **Koikatsu Sunshine (KKS)** that adds one-key outfit export to **CharaStudio** (`CharaStudio.exe`). Select a character, press the hotkey, and the current outfit is written as a standard `.png` coordinate card that can be loaded directly in the main game or in Studio.

### Features

- Exports every part of the current outfit slot: Tops / Bottoms / Bra / Shorts / Gloves / Pantyhose / Socks / Indoor Shoes / Accessories
- Includes makeup when the slot has makeup enabled
- Preserves extended data written by other plugins: **KKSABMX** bone edits, **DBDE** dynamic bones, and any other plugin registered through `CharaCustomFunctionController`
- The thumbnail is rendered live at 252×352 from your **current view direction**, instead of reusing the character card image
- Results appear as a **top-left on-screen toast**: green for success, red for failure, fading out after about 4.5 seconds

### Requirements

| Component | Version | Required |
| --- | --- | --- |
| Koikatsu Sunshine (KKS) | with CharaStudio | Yes (not compatible with Koikatu) |
| BepInEx | 5.4.x (5.x only; BepInEx 6 is not supported) | Yes |
| KKSAPI | 1.48 or newer | Yes |
| ExtensibleSaveFormat | 21.1.4 or newer | Yes |
| ConfigurationManager | 19.0 or newer | No, needed for in-game hotkey rebinding (F1) |
| KKSABMX / DBDE | any | No, data is only exported if installed and used by the character |

### Installation

1. Close the game.
2. Copy `KKS.CoordinateExporter.dll` to `<GameDir>/BepInEx/plugins/KKS_CoordinateExporter/`.
3. Launch `CharaStudio.exe`; `Loading [KKS Coordinate Exporter ...]` in the BepInEx console means it loaded.

### Usage

1. **Select** one or more characters in Studio (hold Ctrl to multi-select).
2. Press **F8** (configurable).
3. The toast reports the result, and the card is saved to `UserData/ExportedCoordinate/`.
4. File names look like `<chara name>_<slot>_<yyyyMMdd_HHmmss_fff>.png`, for example `Kasumi_Plain_20261021_181530_012.png`. Duplicate names get a `_1`, `_2` suffix.

The slot is whatever the character is currently wearing: **Plain / Swim / Pajamas / Bathing**.

### Configuration

Config file: `BepInEx/config/com.kkcsproj.kks.coordinateexporter.cfg`

| Section / Key | Default | Description |
| --- | --- | --- |
| `General / ExportHotkey` | `F8` | Export hotkey; key combinations are supported |
| `General / ExportDirectory` | `UserData/ExportedCoordinate` | Output folder; relative paths are resolved against the game root, absolute paths also work |
| `General / VerboseLog` | `false` | When enabled, the log lists the extended-data keys and approximate sizes written to each card |

Changes take effect immediately (the output folder is resolved on every export).

### How it works

1. Reads the selected character's current outfit slot and makes a deep copy, leaving the character itself untouched.
2. Renders a 252×352 full-body thumbnail from the current view direction with a temporary camera; falls back to the character card image if rendering fails.
3. Triggers the KKAPI coordinate-save chain so KKSABMX, DBDE and other controllers write their extended data into the copied card.
4. Calls the game's own `ChaFileCoordinate.SaveFile`; ExtensibleSaveFormat appends the extended-data block, so the file is byte-for-byte the same format as a vanilla coordinate card.

### Notes and limitations

- Only the **current outfit slot** is exported; Plain / Swim / Pajamas / Bathing are not exported in one go.
- The thumbnail is a full-body shot from your current view without the official background composition; it falls back to the character card image when the character is hidden or rendering fails.
- After switching outfit types in Studio, some plugins (such as KKSABMX) may keep a stale slot index, so their extended data can belong to another slot. For exact results, switch to the target outfit in the main game before entering Studio.
- `OnCoordinateBeingSaved fired outside chara maker` in the log comes from the compatibility fallback; extended data is still written and the message can be ignored.
- Whether the output folder is writable depends on the game process permissions; writing to a protected folder fails and is logged.

### Troubleshooting

| Symptom | Fix |
| --- | --- |
| F8 does nothing | Check the console for the load message; make sure you are in Studio with a character selected |
| Toast says no character is selected | Click a character until the selection frame appears, then press F8 |
| No ABMX / DBDE data in the card | Make sure those plugins are loaded and were used by the character; enable `VerboseLog` to see the written keys |
| Cannot change the hotkey in game | Install ConfigurationManager and press F1 |

### Details

- Plugin GUID: `com.kkcsproj.kks.coordinateexporter`
- Version: 1.0.0
- Target: `CharaStudio.exe` (BepInEx 5 / Mono / .NET Framework 4.6)
