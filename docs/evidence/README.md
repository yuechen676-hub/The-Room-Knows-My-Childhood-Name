# 实现与测试资料

[文档目录](../README.md)

代码摘录和图像用于解释项目实现，未授予通用复用许可。使用范围见[版权与使用说明](../../COPYRIGHT.md)，第三方内容保留其原有许可。

本目录收录代码摘录、性能测量、资产统计和测试结果，供结合正文阅读。实验报告保留采样日期和当时的实现状态；其中的完整工程路径用于定位开发文件，部分文件未在文档仓库公开。

## 测试记录

- [工程与测试摘要](review-summary.json)：范围、文件计数、66 个后端测试结果。
- [离线数据与启动检查](offline-validation.json)：54 条台词、77 个变体及 headless 检查。
- [源码清单与 SHA-256](source-manifest.json)：18 份快照的完整本地工程相对路径。
- [发行验证章节](../VALIDATION.md)：包体、校验与限制。

## 性能与资产

- [2026-09-30 基线报告](performance-20260930.md)
- [2026-10-05 镜面与阴影实验](performance-20261005.md)
- [2026-10-07 音频、资产与 LOD 实验](performance-20261007.md)
- [GPU 基线原始记录](gpu-baseline.json)
- [镜面裁剪候选原始记录](mirror-candidates.json)
- [资产预算摘要](asset-budget.txt)
- [外壳替换完成记录](shell-replace.txt)

## 源码快照说明

`source/` 包含天空、预览工具、材质输入扩展、镜面裁剪、预算工具、外壳生成、交互遮挡、离线回放、转场与测量探针。文件按当前本地版本复制，哈希见清单。缺少依赖和场景，不能作为独立 Unity 工程使用。

URP Lit 输入扩展依赖 Unity 包，其原有许可与归属说明随源码保留。

## 图例

`figures/` 收录制作过程中保存的 11 张 JPG，用于展示天空、着色器、外壳和优化前后的差异。图例对应开发过程中的效果，发行测试范围见[测试报告](../VALIDATION.md)。

另收录两张筛选后用于相框材质的 LoRA 辅助插画原图：`lora-selected-labyrinth.png` 与 `lora-selected-bedroom.png`。制作流程与材质对应见[资产制作](../PRODUCTION.md)。

