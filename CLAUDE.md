# Claude Code Instructions

## 工作流约定

编码任务使用 Opus + Codex 协同模式：

1. Opus 只负责撰写实现计划，写入 `.claude/codex-plan.md`
2. 启动 Sonnet subagent（`model: "sonnet"`）执行：
   a. 读取 `.claude/codex-plan.md`
   b. 翻译为 Codex 友好的英文指令，写入 `.claude/codex-prompt.md`
   c. 调用 `codex exec --full-auto -C "<项目绝对路径>" -o ".claude/codex-output.md"` 执行
   d. 审查 `git diff`，不合格则 `git checkout .` 回滚重试（最多 2 次）
   e. 报告结果：修改了哪些文件、主要变更、执行轮次、是否符合 plan
3. Opus 不直接写代码

## 文件约定

| 文件 | 用途 |
|------|------|
| `.claude/codex-plan.md` | Opus 的实现计划 |
| `.claude/codex-prompt.md` | Sonnet 翻译后的 Codex 英文指令 |
| `.claude/codex-output.md` | Codex 的执行输出 |

## 当前技术方向

自建 GPU FLIP 流体求解器（替代 Zibra Liquid），用于复杂封闭容器（Blender 建模）内的实时流体交互。容器可由玩家鼠标拖拽旋转，流体不得泄漏。

- FLIP 压力投影从数学上保证流体不进入固体区域（不是事后补救）
- 技术栈：Unity + URP + Compute Shader (HLSL) + Blender
- SDF 做固体边界（离线从 Blender Mesh 生成，旋转时在本地坐标系查询）

## FLIP 求解器架构

MAC 交错网格 + 粒子混合方法，每帧 6 个 Compute Shader dispatch：

```
ClassifyCells → P2G → AddForces → PressureSolve → G2P → Advect
```

## 已有文件

```
Assets/Scripts/FluidSolver/
├── FlipSolver.cs          — MonoBehaviour 主控制器，调度 6 个 Compute Shader
├── FlipSolverData.cs      — FlipParticle 结构体、常量（64³网格）、CellType 枚举

Assets/Shaders/FluidSolver/
├── ClassifyCells.compute   — 清零网格 + 标记 FLUID 格子
├── ParticleToGrid.compute  — P2G（InterlockedAdd 原子操作）+ 归一化
├── AddForces.compute       — 重力
├── PressureSolve.compute   — 散度 + Jacobi 迭代 50 次 + 压力投影
├── GridToParticle.compute  — G2P（0.95 FLIP + 0.05 PIC 混合）
├── AdvectParticles.compute — 粒子移动 + clamp

Assets/Editor/
├── FlipSolverBootstrap.cs  — 菜单 Liquid → Create FLIP Solver
```

## Milestone 路线图

| 阶段 | 内容 | 状态 |
|------|------|------|
| M1 | 64³ 网格 + 重力 + 粒子下落 | ✅ 已完成已验证 |
| M2 | SDF 碰撞 + 方盒子边界 | 待做 |
| M3 | 旋转容器 + SDF 本地坐标查询 | 待做 |
| M4 | Blender 复杂 Mesh → SDF → 求解器 | 待做 |
| M5 | 屏幕空间流体渲染（深度平滑+法线+折射） | 待做 |
| M6 | 高级渲染（菲涅尔、SSR、水花、泡沫） | 待做 |
| M7 | 性能优化（分辨率自适应、LOD） | 待做 |

## M2 规格

- 生成方盒子 SDF（硬编码，不需要 Blender）
- ClassifyCells 中用 SDF 标记 SOLID 格子
- PressureSolve 中 SOLID 格子设为边界条件（边界速度）
- AdvectParticles 中用 SDF 做碰撞反弹
- 验证标准：水在方盒子里不漏，底部形成水池
