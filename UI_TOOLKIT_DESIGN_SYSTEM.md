# UnityEvalTool UI Toolkit Design System

Status: mandatory implementation contract for UnityAgentTool, its DebugPanel/DebugWindows, and Runtime workbench.

This document replaces ad-hoc visual decisions with a source-pinned contract derived from mature open-source AI interfaces. Standalone Runtime SystemInfo and PerformanceMonitor overlays retain their existing presentation; their Unity Agent workspace representations use the Agent design system without sharing stylesheets with the standalone overlays.

## 1. Source policy

### Primary source: DeepSeek Harness

- Repository: [deepseek-ai/deepseek-harness](https://github.com/deepseek-ai/deepseek-harness)
- Pinned commit: `47f943859bef60e4160492346772ded9b24f765a`
- License: MIT
- Role: primary geometry, typography, dark palette, responsive sidebar, composer, selectors, menus, modal, input, and scrollbar behavior.
- Source files:
  - [design-platform.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-theme/src/styles/design-platform.css)
  - [base.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-theme/src/styles/base.css)
  - [InputBar.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-conversation/src/client/skeleton/InputBar.module.css)
  - [PermissionSelect.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-conversation/src/client/skeleton/PermissionSelect.module.css)
  - [ConversationRoot.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-conversation/src/client/skeleton/ConversationRoot.module.css)
  - [ModelSelect.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-model-selection/src/client/ModelSelect.module.css)
  - [SidebarRoot.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-sidebar/src/client/SidebarRoot.module.css)
  - [Modal.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-primitives/src/Modal.module.css)
  - [Button.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-primitives/src/Button.module.css)
  - [Input.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-primitives/src/Input.module.css)
  - [PopupSelectView.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-commands/src/client/PopupSelectView.module.css)
  - [SettingsRoot.module.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-settings-general/src/client/SettingsRoot.module.css)
  - [scrollbar.css](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-theme/src/styles/scrollbar.css)
  - [columns.ts](https://github.com/deepseek-ai/deepseek-harness/blob/47f943859bef60e4160492346772ded9b24f765a/packages/client/ui-layout/src/client/columns.ts)

DeepSeek Harness is a developer preview. Pinning the commit is mandatory; do not silently replace these values when upstream changes.

### Cross-check sources

- [assistant-ui/assistant-ui](https://github.com/assistant-ui/assistant-ui), commit `ff1074ec167769ee7d8648e4db6f79aecd35b21b`, MIT. Used to cross-check the single-surface composer, 13/20 text, 28px action controls, model menu truncation, and explicit selected state.
- [danny-avila/LibreChat](https://github.com/danny-avila/LibreChat), commit `5ff282f9006c436e561de1afd39a481bea1ef0d8`, MIT. Used to cross-check a 44px input seat, rounded composer shell, centered maximum content width, 36px model trigger, and non-shrinking send action.
- [open-webui/open-webui](https://github.com/open-webui/open-webui), commit `01f4282f1ffe0d6212f58d3afbeae21fffd0c4be`. Used only as an interaction comparison. Do not copy its branded visual implementation or source into this project.

OpenAI's public open-source component list exposes Codex CLI, SDK, and App Server, but not the desktop App UI. Therefore the user-provided Codex screenshots are visual acceptance references, not a claimed CSS source.

## 2. Non-negotiable rules

1. No screen is visually designed before its component contract and acceptance matrix exist here.
2. Unity native controls may supply text editing, IME, accessibility, focus, and scrolling behavior only. Every visible background, border, padding, arrow, checkmark, scrollbar, hover, active, focus, selected, invalid, and disabled state must be package-owned.
3. Do not use Unicode characters as icons. Prohibited examples include `＋`, `◇`, `▾`, `↻`, `↑`, `■`, and `✓`. Use package-owned vector drawing or `VectorImage` assets so glyph availability cannot erase actions.
4. No readable text is smaller than 12 logical pixels. The previous 8/9/10px labels are forbidden.
5. A non-empty popup must contain at least one visible text row. An empty popup shell is a P0 defect and must not be shown.
6. Model values are selection-only. They come from remote discovery or the curated catalog; there is no editable model ID field.
7. Dynamic rows explicitly set `min-width: 0`; fixed actions explicitly set `flex-shrink: 0`.
8. Standalone SystemInfo and PerformanceMonitor overlays retain their existing styles. Shared theme changes must not cascade into them; the embedded Unity Agent System Info page renders the same data through separate Agent-owned components.

## 3. Source-derived dark tokens

These are the sRGB results of the pinned DeepSeek Harness dark token sheet, renamed for Unity. Alpha borders stay alpha colors instead of being flattened into a guessed background.

| Unity token | Value | DeepSeek source token |
|---|---:|---|
| `Canvas` | `#151517` | neutral-bluish-950 / bg-base |
| `Sidebar` | `#1B1B1C` | neutral-bluish-900 / sidebar-fill |
| `Surface1` | `#232324` | neutral-bluish-875 / bg-layer-1 |
| `Surface2` | `#2C2C2E` | neutral-bluish-850 / bg-layer-2, input-major |
| `Surface3` | `#353638` | neutral-bluish-800 / bg-layer-3, menu, selector |
| `SurfaceHover` | `rgba(255,255,255,0.08)` | interactive-bg-hover |
| `SurfaceActive` | `rgba(255,255,255,0.14)` | interactive-bg-active |
| `Border1` | `rgba(255,255,255,0.06)` | border-l1 / darkmode-thin |
| `Border2` | `rgba(255,255,255,0.12)` | border-l2 |
| `Border3` | `rgba(255,255,255,0.16)` | border-l3 |
| `TextPrimary` | `#F9FAFB` | neutral-bluish-50 / label-primary |
| `TextSecondary` | `#CFD3D6` | neutral-bluish-300 / label-secondary |
| `TextTertiary` | `#ADB2B8` | neutral-bluish-400 / label-tertiary |
| `TextCaption` | `#81858C` | neutral-bluish-600 / label-caption |
| `TextDimmed` | `#43454A` | neutral-bluish-750 / label-dimmed |
| `Accent` | `#60A5FA` | blue-400 / business-primary |
| `Error` | `#F25A5A` | red-400 / error-primary |
| `Warning` | `#DD8629` | amber-600 / warn-label |
| `Success` | `#22C55E` | green-500 / success-primary |
| `Scrollbar1` | `#3C3C3D` | neutral-700 / scrollbar l1 |
| `Scrollbar1Hover` | `#545557` | neutral-600 / scrollbar l1 hover |
| `Scrollbar2` | `#545557` | neutral-600 / scrollbar l2 |
| `Scrollbar2Hover` | `#65676B` | neutral-550 / scrollbar l2 hover |
| `Mask` | `rgba(0,0,0,0.50)` | bg-mask-1 |

Brand color is used for business state, selected tabs, and the active send action. Permission level is not presented as success; `FullAccess` uses primary/secondary ink and a clear label.

## 4. Typography and text visibility

The web references use a platform font stack, but UI Toolkit cannot express that fallback chain reliably across Editor and Player targets. UnityAgentTool therefore does not bundle a font, enumerate operating-system font families, call `Font.CreateDynamicFontFromOSFont`, or assign `unityFont` / `unityFontDefinition` on its root. The workbench inherits the active `PanelSettings` / Theme font and lets Unity perform the platform fallback. This avoids package font weight and per-process dynamic font objects while keeping Editor and Runtime aligned with their host.

PanelSettings and Theme assets used by the workbench must not introduce a package-owned font reference. Missing CJK or Latin glyphs are reported as a host Theme/Unity fallback defect during visual acceptance, not hidden by creating another runtime font. Icons remain package-owned vector drawing so action visibility never depends on a text font.

Unity 2022.3 does not expose CSS `line-height` as a normal USS layout property. Source line heights therefore become minimum label seats and vertical padding contracts:

| Role | Font | Source line height | Unity minimum text seat | Uses |
|---|---:|---:|---:|---|
| Caption | 12 / 500 | 18 | 18 | descriptions, timestamps, state |
| Control | 13 / 500 | 20 | 20 | permission/model triggers, tabs |
| Body | 14 / 400 | 22 | 22 | fields, menu cells, settings text |
| Body strong | 14 / 500 | 22 | 22 | model names, card titles |
| Composer | 16 / 400 | 24 | 24 | prompt input |
| Page title | 16 / 500 | 24 | 24 | header and modal title |
| Empty hero | 26 / 500 | 32 | 32 | empty conversation headline |

Rules:

- A single-line `Label` uses `WhiteSpace.NoWrap`, middle-left alignment, the role's minimum seat, and ellipsis only where the complete value is available in a popup or owned tooltip.
- Wrapped text uses `WhiteSpace.Normal`, content-driven height, and never a fixed height.
- CJK, `gypqj`, `ÅÉ`, long model IDs, and punctuation are mandatory test strings.
- A non-empty text element with resolved width `<= 0`, resolved height below its seat, or primary text colored like its background fails validation.
- Raw `style.fontSize` outside the design-system implementation is prohibited. Screens request a typography role.

## 5. Component contracts

### Composer

Mapped from DeepSeek Harness `InputBar.module.css` and `ConversationRoot.module.css`:

- Transcript content maximum width: 748.
- Composer card maximum width: 780 (`748 + 32`).
- Side clearance: 16; bottom padding: 8.
- Card: width 100%, radius 22, top padding 10, 1px `Border1`, `Surface2`, vertical gap 12.
- Input: 16/24, left 16, right 12, top 4, transparent background and border.
- Draft scrollport: the only composer scroller; maximum 14 text lines. Editor/Runtime may use a lower tested cap only if both use the same value.
- Toolbar: horizontal, vertical center, gap 12, padding `2 8 6`, `min-width: 0`.
- Attach action: 28x28 circle, non-shrinking.
- Permission and model triggers: height 28, radius 24, 13/20/500, horizontal padding `0 4 0 8`, visible focus state.
- Send/stop action: fixed 34x34 as specified by the source. It never shrinks.
- Catalog loading/fallback/error is shown in the model menu or a notice above the card, not as a permanent third composer row.
- The outer composer is the only surface. The native TextField input background/border/padding are completely reset; no nested rectangular field is visible.

### Model, provider, permission, and effort selection

Mapped from `ModelSelect.module.css` and `PopupSelectView.module.css`:

- Trigger height 28, radius 24, 13/20/500, `min-width: 0`, maximum width 220.
- Trigger shows one line with ellipsis; the trailing chevron is a 12–16px vector seat with `flex-shrink: 0`.
- Model menu opens upward from the composer, offset 8. General popup offset is 4.
- Model menu width: 240 or available window width minus 32, whichever is smaller. Settings may use up to 320 when the anchor is wider, but never exceed window width minus 32.
- Maximum height: `min(360, viewport height - 96)`; general command popup maximum is 320.
- Surface: padding 4, radius 12, `Surface3`, `Border1`.
- Model option: width 100%, minimum height 38, padding `6 8`, gap 8, radius 10.
- Model name: 14/20/500, single-line ellipsis. Description: 12/18, single-line ellipsis. Selected check is a vector 18px trailing seat.
- Loading/empty status: 13/20 with at least 10px padding. Error/warning: 12/18 with a visible retry action. Zero visible rows means the popup must not open.
- Popup owns a vertical scrollbar and preserves its 8px gutter.
- Opening any popup hides owned tooltips. Detach, resize, Escape, outside click, or anchor removal closes it.

### Sidebar

Mapped from `SidebarRoot.module.css` and `columns.ts`:

- Expanded width: default 280, minimum 264, maximum 420.
- Below 1024 window width, automatically collapse to a fixed 56px rail instead of stacking above the chat.
- Root padding: `6 12`; font 14. Collapsed rail padding: `18 10 6`.
- Rail controls: 36x36. Expanded icon buttons: 28x28.
- Logo row: height 60, padding `8 0 8 4`, bottom margin 8.
- New conversation: height 38, radius 12, padding `8 16`, font 14/22/500. Collapsed: 36x36 icon action.
- Conversation list is the only expanding/scrolling region. Settings remains in the footer.
- Text rows reserve a stable 8px scrollbar gutter so the list does not jump.
- The center conversation column target minimum is 640; below that, controls switch to compact labels without shrinking typography.

### Input field

Mapped from `Input.module.css`:

- Standard field height 32, horizontal padding 8, gap 6, radius 8, 1px `Border2`, `Surface1`.
- Text is 14/22. Icon seat is 16x16. Placeholder uses `TextCaption`.
- Focus changes the owned border/focus ring; never expose the Unity skin focus visual.
- Multiline inputs use the Body or Composer typography seat and content-driven height.

### Button

Mapped from `Button.module.css`:

- Default: height 36, horizontal padding 14, gap 4, radius 18, text 14/22.
- Small: height 28, horizontal padding 10, radius 14, text 12/18.
- Disabled opacity is 0.4, but the vector icon and action boundary must remain visible.
- Icon-only actions retain the matching fixed square hit seat; their visual glyph is not a text character.

### Settings workspace

Mapped from `SettingsRoot.module.css`:

- Wide dialog/workspace target: `800 x min(800, viewport - 48)`, maximum width `viewport - 48`, radius 24.
- Navigation width 188. Navigation cell: height 40, padding `9 16 9 12`, gap 8, radius 12, text 14/22.
- Header height 54. Options padding `0 24 24`.
- The source only clamps this panel to the viewport and does not define a narrow-screen reflow. Unity therefore reuses the source-pinned 1024/56 sidebar rail rule for its settings navigation below 1024; it does not invent smaller typography or overlapping columns.

### Modal

Mapped from `Modal.module.css`:

- Full-root absolute overlay, 24px outer padding, `Mask` background.
- Dialog width `min(380, available width)`, maximum available height, radius 24, `Surface2`, `Border1`, gap 20.
- Header padding `22 14 12 24`; title 16/24/500; close action 28x28 radius 8.
- Body horizontal padding 24; body copy 14/22.
- Footer horizontal padding 24, right-aligned, gap 8.
- Body scrolls when needed; title and actions remain visible. An empty modal never opens.

### Scrollbar

Mapped from `scrollbar.css`:

- Width and height 8; track and corner transparent; thumb radius 4.
- Base surfaces use `Scrollbar1` / `Scrollbar1Hover`; popup/modal/input surfaces use `Scrollbar2` / `Scrollbar2Hover`.
- Low/high buttons and all Unity images are hidden.
- Space is reserved in the layout. Showing a scrollbar cannot move adjacent text or the composer.

### DebugPanel and Runtime Console

- Keep the established module hierarchy and interaction behavior; apply these same tokens and typography seats rather than inventing a second style family.
- Debug window title/body use 16/24 and 14/22. Toolbar/control rows use 13/20 and 28–32px controls. Log metadata may use 12/18 but nothing smaller.
- Runtime Console tabs, Log, CLI, EvalTool, and Tools share the same input, selector, popup, modal, and scrollbar contracts.
- Standalone SystemInfo and PerformanceMonitor overlays are excluded. No global selector may alter their existing labels, bars, graphs, padding, or colors. Their embedded Unity Agent cards follow this section's Runtime Console tokens, typography, wrapping, and responsive layout rules.

## 6. Unity 2022.3 conversion table

| Web source feature | Unity implementation |
|---|---|
| Browser font fallback stack | Inherit the active Unity PanelSettings / Theme font; do not bundle, enumerate, or dynamically create fonts in the package. |
| CSS custom properties | One package-owned C# token class plus USS literals generated from the same documented values. Screens cannot declare independent palettes. |
| `line-height` | Typography role applies font size, minimum text seat, alignment, and padding; verify glyph bounds in screenshots. |
| `@container` | `GeometryChangedEvent` adds/removes explicit compact classes using the resolved composer width. |
| CSS grid track solver | C# responsive layout uses the pinned 1024/280/56/640 geometry and never measures arbitrary label widths to choose a mode. |
| `position: fixed; inset: 0` | Absolute overlay child of the panel root with all four offsets zero. |
| `::before` / `::after` | Explicit non-picking child element or `generateVisualContent`; never a text glyph. |
| SVG/data icon | Package-owned vector drawing/`VectorImage`. Do not use Unicode icons. |
| `box-shadow` / backdrop blur | UI Toolkit 2022.3 has no equivalent. Use surface elevation, border, and mask only; do not fake blur with a random gradient. |
| `scrollbar-gutter: stable` | Reserve the 8px scrollbar seat in C# layout and keep it present when the thumb is transparent. |
| `:focus-visible` | Owned focus state driven by `FocusInEvent` / `FocusOutEvent`; do not rely on Unity's default focus selector. |
| `:has`, data attributes | Explicit semantic class names updated by view state. |
| `overflow-wrap:anywhere` | Normal CJK/body wrapping; long code/paths use a dedicated scroll or display formatter. Never reduce font size to force a fit. |
| viewport `min()`/`calc()` | Resolve against root geometry after layout and clamp popup/modal bounds in C#. |

## 7. Required screen acceptance

Every screen is implemented, opened in Unity, captured, and then reviewed by a new context-clean SubAgent. All P0/P1 findings are fixed before moving to the next screen.

### Universal text cases

Each applicable screen must contain or temporarily exercise:

- `创建一个跟随玩家移动并可保存状态的镜像角色能力`
- `OpenAI 响应 API / 工具调用中`
- `“设置（实验性）”：连接失败，请重试。`
- `openai/gpt-5.6-terra-reasoning-preview-2026-08-13`
- `ModelCatalogRefreshUnavailableBecauseConnectionIsOffline`
- `Typography clipping: gypqj ÅÉ`

Any missing glyph, half glyph, zero-width non-empty label, clipped ascender/descender, invisible action, or empty popup is a P0 failure.

### UnityAgent Chat

- Empty, running, approval, error, long conversation, and archived/grouped sidebar states.
- Every selector opened with normal, loading, curated fallback, empty, and error results.
- Widths 1460, 1024, 840, 640, and 480; heights 900, 526, and 360.
- Composer with empty, one-line, six-line, and over-limit drafts.
- Send/stop icon remains visible and fixed-size in all widths.

### UnityAgent Settings

- Provider preset, protocol, remote/curated model, effort, local key saved/not-saved, paths, groups, modal error, and confirmation.
- Long path, CJK group name, and long provider/model name.
- No editable model ID control.

### DebugPanel / DebugWindows

- Existing module navigation, draggable/resizable windows, enum selection, toggle, slider, input, progress, and scroll behavior.
- Validate the game viewport at the target runtime resolution; the panel must not cover unrelated game UI through an accidental full-screen opaque surface.
- Confirm standalone SystemInfo and PerformanceMonitor have byte-identical style files to the protected baseline, then separately capture the Agent-styled embedded System Info workspace.

### Runtime Console

- Core shell plus Log, CLI, EvalTool, and Tools tabs.
- Empty/populated/filtered logs, multiline CLI input/output, Eval busy/error, long tool names/descriptions, tab overflow, and draggable panes.

### Scale and host parity

- Editor at 100%, 125%, and 150% display scale.
- Runtime and Editor at the same logical size must use the same token values, typography roles, control geometry, popup states, and truncation rules.
- A responsive breakpoint is based on logical panel width, not physical display scaling.

## 8. Static enforcement

The final implementation must make these checks pass:

- No prohibited Unicode icon characters in UI source.
- No raw 8/9/10px text declarations.
- No native `DropdownField`/`GenericDropdownMenu` in UnityAgent UI.
- No model text editor.
- No popup path that can show a card without a visible row/status.
- No package UI code assigns a native tooltip string; owned tooltip only.
- No package UI code or asset bundles, enumerates, dynamically creates, or explicitly assigns a font for the Unity Agent workbench.
- No global Unity selector from DebugPanel changes SystemInfo or PerformanceMonitor.
- No screen-local palette or raw typography value outside the design-system layer.

This document governs implementation. When a source rule cannot be represented by Unity 2022.3, the conversion table wins and the limitation must be recorded; inventing an undocumented fallback is not allowed.
