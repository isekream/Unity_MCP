# 🎮 Unity MCP Integration Guide for Windsurf

**For Windsurf IDE**: Complete reference for Unity MCP tools integration.

## 🎯 Available Unity MCP Tools (40 Total)

### Quick Tool List for Windsurf AI

When users ask for Unity operations, these tools are available through the `unity-mcp` server:

#### **Project Management (7 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Project Analysis | `project.analyze` | Full project structure analysis |
| Project Info | `project.getInfo` | Basic project information |
| Project Settings | `project.setSettings` | Modify project configuration |
| Scene List | `project.listScenes` | List all project scenes |
| Build Settings | `project.getBuildSettings` | Get build configuration |
| Build Target | `project.setBuildTarget` | Change build platform |
| Refresh Assets | `project.refreshAssets` | Refresh asset database |

#### **Scene Operations (8 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Create Objects | `scene.createGameObject` | Create GameObjects with components |
| Modify Components | `scene.modifyComponent` | Add/edit/remove components |
| Query Scene | `scene.query` | Find objects in scene |
| Select Objects | `scene.selectObjects` | Select objects in Unity |
| Delete Objects | `scene.deleteGameObject` | Remove GameObjects |
| Move Objects | `scene.moveGameObject` | Transform objects |
| Save Scene | `scene.save` | Save current scene |
| Load Scene | `scene.load` | Open different scene |

#### **Asset Management (8 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Import Assets | `assets.import` | Import files into project |
| Create Material | `assets.createMaterial` | Generate materials |
| Manage Prefabs | `assets.managePrefabs` | Create/modify prefabs |
| Organize Assets | `assets.organize` | Folder organization |
| Search Assets | `assets.search` | Find assets by criteria |
| Create Texture | `assets.createTexture` | Generate textures |
| Package Manager | `packages.manage` | Install/update packages |
| Asset Info | `assets.getInfo` | Get asset details |

#### **Code Generation (8 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Create Script | `code.createScript` | Generate C# scripts |
| Analyze Scripts | `code.analyzeScripts` | Parse existing code |
| Attach Scripts | `code.attachScripts` | Attach scripts to objects |
| Find References | `code.findReferences` | Find code usage |
| Refactor Code | `code.refactor` | Rename/move code |
| Generate Docs | `code.generateDocumentation` | Auto-generate docs |
| Validate Code | `code.validate` | Check code quality |
| Format Code | `code.format` | Format code style |

#### **Build & Deploy (9 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Configure Build | `build.configure` | Setup build settings |
| Execute Build | `build.execute` | Run builds |
| Run Tests | `build.runTests` | Execute tests |
| Build Report | `build.getReport` | Get build statistics |
| Clean Build | `build.clean` | Clean cache/temp files |
| Addressables | `build.addressables` | Manage addressables |
| Optimize Build | `build.optimize` | Optimize performance |
| Console Logs | `build.getConsoleLogs` | Get Unity console |
| Profile Build | `build.profile` | Performance profiling |

## 🤖 Natural Language Mapping for Windsurf AI

### Common User Requests → Tool Usage

**Project Analysis:**
- "Analyze my Unity project" → `project.analyze`
- "What's in this project?" → `project.getInfo`
- "Show project structure" → `project.analyze` with full parameters

**Scene Building:**
- "Create a cube" → `scene.createGameObject` with cube primitive
- "Add physics to player" → `scene.modifyComponent` with Rigidbody
- "Delete all enemies" → `scene.query` + `scene.deleteGameObject`

**Asset Creation:**
- "Make a red material" → `assets.createMaterial` with red color
- "Import these textures" → `assets.import` with file paths
- "Organize my assets" → `assets.organize` with folder operations

**Code Development:**
- "Create a player controller" → `code.createScript` with MonoBehaviour
- "Find all PlayerController usage" → `code.findReferences`
- "Generate enemy AI script" → `code.createScript` with AI template

**Building:**
- "Build for Android" → `build.setBuildTarget` + `build.execute`
- "Run all tests" → `build.runTests`
- "Optimize my build" → `build.optimize`

## 📋 Tool Parameter Examples

### Essential Parameter Patterns

#### Creating GameObjects:
```json
{
  "name": "Player",
  "primitive": "Capsule",
  "position": {"x": 0, "y": 1, "z": 0},
  "components": ["Rigidbody", "CapsuleCollider"]
}
```

#### Creating Materials:
```json
{
  "materialName": "PlayerMaterial",
  "properties": {
    "albedo": {"r": 0.8, "g": 0.2, "b": 0.2, "a": 1.0},
    "metallic": 0.5,
    "smoothness": 0.8
  }
}
```

#### Creating Scripts:
```json
{
  "scriptName": "PlayerController",
  "scriptType": "MonoBehaviour",
  "methods": ["Start", "Update", "FixedUpdate"],
  "namespace": "Game.Player"
}
```

#### Project Analysis:
```json
{
  "includeAssets": true,
  "includePackages": true,
  "includeScenes": true,
  "includeSettings": true
}
```

## 🔧 Windsurf Implementation Notes

### Tool Availability Check
Before using Unity tools, verify:
1. Unity Editor is running
2. Unity MCP Server shows "Running" status
3. Port 8090 is accessible

### Error Handling
Common error patterns:
- `"Connection refused"` → Unity not running
- `"Tool execution timed out"` → Unity busy/frozen
- `"Tool 'X' not found"` → Wrong tool name format

### Response Format
All tools return:
```json
{
  "Id": "request-id",
  "Type": "response",
  "Result": { "success": true, "data": {} },
  "Error": null
}
```

### Tool Categories for Context
- **Project**: Settings, configuration, analysis
- **Scene**: GameObjects, components, hierarchy
- **Assets**: Materials, textures, prefabs, import
- **Code**: Scripts, analysis, documentation
- **Build**: Compilation, testing, deployment

## 🎯 Windsurf User Experience Guidelines

### Suggested AI Responses

**For "Analyze project":**
> "I'll analyze your Unity project structure using the project.analyze tool. This will give us information about scenes, assets, packages, and settings."

**For "Create a player character":**
> "I'll create a player character by:
> 1. Creating a capsule GameObject with scene.createGameObject
> 2. Adding physics components with scene.modifyComponent  
> 3. Generating a PlayerController script with code.createScript
> 4. Attaching the script with code.attachScripts"

**For "Build for mobile":**
> "I'll configure your project for mobile deployment:
> 1. Setting Android build target with project.setBuildTarget
> 2. Configuring build settings with build.configure
> 3. Executing the build with build.execute"

### Best Practices for Windsurf
1. **Always verify Unity connection** before tool usage
2. **Use descriptive parameter names** for clarity
3. **Chain related operations** for complex workflows
4. **Provide error context** when operations fail
5. **Suggest alternatives** if primary tools fail

## 📞 Integration Support

**Server URL**: `http://localhost:8090`  
**Protocol**: HTTP POST with JSON  
**Timeout**: 30 seconds (configurable)  
**Error Codes**: Standard HTTP + Unity-specific errors

---

**🎯 For Windsurf AI**: Use these tools to provide comprehensive Unity development assistance through natural language commands. All 40 tools are production-ready and cover the complete Unity development workflow. 