# AnchorComponent

The `AnchorComponent` provides a lightweight anchoring system for entities that allows them to maintain their position relative to screen edges when the window is resized.

## Features

- **Zero Memory Overhead**: No memory allocation when anchoring is disabled (default state)
- **Flexible Anchoring**: Anchor to any combination of screen edges (left, right, top, bottom)
- **Automatic Updates**: Automatically repositions entities when window is resized
- **Runtime Control**: Enable, disable, or modify anchoring behavior at runtime
- **Easy Integration**: Follows existing component patterns in the helengine framework

## Basic Usage

### 1. Add AnchorComponent to an Entity

```csharp
var entity = new Entity();
entity.Position = new float3(100, 50, 0);
entity.InitComponents();

// Add the anchor component
var anchor = new AnchorComponent();
entity.AddComponent(anchor);
```

### 2. Enable Anchoring

```csharp
// Anchor to top-left corner
anchor.EnableAnchoring(left: true, top: true);

// Anchor to bottom-right corner
anchor.EnableAnchoring(right: true, bottom: true);

// Anchor to left edge only
anchor.EnableAnchoring(left: true);

// Anchor to all edges (stretches with window)
anchor.EnableAnchoring(left: true, right: true, top: true, bottom: true);
```

### 3. Manual Distance Control

```csharp
// Set specific distances from edges
anchor.SetAnchorDistances(
    left: 20,    // 20px from left edge
    top: 10      // 10px from top edge
);
```

### 4. Runtime Management

```csharp
// Check if anchoring is active
if (anchor.IsAnchored) {
    Console.WriteLine(anchor.GetAnchorInfo());
}

// Disable anchoring
anchor.DisableAnchoring();
```

## Common Use Cases

### UI Button in Top-Right Corner
```csharp
var button = new Entity();
var windowSize = Core.Instance.RenderManager3D.MainWindowSize;
button.Position = new float3(windowSize.X - 120, 10, 0);

var anchor = new AnchorComponent();
button.AddComponent(anchor);
anchor.EnableAnchoring(right: true, top: true);
```

### Status Bar at Bottom
```csharp
var statusBar = new Entity();
var windowSize = Core.Instance.RenderManager3D.MainWindowSize;
statusBar.Position = new float3(0, windowSize.Y - 30, 0);

var anchor = new AnchorComponent();
statusBar.AddComponent(anchor);
anchor.EnableAnchoring(left: true, bottom: true);
```

### Centered Dialog
```csharp
var dialog = new Entity();
var windowSize = Core.Instance.RenderManager3D.MainWindowSize;
dialog.Position = new float3(windowSize.X / 2, windowSize.Y / 2, 0);

var anchor = new AnchorComponent();
dialog.AddComponent(anchor);
anchor.SetAnchorDistances(
    left: windowSize.X / 2,
    top: windowSize.Y / 2
);
```

## Implementation Details

- **Memory Efficient**: The `AnchorData` class is only instantiated when anchoring is enabled
- **Event-Driven**: Uses `RenderManager3D.WindowResized` events for automatic updates
- **Priority System**: When both left/right or top/bottom anchors are set, left and top take priority
- **Automatic Cleanup**: Unsubscribes from events when component is removed or anchoring is disabled

## API Reference

### Properties
- `bool IsAnchored` - Returns true if anchoring is currently enabled

### Methods
- `EnableAnchoring(bool left, bool right, bool top, bool bottom)` - Enable anchoring to specified edges
- `DisableAnchoring()` - Disable anchoring and clean up resources
- `SetAnchorDistances(float? left, float? right, float? top, float? bottom)` - Set specific distances from edges
- `string GetAnchorInfo()` - Get human-readable description of current anchor configuration

### Events
- Automatically subscribes to `Core.Instance.RenderManager3D.WindowResized` when anchoring is enabled
- Automatically unsubscribes when anchoring is disabled or component is removed
