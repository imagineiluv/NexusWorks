#!/usr/bin/env node

import { promises as fs } from 'node:fs';
import path from 'node:path';

function parseArgs(argv) {
  const args = {};

  for (let index = 0; index < argv.length; index += 1) {
    const current = argv[index];

    if (current === '--artifacts-dir') {
      args.artifactsDir = argv[++index];
      continue;
    }

    if (current === '--manifest') {
      args.manifest = argv[++index];
      continue;
    }

    if (current === '--output') {
      args.output = argv[++index];
      continue;
    }

    throw new Error(`Unknown argument: ${current}`);
  }

  if (!args.artifactsDir && !args.manifest) {
    throw new Error('--artifacts-dir or --manifest is required');
  }

  return args;
}

function formatSize(size) {
  if (size < 1024) {
    return `${size} B`;
  }

  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`;
  }

  if (size < 1024 * 1024 * 1024) {
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  }

  return `${(size / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

function buildMetadataLines(metadata) {
  const lines = [];

  for (const [key, value] of Object.entries(metadata)) {
    lines.push(`- ${key}: ${value}`);
  }

  if (lines.length === 0) {
    lines.push('- none');
  }

  return lines.join('\n');
}

function buildFileTable(files) {
  const lines = [
    '| File | Size | SHA-256 |',
    '|---|---:|---|'
  ];

  for (const file of files) {
    lines.push(`| ${file.path} | ${formatSize(file.size)} | \`${file.sha256}\` |`);
  }

  return lines.join('\n');
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const artifactsDir = path.resolve(args.artifactsDir ?? path.dirname(args.manifest));
  const manifestPath = path.resolve(args.manifest ?? path.join(artifactsDir, 'manifest.json'));
  const outputPath = path.resolve(args.output ?? path.join(artifactsDir, 'release-summary.md'));
  const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
  const content = [
    '# Release Summary',
    '',
    `- platform: ${manifest.platform || 'unknown'}`,
    `- generatedAtUtc: ${manifest.generatedAtUtc || 'unknown'}`,
    `- artifactsDir: ${manifest.artifactsDir || artifactsDir}`,
    `- fileCount: ${manifest.fileCount ?? 0}`,
    '',
    '## Metadata',
    '',
    buildMetadataLines(manifest.metadata ?? {}),
    '',
    '## Files',
    '',
    buildFileTable(manifest.files ?? []),
    ''
  ].join('\n');

  await fs.writeFile(outputPath, content, 'utf8');
  process.stdout.write(`${outputPath}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
});
