# Patice

SDEZ 独立练习 mod，通过 MelonLoader 加载 `Patice.dll`，使用 Harmony 接入游戏原生播放与 autoplay。没有 AquaMai 运行时依赖，不修改或替换 `AquaMai.dll`。

- `src/`：练习状态、原生接口、界面和输入。
- `assets/`：设置按钮空框；字体、其他按钮和特效使用机内资源。
- `segments/`：片段格式和示例，只按谱面 ID + 难度匹配。
- `Patice.csproj`：独立 .NET Framework 4.7.2 工程。
- `.github/workflows/release.yml`：GitHub Actions 构建与发布。

提交和 PR 自动执行 `dotnet restore Patice.csproj` 和 `dotnet build Patice.csproj`；推送 `v*` 标签后，将 `Patice.dll` 上传到 GitHub Release。编译引用（游戏、Unity、MelonLoader、Harmony DLL）只下载到 Actions 的 `.build/` 目录。不会编译 AquaMai 项目，也不打源码补丁。

将 Release 中的 `Patice.dll` 放入游戏 `Mods/` 目录。入口为机台 Test 键。已有的其他练习功能应关闭，避免同时控制游戏时钟。

片段运行时路径：`UserData/PracticeSegments/{musicId}/{difficulty}.json`。毫秒区间为 `[startMs, endMs)`。示例的 `exampleOnly: true` 会阻止加载，尚未发布人工标记数据。

6 / 3 切换，4 确认，7 调整倍速，8 切换循环/单次。预览静音；按钮使用原生选歌左右音效。单次结束或确认退出循环后，从当前位置继续正常游玩。

目前仅完成本地构建，下面的游戏集成验收尚未执行。由于游戏时钟和 autoplay 是全局状态，目前只允许单玩家进入，由 Patice 自身阻止练习成绩写入。

`LICENSE` 保留所参考和复用代码的上游许可。

## 核心行为集成验收

在真实 SDEZ 中加载构建出的 DLL 和已有标记文件，使用实际按键、谱面、游戏时钟及音频执行。以下是实机验收用例，不是自动化测试；GitHub Actions 只负责构建和发布。

| 流程 | 操作 | 检查实际游戏行为 |
| --- | --- | --- |
| 选择与预览 | 正常游玩中进入练习，按 3 / 6 切换，按 7 改倍速 | 当前片段的真实音符自动判定；达到结束点回到开始点；预览无歌曲与打击声，按钮声音正常；倍速生效 |
| 确认与单次 | 选择单次，按 4 确认并打到片段结束 | 选中动画后倒数 3、2、1；倒数期间谱面不前进；开始后由玩家判定；结束后恢复原速度，继续后面的谱面 |
| 连续循环 | 选择循环，按 4 开始，连续完成两轮 | 每次都回到标记起点，音频和谱面同步，没有重复倒数，也不进入结算 |
| 中途退出 | 循环中按入口键，先取消一次，再确认退出 | 提示期间谱面暂停；取消后接着练；确认后从当前音符位置继续正常游玩，不回到片段起点或进入前位置 |
| 取消与清理 | 在选择页取消；另一次在倒数中取消；最后结束歌曲并进入下一首 | 选择页取消恢复进入前位置和状态；倒数取消回选择页；下一首没有遗留 autoplay、变速、静音或练习界面 |
