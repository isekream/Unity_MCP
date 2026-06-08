const http = require('http');

function sendRequest(method, params) {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({
      id: `test-${Date.now()}`,
      type: 'request',
      method,
      params,
    });

    const req = http.request(
      {
        hostname: 'localhost',
        port: 8090,
        path: '/',
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Content-Length': Buffer.byteLength(body),
        },
        timeout: 30000,
      },
      (res) => {
        let response = '';
        res.on('data', (chunk) => (response += chunk));
        res.on('end', () => {
          try {
            resolve(JSON.parse(response));
          } catch (e) {
            reject(new Error(`Invalid JSON: ${response.slice(0, 200)}`));
          }
        });
      }
    );

    req.on('error', reject);
    req.on('timeout', () => {
      req.destroy();
      reject(new Error('Request timed out'));
    });
    req.write(body);
    req.end();
  });
}

async function run() {
  console.log('Scene-building smoke test (Unity must be running on :8090)\n');

  const ping = await sendRequest('test', {});
  console.log('✅ ping:', ping.result?.status ?? ping.result);

  const slope = await sendRequest('scene.createGameObject', {
    name: 'MCP_Slope',
    primitive: 'Plane',
    scale: { x: 10, y: 1, z: 20 },
  });
  const slopeData = slope.result?.data ?? slope.result;
  console.log('✅ createGameObject (primitive):', slopeData?.name, 'id=', slopeData?.instanceId);

  const skier = await sendRequest('scene.createPrimitive', {
    name: 'MCP_Skier',
    primitiveType: 'Capsule',
    position: { x: 0, y: 2, z: 0 },
  });
  const skierData = skier.result?.data ?? skier.result;
  console.log('✅ createPrimitive:', skierData?.name, 'id=', skierData?.instanceId);

  const addRb = await sendRequest('scene.modifyComponent', {
    gameObjectName: 'MCP_Skier',
    action: 'add',
    componentType: 'Rigidbody',
  });
  console.log('✅ add Rigidbody:', addRb.result?.message ?? addRb.result);

  const modRb = await sendRequest('scene.modifyComponent', {
    gameObjectName: 'MCP_Skier',
    action: 'modify',
    componentType: 'Rigidbody',
    properties: { mass: 70, useGravity: true },
  });
  console.log('✅ modify Rigidbody:', modRb.result?.message ?? modRb.result);

  const query = await sendRequest('scene.query', { filter: 'MCP_' });
  const objects = query.result?.data?.objects ?? query.result?.objects ?? [];
  console.log('✅ query:', objects.length, 'root(s) matching MCP_');

  if (typeof skierData?.instanceId !== 'number') {
    throw new Error(`instanceId should be number, got ${JSON.stringify(skierData?.instanceId)}`);
  }

  console.log('\n🎉 Scene-building smoke test passed');
}

run().catch((err) => {
  console.error('❌', err.message);
  console.error('Start Unity → Tools > Unity MCP > Server Window → Start Server');
  process.exit(1);
});