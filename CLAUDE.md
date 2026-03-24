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
