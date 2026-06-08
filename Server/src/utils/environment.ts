import type { ServerConfig } from '../types/index.js';

export function validateEnvironment(): void {
  // No required environment variables for now - all have defaults
  const requiredEnvVars: Record<string, string> = {};

  const missing: string[] = [];

  for (const [envVar, description] of Object.entries(requiredEnvVars)) {
    if (!process.env[envVar]) {
      missing.push(`${envVar} - ${description}`);
    }
  }

  if (missing.length > 0) {
    console.error('Missing required environment variables:');
    for (const item of missing) {
      console.error(`  - ${item}`);
    }
    throw new Error('Environment validation failed');
  }
}

export function getConfig(): ServerConfig {
  return {
    unityPort: parseInt(process.env.UNITY_PORT ?? '8090', 10),
    requestTimeout: parseInt(process.env.REQUEST_TIMEOUT ?? '10', 10) * 1000,
    logLevel: (process.env.LOG_LEVEL as ServerConfig['logLevel']) ?? 'info',
    retryAttempts: parseInt(process.env.RETRY_ATTEMPTS ?? '3', 10),
    retryDelay: parseInt(process.env.RETRY_DELAY ?? '1000', 10),
  };
}
