import { getConfig } from './utils/environment.js';
import type { UnityMessage } from './types/index.js';

const config = getConfig();

export class UnityClient {
  private baseUrl: string;
  private isConnectedFlag = false;
  private retryAttempts = 0;
  private maxRetryAttempts = config.retryAttempts;

  constructor() {
    this.baseUrl = `http://localhost:${config.unityPort}`;
  }

  public async connect(): Promise<void> {
    if (this.isConnectedFlag) {
      return;
    }

    try {
      // Test connection with a simple ping
      await this.makeRequest('test', {});
      console.log(`Connected to Unity Editor at ${this.baseUrl}`);
      this.isConnectedFlag = true;
      this.retryAttempts = 0;
    } catch (error) {
      console.error('Unity connection failed:', error);
      
      if (this.retryAttempts < this.maxRetryAttempts) {
        this.retryAttempts++;
        console.log(`Retrying connection (${this.retryAttempts}/${this.maxRetryAttempts})...`);
        
        await new Promise(resolve => setTimeout(resolve, config.retryDelay));
        return this.connect();
      } else {
        throw new Error(`Failed to connect to Unity after ${this.maxRetryAttempts} attempts`);
      }
    }
  }

  public async disconnect(): Promise<void> {
    this.isConnectedFlag = false;
    console.log('Disconnected from Unity Editor');
  }

  public isConnected(): boolean {
    return this.isConnectedFlag;
  }

  public async sendRequest(method: string, params?: Record<string, unknown>, timeoutMs?: number): Promise<unknown> {
    if (!this.isConnectedFlag) {
      throw new Error('Not connected to Unity Editor');
    }

    try {
      return await this.makeRequest(method, params, timeoutMs);
    } catch (error) {
      console.error(`Request failed for method ${method}:`, error);

      // Mark as disconnected on request failure
      this.isConnectedFlag = false;
      throw error;
    }
  }

  public sendNotification(method: string, params?: Record<string, unknown>): void {
    // For HTTP, notifications are the same as requests but we don't wait for response
    this.sendRequest(method, params).catch(error => {
      console.error(`Notification failed for method ${method}:`, error);
    });
  }

  private async makeRequest(method: string, params?: Record<string, unknown>, timeoutMs?: number): Promise<unknown> {
    const message: UnityMessage = {
      id: this.generateId(),
      type: 'request',
      method,
      ...(params && { params }),
    };

    const response = await fetch(this.baseUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(message),
      signal: AbortSignal.timeout(timeoutMs ?? config.requestTimeout),
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    const result = await response.json() as UnityMessage;
    
    if (result.error) {
      throw new Error(result.error.message);
    }

    return result.result;
  }

  private generateId(): string {
    return Math.random().toString(36).substring(2, 15) + 
           Math.random().toString(36).substring(2, 15);
  }
} 