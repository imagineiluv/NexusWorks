#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { promises as fs } from 'node:fs';
import path from 'node:path';

function parseArgs(argv) {
  const args = {
    metadata: {}
  };

  for (let index = 0; index < argv.length; index += 1) {
    const current = argv[index];

    if (current === '--artifacts-dir') {
      args.artifactsDir = argv[++index];
      continue;
    }

    if (current === '--platform') {
      args.platform = argv[++index];
      continue;
    }

    if (current === '--output') {
      args.output = argv[++index];
      continue;
    }

    if (current === '--metadata') {
      const entry = argv[++index];
      const separatorIndex = entry.indexOf('=');

      if (separatorIndex <= 0) {
        throw new Error(`Invalid metadata entry: ${entry}`);
      }

      const key = entry.slice(0, separatorIndex);
      const value = entry.slice(separatorIndex + 1);
      args.metadata[key] = value;
      continue;
    }

    throw new Error(`Unknown argument: ${current}`);
  }

  if (!args.artifactsDir) {
    throw new Error('--artifacts-dir is required');
  }

  return args;
}

async function collectFiles(rootDir, currentDir = rootDir) {
  const entries = await fs.readdir(currentDir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = path.join(currentDir, entry.name);

    if (entry.isDirectory()) {
      files.push(...await collectFiles(rootDir, fullPath));
      continue;
    }

    if (!entry.isFile()) {
      continue;
    }

    const relativePath = path.relative(rootDir, fullPath).split(path.sep).join('/');

    if (relativePath === 'manifest.json') {
      continue;
    }

    const content = await fs.readFile(fullPath);
    const hash = createHash('sha256').update(content).digest('hex');
    const stats = await fs.stat(fullPath);

    files.push({
      path: relativePath,
      size: stats.size,
      sha256: hash
    });
  }

  files.sort((left, right) => left.path.localeCompare(right.path));
  return files;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const artifactsDir = path.resolve(args.artifactsDir);
  const outputPath = path.resolve(args.output ?? path.join(artifactsDir, 'manifest.json'));
  const files = await collectFiles(artifactsDir);
  const manifest = {
    platform: args.platform ?? '',
    generatedAtUtc: new Date().toISOString(),
    artifactsDir,
    fileCount: files.length,
    metadata: args.metadata,
    files
  };

  await fs.writeFile(outputPath, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
  process.stdout.write(`${outputPath}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
});
