Unity Menu System

This is a complete UI menu navigation and layout framework. It handles cursor-driven menu navigation, multiple option types, programmatic sub-menus, automated animation queues, and a custom layout system with geometric arrangement shapes. Fully generic - no domain types required.


HOW IT WORKS

There are two main menu types:

1. Menu -- a container that holds MenuOption components as children. Each MenuOption has a type (Slider, Toggle, Menu, Scene, ControlSetup, CloseMenu, SendMessage, Return, DisableCursor, GameSettingList). The cursor moves between them and clicks them. This is used for the main pause menu, settings, etc.

2. SimpleMenu -- a lightweight programmatic menu. You add options at runtime via code with AddOption(). No child components needed. Supports sub-menu stacks, grouped options, and overlay detection. Good for popup lists like pickers, context menus, confirmation dialogs, etc.

The cursor (CursorObject) is the heart of navigation. When you press a direction, it uses a two-phase algorithm to find the next selectable:

- First, it casts a ray from the current selectable's center in the input direction and checks which selectable's hit-area rectangle intersects. It sweeps perpendicular offsets (0, +1, -1, +2, -2, ...) to be forgiving.
- If no raycast hit is found, it falls back to a cone search: expanding in 5-degree angular bands up to 90 degrees, picking the closest selectable in each band.

This gives precise navigation in grid layouts while staying forgiving in irregular arrangements.

CursorSelectable is the base class for anything the cursor can select. It manages selection state, visual fade, shake/sine effects, and raycast hit areas. Both MenuOption and SimpleMenu extend it.

MenuAutomation is a static system that queues up menu animations. When something needs the user's attention (a newly unlocked node, a completed step, a notification), it queues an animation that opens the relevant menu, moves the cursor to the new element, plays a plop effect, and waits for the user to close the menu. It batches consecutive reveals that share the same menu so the menu only opens once. Nodes are registered by key via MenuNode, and reveal groups implement IMenuRevealGroup.

SimpleGrid is a custom layout system that replaces Unity's GridLayoutGroup. It supports Grid, Row, Column, Circle, and Auto arrangements. Auto is smart: 1 item is centered, 2 items go side by side, 3 items form a triangle, 4 items form a square, 5+ items form a polygon. It also handles resizing and padding.


FILES

- CursorSelectable.cs -- base class for anything the cursor can select. Manages selection state, visual fade, shake/sine effects, raycast hit areas, and dropdown integration.
- CursorObject.cs -- the cursor controller. Handles directional navigation via raycast-area scanning with cone fallback, auto-repeat, confirm/cancel/pause input, and smooth position interpolation.
- Menu.cs -- container for MenuOption components. Manages open/close, submenu chaining, cursor position memory, pause integration, and animation.
- MenuOption.cs -- concrete option types: Slider, Toggle, Menu, Scene, ControlSetup, CloseMenu, SendMessage, Return, DisableCursor, GameSettingList. Handles value editing, rebinding, and click behavior.
- SimpleMenu.cs -- lightweight programmatic menu built on CursorSelectable. Supports dynamic options, sub-menu stacks, grouped options, pointer smoothing, and overlay detection.
- SimpleMenuOption.cs -- data class for SimpleMenu options. Supports title/description, enable conditions, hover events, and grouping via SimpleMenuOptionGroup.
- MenuAutomation.cs -- static system for queued menu animations (node reveals, notifications). Batches consecutive reveals, manages cursor lock/unlock, and fires OnAutomationBegin/OnAutomationEnd so the host app can pause/resume surrounding workflows.
- MenuFramework.cs -- generic support layer: MenuPause (reference-counted pause), MenuInput (input action singleton + helpers), MenuAudio (static clip playback), MenuRoutines (coroutine host), MenuNode (keyed selectable for automation), IMenuRevealGroup (reveal-group contract).
- SimpleGrid.cs -- custom layout system replacing Unity's GridLayoutGroup. Supports Grid, Row, Column, Circle, and Auto (triangle/square/polygon) arrangements with anchor-based positioning.


KEY FEATURES

- Cursor memory: saves and restores cursor position when navigating between menus
- Submenu chaining: parent menus stay visible or hide based on SubMenuClosesParent flag
- Overlay detection: SimpleMenu detects when another menu pauses on top and hides its visuals
- Auto-repeat navigation: initial press moves once, then auto-repeats after 15 frames at 4-frame intervals
- Animation queue: MenuAutomation batches consecutive node reveals sharing the same menu into one open-close cycle
- Pause integration: menus can pause the game on open; SimpleMenu tracks ambient pause sources to distinguish self-pauses from overlay pauses
- Layout shapes: SimpleGrid auto-arranges 1 item (centered), 2 items (horizontal pair), 3 items (triangle), 4 items (square), 5+ items (polygon)
- Platform-aware: options can be disabled on PC or console via DisableOnPC/DisableOnConsole


DEPENDENCIES

Self-contained apart from a few generic UI helpers and Unity's Input System:
- MenuFramework.cs (included) -- MenuPause, MenuInput, MenuAudio, MenuRoutines, MenuNode, IMenuRevealGroup
- GameAnimation -- animation component for show/hide transitions
- TextMeshProEffects -- TMP text with typing/focus animations
- MenuEffectLayer -- UI layer for detached visual effects
- CursorSelectableDropdown -- dropdown integration for selectables
- GameSetting -- persistent settings abstraction (for GameSettingList options)
- ActionSequencer -- coroutine-based task sequencing (drives MenuAutomation)
- SimplePathObject -- not required here; see Unity-GamePath


ORIGIN

Extracted from Wings of Vi 2 (Unity 2D action-platformer) developed with the Grynsoft 2D Engine.


LICENSE

MIT -- see LICENSE file.
