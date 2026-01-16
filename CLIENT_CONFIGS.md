# Unity MCP Client Configuration Guide

This guide provides configuration examples for connecting Unity MCP Server to various AI IDEs and MCP clients.

## Table of Contents

- [Claude Desktop (Claude Code)](#claude-desktop-claude-code)
- [Claude Code CLI](#claude-code-cli)
- [Cursor](#cursor)
- [Windsurf](#windsurf)
- [Cline (formerly Claude Dev)](#cline-formerly-claude-dev)
- [Environment Variables](#environment-variables)
- [Troubleshooting](#troubleshooting)

---

## Claude Desktop (Claude Code)

### Configuration Location
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

### Configuration Example

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Unity_MCP/Server/build/index.js"],
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

### Setup Steps

1. **Build the server**:
   ```bash
   cd /path/to/Unity_MCP/Server
   npm install
   npm run build
   ```

2. **Edit configuration**:
   - Open the configuration file in your preferred text editor
   - Add the Unity MCP configuration
   - Replace `/absolute/path/to/Unity_MCP` with your actual project path

3. **Restart Claude Desktop**:
   - Completely quit and restart the application
   - The MCP server will start automatically when Claude Desktop launches

4. **Verify connection**:
   - Look for the 🔌 icon in Claude Desktop
   - Click it to see available MCP servers
   - Unity MCP should appear with 40 tools listed

---

## Claude Code CLI

### Configuration Location
- `~/.config/claude-code/mcp_settings.json`

### Configuration Example

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Unity_MCP/Server/build/index.js"],
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090",
        "LOG_LEVEL": "info"
      }
    }
  }
}
```

### Setup Steps

1. **Build the server** (same as Claude Desktop)

2. **Configure Claude Code**:
   ```bash
   # Edit the configuration file
   code ~/.config/claude-code/mcp_settings.json
   ```

3. **Test the connection**:
   ```bash
   # Start Claude Code
   claude-code

   # In the chat, ask about available tools
   "What Unity tools are available?"
   ```

---

## Cursor

### Configuration Location
- **macOS**: `~/Library/Application Support/Cursor/User/globalStorage/cursor-mcp/config.json`
- **Windows**: `%APPDATA%\Cursor\User\globalStorage\cursor-mcp\config.json`
- **Linux**: `~/.config/Cursor/User/globalStorage/cursor-mcp/config.json`

### Configuration Example

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Unity_MCP/Server/build/index.js"],
      "cwd": "/absolute/path/to/Unity_MCP",
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

### Setup Steps

1. **Enable MCP in Cursor**:
   - Open Cursor Settings (Cmd/Ctrl + ,)
   - Search for "MCP" or "Model Context Protocol"
   - Enable MCP support

2. **Build and configure**:
   ```bash
   cd /path/to/Unity_MCP/Server
   npm install
   npm run build
   ```

3. **Add configuration**:
   - Create or edit the config.json file
   - Add the Unity MCP configuration

4. **Restart Cursor**:
   - Completely quit and restart Cursor
   - Check for MCP connection indicator in the status bar

---

## Windsurf

### Configuration Location
- **Project-level**: `<project-root>/.windsurf/mcp.json`
- **Global**: `<project-root>/mcp.json`

### Configuration Example

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

### Setup Steps

1. **Quick setup**:
   ```bash
   cd /path/to/Unity_MCP

   # Install dependencies and build
   cd Server
   npm install
   npm run build
   cd ..

   # Configuration is already included in the project
   ```

2. **Open in Windsurf**:
   - Open the Unity_MCP folder in Windsurf
   - The MCP server will start automatically
   - Look for MCP connection indicator in the IDE

3. **Verify tools**:
   - Ask Windsurf: "What Unity tools are available?"
   - Should see all 40 tools listed

---

## Cline (formerly Claude Dev)

### Configuration Location
- **VS Code Settings**: Settings → Extensions → Cline → MCP Settings
- **Settings file**: `.vscode/settings.json` in your project

### Configuration Example

Add to your `.vscode/settings.json`:

```json
{
  "cline.mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/Unity_MCP/Server/build/index.js"],
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

### Setup Steps

1. **Install Cline extension**:
   - Open VS Code
   - Install "Cline" from the marketplace
   - Enable MCP support in Cline settings

2. **Build and configure**:
   ```bash
   cd /path/to/Unity_MCP/Server
   npm install
   npm run build
   ```

3. **Add configuration**:
   - Open VS Code settings (Cmd/Ctrl + ,)
   - Search for "Cline MCP"
   - Add the Unity MCP configuration
   - Or add directly to `.vscode/settings.json`

4. **Restart VS Code**:
   - Reload the window (Cmd/Ctrl + R)
   - Open Cline panel
   - Check for Unity MCP connection

---

## Environment Variables

All configurations support these environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `NODE_ENV` | `production` | Node environment mode |
| `UNITY_PORT` | `8090` | Unity Editor HTTP server port |
| `LOG_LEVEL` | `info` | Logging level (debug/info/warn/error) |
| `REQUEST_TIMEOUT` | `10` | Request timeout in seconds |
| `RETRY_ATTEMPTS` | `3` | Connection retry attempts |
| `RETRY_DELAY` | `1000` | Delay between retries (ms) |

### Example with Custom Settings

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/path/to/Server/build/index.js"],
      "env": {
        "NODE_ENV": "production",
        "UNITY_PORT": "8090",
        "LOG_LEVEL": "debug",
        "REQUEST_TIMEOUT": "30",
        "RETRY_ATTEMPTS": "5",
        "RETRY_DELAY": "2000"
      }
    }
  }
}
```

---

## Path Configuration

### Absolute vs Relative Paths

**Absolute paths** (recommended for global configs):
```json
"args": ["/Users/username/Projects/Unity_MCP/Server/build/index.js"]
```

**Relative paths** (for project-level configs):
```json
"args": ["./Server/build/index.js"],
"cwd": "."
```

### Platform-Specific Paths

**macOS/Linux**:
```json
"args": ["/home/user/Unity_MCP/Server/build/index.js"]
```

**Windows**:
```json
"args": ["C:\\Users\\Username\\Unity_MCP\\Server\\build\\index.js"]
```

Or use forward slashes (Windows supports this):
```json
"args": ["C:/Users/Username/Unity_MCP/Server/build/index.js"]
```

---

## Troubleshooting

### Server Not Starting

1. **Check build**:
   ```bash
   cd Server
   npm run build
   # Ensure build completes without errors
   ```

2. **Test manually**:
   ```bash
   node Server/build/index.js
   # Should show "Connecting to Unity Editor..."
   ```

3. **Check logs**:
   - Look for error messages in your IDE's MCP logs
   - Set `LOG_LEVEL=debug` for more details

### Connection Issues

1. **Verify Unity server is running**:
   - Open Unity Editor
   - Go to Window → MCP Server
   - Ensure server is started and port matches config

2. **Check firewall**:
   - Allow Node.js through your firewall
   - Ensure localhost connections are permitted

3. **Port conflicts**:
   - If port 8090 is in use, change `UNITY_PORT` in both:
     - MCP config
     - Unity MCP Server window

### Tools Not Appearing

1. **Verify connection**:
   - Look for MCP connection indicator in your IDE
   - Check if Unity MCP is listed

2. **Rebuild server**:
   ```bash
   cd Server
   npm run clean
   npm run build
   ```

3. **Restart everything**:
   - Quit IDE completely
   - Restart Unity Editor
   - Start IDE again

### Path Issues

1. **Use absolute paths** for troubleshooting:
   ```bash
   # Get absolute path
   cd /path/to/Unity_MCP
   pwd  # Copy this path
   ```

2. **Verify paths exist**:
   ```bash
   ls Server/build/index.js
   # Should show the file
   ```

3. **Check permissions**:
   ```bash
   # Ensure file is readable/executable
   chmod +x Server/build/index.js
   ```

---

## Additional Resources

- **Tool Reference**: See [TOOLS_REFERENCE.md](./TOOLS_REFERENCE.md) for complete tool documentation
- **Quick Setup**: See [QUICK_SETUP.md](./QUICK_SETUP.md) for 5-minute setup guide
- **Security**: See [SECURITY_AND_PRIVACY.md](./SECURITY_AND_PRIVACY.md) for security best practices
- **Unity Setup**: See [QUICK_UNITY_SETUP.md](./QUICK_UNITY_SETUP.md) for Unity plugin installation

---

## Testing Your Configuration

Once configured, test your setup with these commands:

1. **List available tools**:
   ```
   "What Unity MCP tools are available?"
   ```

2. **Get project info**:
   ```
   "Use project.getInfo to show me Unity project details"
   ```

3. **Analyze project**:
   ```
   "Analyze my Unity project"
   ```

If these work, your configuration is successful! 🎉
