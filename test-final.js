const http = require('http');

// Test the project.analyze tool after the threading fix
const testData = JSON.stringify({
  id: 'final-test',
  type: 'request',
  method: 'project.analyze',
  params: {
    includeAssets: false,
    includePackages: true,
    includeScenes: true,
    includeSettings: true
  }
});

const options = {
  hostname: 'localhost',
  port: 8090,
  path: '/',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': testData.length
  }
};

console.log('🔧 Testing Unity MCP after threading fix...');

const req = http.request(options, (res) => {
  let response = '';
  res.on('data', (chunk) => response += chunk);
  res.on('end', () => {
    console.log('\n📋 Unity Response:');
    try {
      const parsed = JSON.parse(response);
      if (parsed.error) {
        console.log('❌ Error:', parsed.error.message);
      } else if (parsed.result) {
        console.log('✅ SUCCESS! Unity tools working properly');
        console.log('📊 Project Info:');
        const data = parsed.result.data ?? parsed.result;
        if (data.project) {
          console.log(`   - Project: ${data.project.projectName}`);
          console.log(`   - Unity Version: ${data.project.unityVersion}`);
          console.log(`   - Platform: ${data.project.platform}`);
        }
        if (data.scenes) {
          console.log(`   - Total Scenes: ${data.scenes.totalScenes}`);
          console.log(`   - Active Scene: ${data.scenes.activeScene}`);
        }
        console.log('\n🎉 MCP Integration is working correctly!');
      } else {
        console.log('⚠️  Unexpected response format');
      }
    } catch (e) {
      console.log('❌ Could not parse response:', e.message);
      console.log('Raw response:', response);
    }
  });
});

req.on('error', (e) => {
  console.error('❌ Request failed:', e.message);
  console.log('Make sure Unity is running with the MCP server started');
});

req.write(testData);
req.end(); 