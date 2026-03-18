# Layered Memory System

This project uses a layered file-based memory system so future work can load only the memory needed for the current task.

## Layers

- `00-system`: How memory is organized, routed, and updated.
- `01-foundation`: Project-defining truths that should stay stable unless the product direction changes.
- `10-product`: Player fantasy, product positioning, and experience goals.
- `11-research`: External references and comparable games.
- `20-design`: Gameplay and simulation design conclusions.
- `30-prototype`: Concrete prototype specs, implementation targets, and technical framing.
- `90-active`: Current state, open questions, and next actions.
- `_templates`: Reusable note templates for future memory entries.

## Read Order

Read from stable to volatile:

1. `01-foundation`
2. `10-product`
3. `20-design`
4. `30-prototype`
5. `90-active`

Only read `11-research` when the task needs market/reference context.

## Retrieval Rule

Before starting a task:

1. Identify the task type.
2. Check [memory/00-system/task-load-map.md](/D:/myWorkSpace/appProjects/liquid/memory/00-system/task-load-map.md).
3. Load only the listed files for that task.
4. Update the lowest sensible layer when new information is learned.

## Update Rule

- Put long-lived truths in the highest stable layer possible.
- Put design conclusions in `20-design`.
- Put implementation details and prototype constraints in `30-prototype`.
- Put volatile work state in `90-active`.
- Do not duplicate the same fact across multiple files unless one file is a short pointer.

## Entry Points

- System rules: [memory/00-system/memory-rules.md](/D:/myWorkSpace/appProjects/liquid/memory/00-system/memory-rules.md)
- Task routing: [memory/00-system/task-load-map.md](/D:/myWorkSpace/appProjects/liquid/memory/00-system/task-load-map.md)
- Machine-readable index: [memory/00-system/memory-manifest.json](/D:/myWorkSpace/appProjects/liquid/memory/00-system/memory-manifest.json)
- Current context: [memory/90-active/current-context.md](/D:/myWorkSpace/appProjects/liquid/memory/90-active/current-context.md)
