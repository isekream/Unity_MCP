import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createPlayModeTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'playmode.getState',
      description:
        'Get the current Unity Play Mode state (stopped, playing, paused). Use this to check whether the game is running before issuing play mode commands.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.getState', args);
        return result;
      },
    },

    {
      name: 'playmode.enter',
      description:
        'Enter Unity Play Mode to run the game. After entering, use scene.capture to visually verify what the running game looks like, and playmode.getConsoleLogs to check for runtime errors. This enables a full code→test→see→fix development loop.',
      inputSchema: {
        type: 'object',
        properties: {
          maximizeGameView: {
            type: 'boolean',
            description: 'Whether to maximize the Game view when entering Play Mode',
            default: false,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.enter', args);
        return result;
      },
    },

    {
      name: 'playmode.exit',
      description:
        'Exit Unity Play Mode and return to Edit Mode. Always exit Play Mode before making code or scene changes.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.exit', args);
        return result;
      },
    },

    {
      name: 'playmode.pause',
      description:
        'Pause the running game. The game freezes in its current state, allowing inspection of runtime values and viewport capture of a specific moment.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.pause', args);
        return result;
      },
    },

    {
      name: 'playmode.resume',
      description: 'Resume a paused game.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.resume', args);
        return result;
      },
    },

    {
      name: 'playmode.step',
      description:
        'Advance the paused game by exactly one frame. Useful for debugging frame-by-frame behavior, physics issues, or animation transitions.',
      inputSchema: {
        type: 'object',
        properties: {
          frames: {
            type: 'number',
            description: 'Number of frames to step (default 1). The game pauses after stepping.',
            default: 1,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.step', args);
        return result;
      },
    },

    {
      name: 'playmode.inspectGameObject',
      description:
        'Inspect a GameObject\'s runtime state during Play Mode. Returns live component values (transforms, physics velocities, custom script fields) — not just the serialized editor values. Essential for debugging runtime behavior.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject to inspect',
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)',
          },
          componentFilter: {
            type: 'array',
            items: { type: 'string' },
            description:
              'Only return these component types (e.g., ["Transform", "Rigidbody", "PlayerController"]). If empty, returns all components.',
          },
          includeChildren: {
            type: 'boolean',
            description: 'Whether to include child GameObjects in the inspection',
            default: false,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.inspectGameObject', args);
        return result;
      },
    },

    {
      name: 'playmode.setProperty',
      description:
        'Modify a component property on a GameObject at runtime during Play Mode. Changes are temporary and reset when exiting Play Mode. Useful for live-tuning values like speed, health, or positions.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject',
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)',
          },
          componentType: {
            type: 'string',
            description: 'Type of the component to modify (e.g., "Transform", "Rigidbody", "PlayerController")',
          },
          propertyName: {
            type: 'string',
            description: 'Name of the property or field to set (e.g., "position", "velocity", "speed")',
          },
          value: {
            description:
              'New value for the property. Can be a number, string, boolean, or object (e.g., {"x": 0, "y": 5, "z": 0} for Vector3).',
          },
        },
        required: ['componentType', 'propertyName', 'value'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.setProperty', args);
        return result;
      },
    },

    {
      name: 'playmode.invokeMethod',
      description:
        'Call a public method on a component during Play Mode. Useful for triggering game actions like TakeDamage(), Respawn(), or LoadLevel() to test specific behaviors.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject',
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)',
          },
          componentType: {
            type: 'string',
            description: 'Component type containing the method',
          },
          methodName: {
            type: 'string',
            description: 'Name of the public method to invoke',
          },
          arguments: {
            type: 'array',
            description: 'Arguments to pass to the method (in order)',
            items: {},
          },
        },
        required: ['componentType', 'methodName'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.invokeMethod', args);
        return result;
      },
    },

    {
      name: 'playmode.getConsoleLogs',
      description:
        'Get Unity console logs captured during Play Mode, including runtime errors, exceptions, warnings, and Debug.Log messages. Essential for diagnosing runtime issues after entering Play Mode.',
      inputSchema: {
        type: 'object',
        properties: {
          logTypes: {
            type: 'array',
            items: {
              type: 'string',
              enum: ['Log', 'Warning', 'Error', 'Assert', 'Exception'],
            },
            description: 'Types of logs to retrieve',
            default: ['Log', 'Warning', 'Error', 'Exception'],
          },
          limit: {
            type: 'number',
            description: 'Maximum number of log entries to return',
            default: 50,
          },
          sinceLastCall: {
            type: 'boolean',
            description: 'Only return logs since the last call to this tool (useful for incremental monitoring)',
            default: false,
          },
          search: {
            type: 'string',
            description: 'Filter logs containing this text (case-insensitive)',
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.getConsoleLogs', args);
        return result;
      },
    },

    {
      name: 'playmode.getRuntimeInfo',
      description:
        'Get runtime performance and game state information during Play Mode: FPS, frame time, active object count, physics stats, memory usage, and time info (elapsed, timeScale, frame count).',
      inputSchema: {
        type: 'object',
        properties: {
          includePerformance: {
            type: 'boolean',
            description: 'Include FPS, frame time, and memory stats',
            default: true,
          },
          includePhysics: {
            type: 'boolean',
            description: 'Include physics simulation info (contacts, rigidbody count)',
            default: true,
          },
          includeTime: {
            type: 'boolean',
            description: 'Include time info (elapsed, timeScale, frame count)',
            default: true,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.getRuntimeInfo', args);
        return result;
      },
    },

    {
      name: 'playmode.setTimeScale',
      description:
        'Set the game time scale during Play Mode. Use 0 to freeze time (different from pause — scripts still run), 0.5 for slow-motion, 1 for normal, 2+ for fast-forward. Great for testing animations or debugging timing issues.',
      inputSchema: {
        type: 'object',
        properties: {
          timeScale: {
            type: 'number',
            description: 'Time scale value (0 = frozen, 1 = normal, 2 = double speed)',
          },
        },
        required: ['timeScale'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.setTimeScale', args);
        return result;
      },
    },
  ];
}
