
process.argv = [
  process.argv[0],
  '.agents/skills/archify/bin/archify.mjs',
  'render',
  'architecture',
  '.archify/architecture-netcode-20261115/candidate.json',
  '.archify/architecture-netcode-20261115/project-architecture.html'
];
process.on('uncaughtException', err => {
  console.error('Stack:', err.stack);
  if (err.archifyDiagnostics) console.error('Diagnostics:', JSON.stringify(err.archifyDiagnostics, null, 2));
});
import('./.agents/skills/archify/bin/archify.mjs');
