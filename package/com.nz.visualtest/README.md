# NZ Visual Test

Unity 视觉测试基类 VisualTestBase：自动建摄像机、GUI 操作提示覆盖层、FFmpeg 屏幕录制、键鼠输入模拟。适合在 PlayMode 测试中人眼回放 + 录像留证。

内嵌 FFmpegOut 作为录制后端（Keijiro Takahashi 原作 port）。

## 安装

在目标项目的 Packages/manifest.json 中加一条：

    "com.nz.visualtest": "https://github.com/UnityX103/Unity-visualtest-skill.git?path=/package/com.nz.visualtest#main"

## 使用

让 PlayMode 测试类继承 NZ.VisualTest.VisualTestBase；SetUp/TearDown 会自动创建临时摄像机、启动 FFmpeg 录制。子类通过 LogInputAction / SimulateKey / SimulateMouseButton 与 GUI 交互。录制产物输出到 Application.persistentDataPath/TestOutput/<类名>/Video/。可覆盖 TestName / UseDedicatedTestCamera / RecordOnSetUp。

如果项目需要本地可编辑副本，可以把本仓库中的 `package/com.nz.visualtest` vendoring 到目标项目的 `LocalPackage/`，再把 manifest 改成 `file:` 依赖。

## 依赖

- com.unity.inputsystem ≥ 1.4.4
- com.unity.test-framework ≥ 1.1.33
- FFmpeg 可执行文件需位于 PATH，或位于 `Assets/StreamingAssets/FFmpegOut/macOS/`（未找到会自动降级为不录，不阻塞测试）
