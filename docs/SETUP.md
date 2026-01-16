# Unity MCP Setup Guide

This guide will help you set up the Unity MCP integration for seamless Unity Editor control from MCP-compatible AI IDEs (Claude Code, Cursor, AI IDE, Cline, etc.).

## Prerequisites

- **Unity Editor 2022.3 LTS or later**
- **Node.js 18+ and npm**
- **An MCP-compatible AI IDE** (Claude Code, Cursor, AI IDE, Cline, etc.)
- **Git** for version control

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/isekream/AI IDE_Unity_MCP.git
cd AI IDE_Unity_MCP
```

### 2. Install Node.js Dependencies

```bash
cd Server
npm install
```

### 3. Build the MCP Server

```bash
npm run build
```

### 4. Install Unity Package

1. Open Unity Editor
2. Open your Unity project
3. In the Package Manager, click the `+` button
4. Select "Add package from disk..."
5. Navigate to `AI IDE_Unity_MCP/Unity/package.json`
6. Click "Open"

Alternatively, you can copy the `Unity` folder contents to your project's `Packages` directory.

## Configuration

### 1. Unity Setup

1. Open Unity Editor
2. Go to `Window > MCP Server`
3. Configure the HTTP port (default: 8090)
4. Enable "Auto Start Server" if desired
5. Click "Start Server"

The Unity MCP server should now be running and ready to accept connections.

### 2. AI IDE Configuration

Configuration varies by IDE. See [CLIENT_CONFIGS.md](../CLIENT_CONFIGS.md) for detailed setup instructions for:
- Claude Desktop (Claude Code)
- Claude Code CLI
- Cursor
- AI IDE
- Cline

**Example Configuration** (Generic):

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["/absolute/path/to/AI IDE_Unity_MCP/Server/build/index.js"],
      "env": {
        "UNITY_PORT": "8090",
        "REQUEST_TIMEOUT": "10"
      }
    }
  }
}
```

**Note**: Replace `/absolute/path/to/AI IDE_Unity_MCP` with your actual project path.

### 3. Environment Variables

You can configure the MCP server using these environment variables:

- `UNITY_PORT`: WebSocket port for Unity connection (default: 8090)
- `REQUEST_TIMEOUT`: Request timeout in seconds (default: 10)
- `LOG_LEVEL`: Logging level (debug, info, warn, error)

## Verification

### 1. Test Unity Connection

1. Ensure Unity Editor is running with the MCP server started
2. Check the Unity MCP Server window for connection status
3. Server should show "Running" status

### 2. Test AI IDE Integration

1. Open your AI IDE
2. Try asking the AI assistant: "What Unity MCP tools are available?"
3. Try: "Analyze my Unity project"
3. The AI should be able to provide information about your Unity project

### 3. Available Commands

Once set up, you can use natural language commands like:

- "Analyze my Unity project structure"
- "Create a new GameObject called 'Player'"
- "Build the project for Windows"
- "Show me the current scene hierarchy"
- "Create a new C# script for player movement"

## Troubleshooting

### Common Issues

#### Unity Server Won't Start

- **Check port availability**: Ensure port 8090 (or your configured port) is not in use
- **Firewall settings**: Make sure your firewall allows connections on the configured port
- **Unity console errors**: Check the Unity Console for any error messages

#### AI IDE Can't Connect

- **Server running**: Verify the Unity MCP server is running (green status in Unity)
- **Path configuration**: Double-check the path to the MCP server in AI IDE config
- **Node.js version**: Ensure you're using Node.js 18 or later
- **Build status**: Make sure you've built the server with `npm run build`

#### Commands Not Working

- **Tool registration**: Check the Unity MCP Server window for registered tools
- **Parameter format**: Ensure you're using the correct parameter format for tools
- **Unity state**: Some tools require Unity to be in Edit Mode (not Play Mode)

### Debug Mode

Enable debug logging by setting the environment variable:

```bash
export LOG_LEVEL=debug
```

Or in AI IDE configuration:

```json
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": ["./Server/build/index.js"],
      "env": {
        "UNITY_PORT": "8090",
        "LOG_LEVEL": "debug"
      }
    }
  }
}
```

### Getting Help

1. **Check logs**: Review both Unity Console and AI IDE logs for error messages
2. **Unity MCP Server window**: Use the built-in logging and export functionality
3. **GitHub Issues**: Report bugs or ask questions on the project repository
4. **Documentation**: Refer to the API documentation for advanced usage

## Next Steps

- Explore the [API Documentation](API.md) for advanced usage
- Check out [Examples](../examples/) for common use cases
- Contribute to the project by adding new tools or features

## Security Considerations

- The MCP server runs locally and only accepts connections from localhost
- WebSocket communication is unencrypted (suitable for local development)
- Be cautious when running automated build or file operations
- Consider firewall rules if working in a shared network environment 