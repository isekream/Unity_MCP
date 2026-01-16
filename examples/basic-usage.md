# Basic Usage Examples

This document provides examples of how to use the Unity MCP integration for common Unity development tasks with AI IDEs (Claude Code, Cursor, Windsurf, Cline, etc.).

## Project Analysis

### Get Project Overview
```
"Analyze my Unity project and show me the key information"
```

This will return comprehensive information about:
- Project settings and configuration
- Scene structure and build settings
- Package dependencies
- Asset breakdown by type
- Performance metrics

### Detailed Asset Analysis
```
"Analyze my Unity project including all assets and their sizes"
```

This provides detailed asset information including file sizes and types.

## Scene Management

### Query Scene Hierarchy
```
"Show me all GameObjects in the current scene"
```

### Create GameObjects
```
"Create a new GameObject called 'Player' with a Rigidbody component"
```

### Modify Scene Objects
```
"Add a MeshRenderer component to the Player GameObject"
```

## Build Operations

### Build for Windows
```
"Build my Unity project for Windows 64-bit"
```

### Development Build
```
"Create a development build for Windows with script debugging enabled"
```

### Build with Custom Settings
```
"Build the project for Android with these settings: development build enabled, connect profiler enabled"
```

## Asset Management

### Import Assets
```
"Import the texture file at path 'Assets/Textures/player_texture.png'"
```

### Create Materials
```
"Create a new Standard material called 'PlayerMaterial' with red color"
```

### Manage Prefabs
```
"Create a prefab from the Player GameObject and save it as 'Assets/Prefabs/Player.prefab'"
```

## Code Generation

### Create Scripts
```
"Create a new MonoBehaviour script called 'PlayerController' with Start and Update methods"
```

### Analyze Code
```
"Analyze all C# scripts in the Assets/Scripts folder for dependencies and complexity"
```

## Advanced Examples

### Batch Operations
```
"Analyze the project, then build it for Windows, and finally create a build report"
```

### Conditional Operations
```
"If the project has any build errors, show them to me, otherwise proceed with the build"
```

### Project Setup
```
"Set up a new Unity project with these settings: company name 'MyCompany', product name 'MyGame', version '1.0.0'"
```

## Tips for Better Results

1. **Be Specific**: Include exact names, paths, and parameters when possible
2. **Use Unity Terminology**: Use Unity-specific terms like "GameObject", "Component", "Scene"
3. **Specify Platforms**: When building, specify the target platform clearly
4. **Include Context**: Mention if you want development builds, specific settings, etc.
5. **Chain Commands**: You can ask for multiple operations in sequence

## Error Handling

If a command fails, the AI will provide:
- Clear error messages
- Suggestions for fixing the issue
- Alternative approaches

Common issues and solutions:
- **Unity not in Edit Mode**: Some operations require Unity to be in Edit Mode, not Play Mode
- **Missing Dependencies**: Ensure all required packages are installed
- **Path Issues**: Use forward slashes and relative paths from the Assets folder
- **Permission Issues**: Ensure Unity has write permissions for build outputs

## Getting Help

You can always ask:
- "What Unity tools are available?"
- "How do I build for iOS?"
- "What's the current status of my Unity project?"
- "Show me the available MCP commands for Unity" 