# Engine Workspace

The completed browser MVP lives in [prototype/engine/web](/D:/myWorkSpace/appProjects/liquid/prototype/engine/web).

The next implementation target is [prototype/engine/unity](/D:/myWorkSpace/appProjects/liquid/prototype/engine/unity).

This engine workspace is still intentionally lightweight. The browser prototype exists to validate:

- trackball-style container rotation
- square glass container readability
- droplet motion on inner faces
- merging behavior under gravity and angular inertia

The Unity prototype exists to replace the browser approximation with GPU-driven liquid simulation while preserving:

- trackball-style container manipulation
- transparent square-glass-container readability
- convincing wall interaction and settling behavior

Keep each heavier engine project in its own subdirectory instead of replacing the existing prototype history.
