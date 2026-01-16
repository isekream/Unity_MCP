#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ErrorCode,
  ListToolsRequestSchema,
  McpError,
} from '@modelcontextprotocol/sdk/types.js';
import { UnityClient } from './unity-client.js';
import { logger } from './utils/logger.js';
import { validateEnvironment } from './utils/environment.js';
import { createProjectTools } from './tools/project-tools.js';
import { createSceneTools } from './tools/scene-tools.js';
import { createAssetTools } from './tools/asset-tools.js';
import { createCodeTools } from './tools/code-tools.js';
import { createBuildTools } from './tools/build-tools.js';
import type { Tool } from './types/index.js';

const SERVER_NAME = 'unity-mcp';
const SERVER_VERSION = '1.0.0';

class UnityMCPServer {
  private server: Server;
  private unityClient: UnityClient;
  private tools: Map<string, Tool> = new Map();

  constructor() {
    this.server = new Server(
      {
        name: SERVER_NAME,
        version: SERVER_VERSION,
      },
      {
        capabilities: {
          tools: {},
        },
      }
    );

    this.unityClient = new UnityClient();
    this.setupTools();
    this.setupHandlers();
  }

  private setupTools(): void {
    // Register all available tools
    const toolCategories = [
      createProjectTools(this.unityClient),
      createSceneTools(this.unityClient),
      createAssetTools(this.unityClient),
      createCodeTools(this.unityClient),
      createBuildTools(this.unityClient),
    ];

    for (const tools of toolCategories) {
      for (const tool of tools) {
        this.tools.set(tool.name, tool);
      }
    }

    logger.info(`Registered ${this.tools.size} tools`);
  }

  private setupHandlers(): void {
    // List available tools
    this.server.setRequestHandler(ListToolsRequestSchema, async () => {
      logger.debug('Listing available tools');
      
      return {
        tools: Array.from(this.tools.values()).map(tool => ({
          name: tool.name,
          description: tool.description,
          inputSchema: tool.inputSchema,
        })),
      };
    });

    // Handle tool execution
    this.server.setRequestHandler(CallToolRequestSchema, async request => {
      const { name, arguments: args } = request.params;
      
      logger.info(`Executing tool: ${name}`);
      logger.debug(`Tool arguments:`, args);

      const tool = this.tools.get(name);
      if (!tool) {
        throw new McpError(
          ErrorCode.MethodNotFound,
          `Tool '${name}' not found`
        );
      }

      try {
        // Ensure Unity connection is available
        if (!this.unityClient.isConnected()) {
          await this.unityClient.connect();
        }

        const result = await tool.execute(args ?? {});
        
        logger.debug(`Tool ${name} executed successfully`);
        
        return {
          content: [
            {
              type: 'text',
              text: typeof result === 'string' ? result : JSON.stringify(result, null, 2),
            },
          ],
        };
      } catch (error) {
        logger.error(`Error executing tool ${name}:`, error);
        
        throw new McpError(
          ErrorCode.InternalError,
          `Failed to execute tool '${name}': ${error instanceof Error ? error.message : String(error)}`
        );
      }
    });

    // Error handling
    this.server.onerror = (error: Error) => {
      logger.error('MCP Server error:', error);
    };

    // Process error handling
    process.on('SIGINT', async () => {
      logger.info('Received SIGINT, shutting down gracefully...');
      await this.shutdown();
      process.exit(0);
    });

    process.on('SIGTERM', async () => {
      logger.info('Received SIGTERM, shutting down gracefully...');
      await this.shutdown();
      process.exit(0);
    });
  }

  public async start(): Promise<void> {
    try {
      // Validate environment
      validateEnvironment();

      // Connect to Unity
      logger.info('Connecting to Unity Editor...');
      await this.unityClient.connect();
      logger.info('Connected to Unity Editor successfully');

      // Start MCP server
      const transport = new StdioServerTransport();
      await this.server.connect(transport);
      
      logger.info(`${SERVER_NAME} v${SERVER_VERSION} started successfully`);
      logger.info(`Available tools: ${Array.from(this.tools.keys()).join(', ')}`);
      
    } catch (error) {
      logger.error('Failed to start server:', error);
      throw error;
    }
  }

  public async shutdown(): Promise<void> {
    try {
      logger.info('Shutting down server...');
      
      // Close Unity connection
      await this.unityClient.disconnect();
      
      // Close MCP server
      await this.server.close();
      
      logger.info('Server shut down complete');
    } catch (error) {
      logger.error('Error during shutdown:', error);
    }
  }
}

// Main execution
async function main(): Promise<void> {
  const server = new UnityMCPServer();

  try {
    await server.start();
  } catch (error) {
    logger.error('Failed to start Unity MCP Server:', error);
    process.exit(1);
  }
}

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason, promise) => {
  logger.error('Unhandled promise rejection at:', promise, 'reason:', reason);
  process.exit(1);
});

// Handle uncaught exceptions
process.on('uncaughtException', (error: Error) => {
  logger.error('Uncaught exception:', error);
  process.exit(1);
});

// Start the server
if (import.meta.url === `file://${process.argv[1]}`) {
  void main();
} 