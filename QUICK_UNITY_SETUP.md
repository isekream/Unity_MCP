# Quick Unity MCP Setup for New Projects

## ✅ Global MCP is Now Configured!

Your Unity MCP is globally configured and will work with **ALL Unity projects**.

## 🚀 For Any New Unity Project (2-Minute Setup):

### 1. Open Unity Editor with your project

### 2. Install the Unity MCP Package
- Go to `Window > Package Manager`
- Click the `+` button → `Add package from git URL...`
- Paste this URL:
  ```
  https://github.com/isekream/Unity_MCP.git?path=/Unity
  ```
- Click `Add`

### 3. Start the Unity MCP Server
- Go to `Window > MCP Server`
- Click `Start Server`
- Verify it shows "Status: Running" and "HTTP URL: http://localhost:8090"

### 4. Restart Your AI IDE
- Close and reopen your AI IDE (Claude Code, Cursor, Windsurf, Cline, etc.)
- The Unity MCP tools will now be available

## 🎉 That's It!

You can now use your AI IDE with natural language commands like:
- "List all scenes in the project"
- "Create a new cube in the current scene"
- "Build the project for Windows"
- "Show me the project structure"
- "Create a player controller script"

## 🔧 Global Configuration Details

**MCP Server Location**: `<PROJECT_FOLDER>/Server/build/index.js`
**Configuration Files**: See [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for your specific IDE

## 💡 Pro Tips

1. **Keep this folder**: Don't move or delete the `Unity_MCP` folder - the global config points to it
2. **Auto-start Unity server**: Enable "Auto Start Server" in the Unity MCP Server Window for convenience
3. **Check Unity Console**: If something doesn't work, check Unity Console for error messages
4. **Port conflicts**: If port 8090 is busy, change it in the Unity Server Window

## 🐛 Troubleshooting

If MCP doesn't appear in Windsurf Tools:
1. Verify Unity MCP server is running (green status)
2. Restart Windsurf IDE completely
3. Check `~/.config/windsurf/mcp.json` exists and has correct path
4. Run diagnostic: `node <PROJECT_FOLDER>/test-connection.js` 