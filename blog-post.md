# Building a Unity MCP Server: Vibe-Driven Development in 2026

I shipped a Unity integration for Windsurf IDE that lets you control Unity Editor through natural language. Here's what I learned building with AI as my coding partner instead of just a autocomplete tool.

## The Initial Spark

The problem was simple: Unity Editor is powerful but clunky. Every time I wanted to create a GameObject, configure physics, or analyze a scene, I had to click through menus or write boilerplate. Meanwhile, Windsurf (my IDE) supports MCP (Model Context Protocol) - a way to give AI agents real superpowers through structured tools.

The vibe: **What if I could just tell my IDE "create a player character with movement controls" and it would execute that directly in Unity?**

No switching windows. No manual clicking. Just describe what you want, and the AI handles the execution through a proper protocol.

That's it. That was the entire spec when I started. I didn't draw architecture diagrams. I didn't write a requirements doc. I just knew I wanted to bridge Windsurf's AI to Unity's Editor API through MCP.

## The Stack & Flow

I ended up with a three-layer architecture that emerged from the requirements:

```
Windsurf IDE ←→ Node.js MCP Server ←→ Unity Editor C# Plugin
     │                    │                        │
  AI Commands      Protocol Bridge            Unity API
```

### The Pieces

1. **Node.js MCP Server** (`Server/`) - Receives MCP requests from Windsurf, forwards them to Unity
2. **Unity C# Plugin** (`Unity/`) - HTTP server running inside Unity Editor that executes the actual Unity API calls
3. **40 Tools** - Structured operations across five categories: Project, Scene, Assets, Code, and Build

Why this stack? Because MCP servers speak Node.js by default, and Unity Editor speaks C#. The HTTP bridge was the simplest way to connect them without fighting either ecosystem.

### Vibe Coding in Practice

Here's what "vibe coding" meant for this project: I described *what* I wanted (tool categories, API patterns) and let the AI generate the boilerplate. For example:

- **Me**: "Create tools for scene manipulation: create GameObjects, modify components, query hierarchy"
- **AI**: Generates TypeScript tool definitions with proper JSON schemas + C# implementations extending `McpToolBase`

The AI handled the repetitive stuff (40 tool files, each with schema validation and error handling). I focused on architecture decisions and fixing the weird edge cases.

**Key insight**: The architecture naturally followed a pattern because I let the AI generate similar structures repeatedly. Every tool follows the same base class pattern, same response format, same error handling. This consistency wasn't from upfront planning - it emerged from prompting for similar things over and over.

## The "Stochastic" Hurdles

Not everything went smooth. Here are the moments where the AI and I weren't speaking the same language:

### Hurdle #1: The Main Thread Deadlock

**The Bug**: HTTP requests would hang forever. No error. No timeout. Just... nothing.

**What Happened**: Unity's Editor API *must* run on the main thread. But HTTP requests come in on background threads. So my tools were trying to create GameObjects from a background thread, Unity said "nope," and the whole thing locked up.

**The AI Hallucination**: Initial implementations didn't account for Unity's threading model at all. The AI generated straightforward HTTP handlers that looked correct but would deadlock the entire editor.

This was commit `424dbe7` - the fix that made the project actually work:

```csharp
// Execute tool on main thread using EditorApplication.delayCall
EditorApplication.delayCall += () =>
{
    try
    {
        result = McpResponse.CreateSuccess(tool.Execute(parameters));
    }
    catch (Exception e)
    {
        resultException = e;
    }
    finally
    {
        isComplete = true;
    }
};

// Wait for completion (with timeout)
var startTime = DateTime.Now;
var timeout = TimeSpan.FromSeconds(requestTimeout);

while (!isComplete && DateTime.Now - startTime < timeout)
{
    Thread.Sleep(10);
}
```

This wasn't something the AI suggested. I had to debug why Unity was freezing, understand `EditorApplication.delayCall`, and manually write the timeout mechanism. The AI gave me structure; I gave it the domain knowledge.

### Hurdle #2: Tool Naming Chaos

**The Bug**: Tools were registered as `create_game_object` in Unity but called as `scene.createGameObject` from Node.js. Mismatches everywhere.

**What Happened**: The AI generated different naming conventions for TypeScript vs C# without realizing they needed to map to each other. TypeScript used camelCase namespaces, C# used snake_case, and the bridge code was doing string parsing that failed half the time.

**The Fix**: Commit `6478748` - standardized everything to dot notation: `scene.createGameObject`, `assets.import`, `build.execute`. Simple regex transforms on both sides.

```typescript
// Node.js side
name: 'scene.createGameObject'

// Unity side
public override string ToolName => "scene_createGameObject";

// Bridge parsing
var methodParts = request.Method.Split('.');
string toolName = $"{methodParts[0]}_{methodParts[1]}";
```

Again, the AI didn't spot this inconsistency. It generated perfectly valid code in isolation. I had to see the pattern across 40 tools and enforce consistency.

### Hurdle #3: Hardcoded Paths Everywhere

**The Bug**: Config files and docs had my local paths (`/Users/isekream/Projects/...`) baked in.

**What Happened**: When generating example configs, the AI used the current working directory as context. Totally reasonable for examples, but it meant every config file was personalized to my machine.

**The Fix**: Commit `6a31e89` - replaced all absolute paths with placeholders like `/path/to/Windsurf_Unity_MCP`. Obvious in retrospect, but easy to miss when you're moving fast.

## The Breakthroughs

Two moments turned this from "kind of works on my machine" to "actually ships."

### Breakthrough #1: The Threading Pattern

Once I understood Unity's main thread requirement, I created a pattern that worked across all tools:

```csharp
public static McpResponse ExecuteTool(string toolName, object parameters)
{
    McpResponse result = null;
    bool isComplete = false;

    // Marshal to main thread
    EditorApplication.delayCall += () =>
    {
        result = McpResponse.CreateSuccess(tool.Execute(parameters));
        isComplete = true;
    };

    // Wait with timeout
    while (!isComplete && !timedOut)
        Thread.Sleep(10);

    return result;
}
```

**The Golden Prompt**:
> "Modify the ExecuteTool method to marshal all Unity API calls to the main thread using EditorApplication.delayCall, and add a timeout mechanism that prevents hanging if Unity is busy."

This one prompt fixed 40 tools at once. The AI understood the pattern and applied it everywhere correctly.

### Breakthrough #2: Schema-Driven Tool Generation

Instead of manually writing 40 tool files, I realized I could describe the pattern once and let the AI replicate it:

**The Golden Prompt**:
> "Create a tool that follows this pattern: TypeScript definition with name, description, JSON schema validation, and an execute function that forwards to Unity. Unity side should extend McpToolBase with ToolName, Description, Category, and Execute method. Generate tools for: project.analyze, scene.createGameObject, assets.import, code.createScript, build.execute."

Result: Clean, consistent code across the entire tool suite. Each tool follows the same structure:

```typescript
{
  name: 'scene.createGameObject',
  description: 'Create a new GameObject in the current scene...',
  inputSchema: { /* JSON Schema */ },
  async execute(args) {
    return await unityClient.sendRequest('scene.createGameObject', args);
  }
}
```

The AI isn't creative here - it's *consistent*. And for infrastructure code, consistency is better than creativity.

## Engineering in 2026

Here's what this project taught me about building with AI agents:

### 1. Vibes Work, But You Need a Mental Model

I started with vibes ("bridge Unity to Windsurf"), but I had to understand *how MCP works* and *how Unity threading works* to debug when things broke. The AI can't fix problems it doesn't understand.

**The shift**: AI is great at *implementing* patterns you define, but *you* still need to understand the domain. I needed to know Unity's main thread model existed before I could prompt for a fix.

### 2. Stochastic Bugs Are Real

The AI will generate code that works in isolation but breaks when combined. Tool naming worked perfectly when tested individually. The deadlock only appeared when tools were called via HTTP.

**The pattern**: Generate fast, test integration early. Don't wait until you have 40 tools to discover they all have the same threading bug.

### 3. Prompts Are Code

I realized I was essentially writing "meta-code" - prompts that generate implementations. The more precise my prompts, the better the output:

- ❌ "Create scene tools"
- ✅ "Create tools for scene manipulation (create, modify, delete GameObjects) with JSON schemas for validation, extending McpToolBase, and returning standardized success/error responses"

The second prompt gave me production-ready code. The first gave me a starting point that needed heavy editing.

### 4. AI as an Agentic Layer vs Autocomplete

This project wasn't about tab-completion. It was about giving the AI *agency* - the ability to execute actions in Unity on my behalf through a structured protocol.

Building the MCP server taught me how to *manage* AI agents:
- Define clear tool interfaces (JSON schemas)
- Handle errors gracefully (timeout mechanisms)
- Provide rich context in responses (success/error objects with timestamps)

These patterns apply whether you're building MCP servers or using MCP-enabled tools. The agent layer needs *structure* to be reliable.

## What I Shipped

- **40 production-ready Unity tools** across Project, Scene, Asset, Code, and Build categories
- **Natural language Unity control** - "create a player with physics" just works
- **Real-time bidirectional communication** between Windsurf and Unity
- **Full documentation** and setup guides

GitHub: [Windsurf_Unity_MCP](https://github.com/isekream/Windsurf_Unity_MCP)

The initial commit was 42 files. The main thread fix was 45 lines. The difference between "doesn't work" and "ships" was understanding *one* Unity threading concept.

That's vibe coding in 2026: Start with intent, let AI handle structure, debug the stochastic gaps, and ship.
