// Manual control of the GO gate: node tools/hooks/gate-cli.mjs grant <sessionId> | revoke <sessionId> | status <sessionId>
import { grant, isGranted, loadConfig, revoke } from './gate-core.mjs';

const [command, sessionId] = process.argv.slice(2);

if (!sessionId || !['grant', 'revoke', 'status'].includes(command)) {
  console.error('usage: gate-cli.mjs grant|revoke|status <sessionId>');
  process.exit(1);
}

if (command === 'grant') grant(sessionId);
if (command === 'revoke') revoke(sessionId);
console.log(`${command} ${sessionId}: ${isGranted(sessionId, loadConfig()) ? 'GO' : 'locked'}`);
