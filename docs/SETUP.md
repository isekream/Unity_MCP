# Unity MCP Setup Guide

> **⚠️ This is commercial software.** A paid license is required to use Unity MCP. Unauthorized use is prohibited. Purchase a license at support@unitymcp.com before proceeding.

This guide helps you set up Unity MCP for use with any MCP-compatible AI client.

## Prerequisites

- Unity Editor 2022.3 LTS or later
- Node.js 18+ and npm
- An MCP-compatible AI client
- Git

## Installation Steps

1. Clone this repository and build the server (requires commercial license access):

   ```bash
   git clone https://github.com/isekream/Unity_MCP.git
   cd Unity_MCP/Server
   npm install
   npm run build
   cd ..
   ```

2. Install the Unity package in your project via Package Manager → "Add package from git URL" using:

   `https://github.com/isekream/Unity_MCP.git?path=/Unity` (license required)

3. Start the Unity MCP Server window inside the Unity Editor and click Start.

4. Add the server to your AI client's MCP configuration (see CLIENT_CONFIGS.md).

5. Restart your AI client.

For the fastest path, see [QUICK_SETUP.md](../QUICK_SETUP.md).
