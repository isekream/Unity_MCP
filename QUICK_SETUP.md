# 🚀 Quick Unity MCP Setup Guide

Get Unity MCP running with all 40 tools in 5 minutes!

## ✅ Prerequisites Check

- [ ] Unity 2022.3+ installed
- [ ] Node.js 18+ installed
- [ ] An MCP-compatible AI IDE installed (Claude Code, Cursor, Windsurf, Cline, etc.)

## 🎯 Step-by-Step Setup

### 1. Build the Node.js Server (2 minutes)

```bash
cd Windsurf_Unity_MCP/Server
npm install
npm run build
```

### 2. Configure Your AI IDE (2 minutes)

**Choose your IDE and follow the configuration:**

<details>
<summary><b>Claude Desktop (Claude Code)</b></summary>

Add to `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):
```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Windsurf_Unity_MCP/Server/build/index.js"],
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      }
    }
  }
}
```
</details>

<details>
<summary><b>Cursor</b></summary>

Add to `~/Library/Application Support/Cursor/User/globalStorage/cursor-mcp/config.json` (macOS):
```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Windsurf_Unity_MCP/Server/build/index.js"],
      "cwd": "/absolute/path/to/Windsurf_Unity_MCP",
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      }
    }
  }
}
```
</details>

<details>
<summary><b>Windsurf</b></summary>

Add to `~/.config/windsurf/mcp.json` or `<project-root>/.windsurf/mcp.json`:
```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["./Server/build/index.js"],
      "cwd": ".",
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      },
      "description": "Unity Editor MCP integration - 40 tools for project, scene, asset, code, and build management",
      "enabled": true
    }
  }
}
```
</details>

<details>
<summary><b>Cline (VS Code Extension)</b></summary>

Add to `.vscode/settings.json` in your project:
```json
{
  "cline.mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Windsurf_Unity_MCP/Server/build/index.js"],
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      }
    }
  }
}
```
</details>

**📖 For detailed configuration, see [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md)**

### 3. Install Unity Plugin (1 minute)

1. Open Unity Editor with any project
2. Go to `Window > Package Manager`
3. Click `+` → `Add package from git URL...`
4. Enter: `https://github.com/isekream/Windsurf_Unity_MCP.git?path=/Unity`
5. Click `Add`

### 4. Start Unity MCP Server (30 seconds)

1. In Unity: `Window > MCP Server`
2. Click **`Start Server`**
3. Verify status shows **"Running"** (green) at `http://localhost:8090`
4. Check that **40 tools** are registered

### 5. Test Connection (1 minute)

Run this test command in terminal (replace path with your actual project location):
```bash
cd <PATH_TO_WINDSURF_UNITY_MCP>
python3 unity_mcp_client.py project.getInfo
```

Expected output: Project information JSON (not timeout/connection errors)

### 6. Restart Your AI IDE (30 seconds)

1. Close your AI IDE completely
2. Reopen your IDE
3. Unity MCP tools should now be available

## 🎉 You're Ready!

### Test These Commands in Your AI IDE:

- "What Unity MCP tools are available?"
- "Analyze my Unity project structure"
- "Create a cube in the scene"
- "List all scenes in the project"
- "Show me the project build settings"

### All 40 Available Tools:

#### Project (7 tools)
- `project.analyze` - Full project analysis
- `project.getInfo` - Basic project info
- `project.setSettings` - Change project settings
- `project.listScenes` - List all scenes
- `project.getBuildSettings` - Get build config
- `project.setBuildTarget` - Change build platform
- `project.refreshAssets` - Refresh asset database

#### Scene (8 tools)  
- `scene.createGameObject` - Create objects
- `scene.modifyComponent` - Edit components
- `scene.query` - Find objects
- `scene.selectObjects` - Select objects
- `scene.deleteGameObject` - Delete objects
- `scene.moveGameObject` - Move/transform
- `scene.save` - Save scenes
- `scene.load` - Load scenes

#### Assets (8 tools)
- `assets.import` - Import files
- `assets.createMaterial` - Make materials
- `assets.managePrefabs` - Handle prefabs
- `assets.organize` - Organize folders
- `assets.search` - Find assets
- `assets.createTexture` - Make textures
- `packages.manage` - Handle packages
- `assets.getInfo` - Asset details

#### Code (8 tools)
- `code.createScript` - Generate scripts
- `code.analyzeScripts` - Analyze code
- `code.attachScripts` - Attach to objects
- `code.findReferences` - Find usage
- `code.refactor` - Refactor code
- `code.generateDocumentation` - Make docs
- `code.validate` - Check code quality
- `code.format` - Format code

#### Build (9 tools)
- `build.configure` - Setup builds
- `build.execute` - Run builds
- `build.runTests` - Run tests
- `build.getReport` - Build reports
- `build.clean` - Clean cache
- `build.addressables` - Addressables
- `build.optimize` - Optimize
- `build.getConsoleLogs` - Console logs
- `build.profile` - Performance

## 🐛 Troubleshooting

### "Connection refused"
- ✅ Unity Editor is running
- ✅ Unity MCP Server shows "Running" 
- ✅ Port 8090 is available

### "Tool execution timed out"
- ✅ Unity Editor is responsive (not frozen)
- ✅ Check Unity Console for errors
- ✅ Increase timeout in Unity MCP settings

### "MCP not appearing in IDE"
- ✅ Restart your AI IDE completely
- ✅ Check MCP configuration file syntax (see [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md))
- ✅ Verify paths in configuration are correct and absolute
- ✅ Ensure Server/build/index.js exists (run `npm run build`)

### "Tool not found"
- ✅ Unity MCP Server window shows all tools registered
- ✅ Use correct tool format: `category.action`
- ✅ Check Node.js server logs

## 📞 Quick Support

**Test Connection**: `curl -X POST http://localhost:8090 -H "Content-Type: application/json" -d '{"id":"test","type":"request","method":"project.getInfo","params":{}}'`

**View All Tools**: Open Unity MCP Server Window in Unity Editor

**Debug Mode**: Enable detailed logging in Unity MCP Server settings

---

**🎯 Success Criteria**: You can run `python3 unity_mcp_client.py project.analyze` from your project directory and get project details (not errors). 