# 🎮 Unity MCP Server

A powerful Model Context Protocol (MCP) server that enables seamless Unity Editor integration with AI IDEs (Claude Code, Cursor, Windsurf, Cline, and more). Control Unity projects through natural language commands powered by AI.

## ✨ Features

- 🎯 **40+ Unity Tools** - Comprehensive project, scene, asset, code, and build management
- 🔄 **Real-time Integration** - Direct Unity Editor communication via HTTP API
- 🤖 **AI-Powered** - Natural language commands for complex Unity operations
- 🛠️ **Extensible** - Easy to add custom tools for specific project needs
- 📱 **Cross-Platform** - Works on Windows, macOS, and Linux
- 🚀 **Zero Config** - Auto-discovery and setup for new Unity projects

## 🏗️ Architecture

```
AI IDE (Claude Code/Cursor/Windsurf/Cline) ←→ Node.js MCP Server ←→ Unity Editor C# Plugin
                    │                                 │                        │
                AI Commands                    Protocol Bridge            Unity API
```

### Components

1. **Node.js MCP Server** (`Server/`) - Handles MCP client communication and tool routing
2. **Unity C# Plugin** (`Unity/`) - Executes commands directly in Unity Editor
3. **Python Client** (`unity_mcp_client.py`) - Test client for development

### Supported AI IDEs

- ✅ **Claude Desktop** (Claude Code) - Official Anthropic desktop app
- ✅ **Claude Code CLI** - Command-line interface
- ✅ **Cursor** - AI-powered code editor
- ✅ **Windsurf** - AI development environment
- ✅ **Cline** (formerly Claude Dev) - VS Code extension
- ✅ **Any MCP-compatible client** - Standard MCP protocol

## 🚀 Quick Start

### Prerequisites

- Unity 2022.3+
- Node.js 18+
- An MCP-compatible AI IDE (Claude Code, Cursor, Windsurf, Cline, etc.)

### Installation

1. **Clone this repository**:
   ```bash
   git clone https://github.com/isekream/Windsurf_Unity_MCP.git
   cd Windsurf_Unity_MCP
   ```

2. **Install Node.js dependencies**:
   ```bash
   cd Server
   npm install
   npm run build
   cd ..
   ```

3. **Configure Your AI IDE**:

   Configuration varies by IDE. See [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for detailed examples.

   **Quick Example** (Claude Desktop):
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

   **For other IDEs**, see [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md):
   - Claude Code CLI
   - Cursor
   - Windsurf
   - Cline

4. **Install Unity Plugin**:
   - Open Unity Editor with your project
   - Go to `Window > Package Manager`
   - Click `+` → `Add package from git URL...`
   - Enter: `https://github.com/isekream/Windsurf_Unity_MCP.git?path=/Unity`

5. **Start the Unity MCP Server**:
   - In Unity: `Window > MCP Server`
   - Click `Start Server`
   - Verify status shows "Running" at http://localhost:8090

6. **Restart Your AI IDE** to load the MCP server

## 🎯 Available Tools (40 Total)

### Project Management (6 tools)
- `project.analyze` - Comprehensive project analysis and structure overview
- `project.getInfo` - Get basic project information and settings
- `project.setSettings` - Modify project configuration and player settings
- `project.listScenes` - List all scenes in build settings with status
- `project.getBuildSettings` - Get current build configuration
- `project.setBuildTarget` - Change active build target platform
- `project.refreshAssets` - Force refresh of project assets database

### Scene Operations (8 tools)
- `scene.createGameObject` - Create GameObjects with components and hierarchy
- `scene.modifyComponent` - Add, remove, or update component properties
- `scene.query` - Query scene hierarchy and find objects by criteria
- `scene.selectObjects` - Select and focus objects in Scene/Hierarchy view
- `scene.deleteGameObject` - Remove GameObjects and children safely
- `scene.moveGameObject` - Transform position, rotation, scale, and parenting
- `scene.save` - Save current scene or all open scenes
- `scene.load` - Open and switch between project scenes

### Asset Management (8 tools)
- `assets.import` - Import files with custom import settings
- `assets.createMaterial` - Generate materials with shader properties
- `assets.managePrefabs` - Create, modify, and instantiate prefab assets
- `assets.organize` - Folder operations, moving, and organizing project assets
- `assets.search` - Find assets by name, type, or properties
- `assets.createTexture` - Generate procedural textures and import images
- `packages.manage` - Install, update, and remove Unity packages
- `assets.getInfo` - Get detailed information about selected assets

### Code Generation (8 tools)
- `code.createScript` - Generate C# scripts from templates or descriptions
- `code.analyzeScripts` - Parse existing scripts and extract information
- `code.attachScripts` - Attach MonoBehaviour scripts to GameObjects
- `code.findReferences` - Find script and component usage across project
- `code.refactor` - Rename, move, and restructure code safely
- `code.generateDocumentation` - Auto-generate XML documentation
- `code.validate` - Check scripts for common issues and best practices
- `code.format` - Format and style code according to conventions

### Build & Deploy (10 tools)
- `build.configure` - Set build settings, scenes, and platform options
- `build.execute` - Trigger builds for target platforms
- `build.runTests` - Execute Unity Test Runner and get results
- `build.getReport` - Get detailed build reports and statistics
- `build.clean` - Clean build cache and temporary files
- `build.addressables` - Manage Addressable Asset System configuration
- `build.optimize` - Analyze and optimize build size and performance
- `build.getConsoleLogs` - Retrieve Unity Console messages and errors
- `build.profile` - Performance profiling and optimization suggestions

## 💡 Usage Examples

### Natural Language Commands

Ask your AI IDE:

```
"Analyze my Unity project and show me the structure"
→ Executes: project.analyze

"Create a player character with movement controls in the scene"
→ Executes: scene.createGameObject, code.createScript, code.attachScripts

"Build my project for Android with development settings"
→ Executes: build.configure, build.setBuildTarget, build.execute

"Find all scripts that use the PlayerController component"
→ Executes: code.findReferences

"Create a fire particle system with appropriate materials"
→ Executes: scene.createGameObject, assets.createMaterial, assets.managePrefabs
```

### Direct Tool Testing

You can test tools directly using the Python client:

```bash
# Analyze project structure
python3 unity_mcp_client.py project.analyze

# Get project info
python3 unity_mcp_client.py project.getInfo

# Create a cube in the scene
python3 unity_mcp_client.py scene.createGameObject '{"name": "TestCube", "primitive": "Cube"}'

# List all scenes
python3 unity_mcp_client.py project.listScenes

# Get build settings
python3 unity_mcp_client.py project.getBuildSettings
```

## 🔧 Configuration

### Unity Configuration

1. **Server Settings**: 
   - Port: 8090 (default, configurable)
   - Timeout: 10 seconds (configurable)
   - Auto-start: Optional for convenience

2. **Tool Categories**:
   - All tools are auto-registered on Unity Editor startup
   - Custom tools can be added by extending `McpToolBase`

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `UNITY_PORT` | Unity Editor HTTP server port | `8090` |
| `NODE_ENV` | Node.js environment mode | `production` |
| `REQUEST_TIMEOUT` | Tool execution timeout (seconds) | `10` |
| `LOG_LEVEL` | Logging verbosity | `info` |

## 🏃‍♂️ Development

### Adding Custom Tools

1. **Create Unity Tool** (C#):
   ```csharp
   public class MyCustomTool : McpToolBase
   {
       public override string ToolName => "custom_mytool";
       public override string Description => "My custom Unity tool";
       public override string Category => "custom";
       
       public override object Execute(object parameters)
       {
           // Your Unity API calls here
           return CreateSuccessResponse(result);
       }
   }
   ```

2. **Register in Unity Server**:
   ```csharp
   // In McpUnityServer.InitializeTools()
   RegisterTool(new MyCustomTool());
   ```

3. **Add Node.js Tool** (TypeScript):
   ```typescript
   // In Server/src/tools/custom-tools.ts
   export const myCustomTool: Tool = {
     name: 'custom.mytool',
     description: 'My custom Unity tool',
     inputSchema: { /* JSON schema */ },
     handler: async (params) => {
       return await unityClient.sendRequest('custom.mytool', params);
     }
   };
   ```

### Building and Testing

```bash
# Build Node.js server
cd Server
npm run build

# Run tests
npm test

# Start development server with hot reload
npm run dev

# Test specific tool
python3 unity_mcp_client.py your.tool '{"param": "value"}'
```

## 🐛 Troubleshooting

### Common Issues

1. **"Connection refused"**:
   - Ensure Unity Editor is running
   - Check Unity MCP Server window shows "Running"
   - Verify port 8090 is not blocked

2. **"Tool execution timed out"**:
   - Unity Editor may be busy or not responding
   - Increase timeout in Unity MCP Server settings
   - Check Unity Console for errors

3. **"Tool not found"**:
   - Verify tool is registered in Unity C# server
   - Check Node.js server logs for tool registration
   - Ensure tool name format matches (category.action)

4. **MCP not appearing in your IDE**:
   - Restart your AI IDE after configuration changes
   - Check MCP configuration file syntax (see [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md))
   - Verify Node.js server builds without errors

### Diagnostic Commands

```bash
# Test Unity connection
curl -X POST http://localhost:8090 -H "Content-Type: application/json" -d '{"id":"test","type":"request","method":"project.getInfo","params":{}}'

# Check Node.js server status
node Server/build/index.js --test

# Validate MCP configuration (adjust path for your IDE)
# Claude Desktop (macOS):
cat ~/Library/Application\ Support/Claude/claude_desktop_config.json | jq .
# Windsurf:
cat ~/.config/windsurf/mcp.json | jq .
```

## 📁 Project Structure

```
Windsurf_Unity_MCP/
├── Server/                    # Node.js MCP Server
│   ├── src/                   # TypeScript source code
│   │   ├── tools/            # Tool implementations
│   │   ├── unity-client.ts   # Unity communication client
│   │   └── index.ts          # Main server entry point
│   ├── build/                # Compiled JavaScript
│   └── package.json          # Node.js dependencies
├── Unity/                     # Unity C# Plugin
│   ├── Editor/               # Unity Editor scripts
│   │   ├── Tools/           # Individual MCP tools
│   │   └── McpUnityServer.cs # Main Unity HTTP server
│   └── package.json          # Unity package definition
├── unity_mcp_client.py       # Python test client
├── mcp.json                  # Local MCP configuration
└── README.md                 # This documentation
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-tool`)
3. Add your tool following the established patterns
4. Write tests and documentation
5. Submit a pull request

### Tool Development Guidelines

- Follow Unity C# coding conventions
- Use TypeScript for Node.js components
- Include comprehensive error handling
- Add parameter validation and helpful error messages
- Document all public APIs and tool parameters
- Test tools with various Unity project configurations

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Unity Technologies for the comprehensive Editor API
- Anthropic for the Model Context Protocol (MCP) specification
- AI IDE teams (Claude Code, Cursor, Windsurf, Cline) for MCP integration
- Open source contributors and beta testers

## 📞 Support

- 🐛 **Issues**: [GitHub Issues](https://github.com/isekream/Windsurf_Unity_MCP/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/isekream/Windsurf_Unity_MCP/discussions)
- 📧 **Email**: support@windsurfmcp.com
- 📖 **Docs**: [Wiki](https://github.com/isekream/Windsurf_Unity_MCP/wiki)

---

**Made with ❤️ for the Unity and AI development community** 