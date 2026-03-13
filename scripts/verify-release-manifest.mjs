#!/usr/bin/env node

import { createHash } from 'node:crypto';
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

    throw new Error(`Unknown argument: ${current}`);
  }

  if (!args.artifactsDir && !args.manifest) {
    throw new Error('--artifacts-dir or --manifest is required');
  }

  return args;
}

async function sha256ForFile(filePath) {
  const content = await fs.readFile(filePath);
  return createHash('sha256').update(content).digest('hex');
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const artifactsDir = path.resolve(args.artifactsDir ?? path.dirname(args.manifest));
  const manifestPath = path.resolve(args.manifest ?? path.join(artifactsDir, 'manifest.json'));
  const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
  const failures = [];

  for (const file of manifest.files ?? []) {
    const fullPath = path.join(artifactsDir, file.path);

    try {
      const stats = await fs.stat(fullPath);

      if (!stats.isFile()) {
        failures.push(`${file.path}: not a regular file`);
        continue;
      }

      if (stats.size !== file.size) {
        failures.push(`${file.path}: size mismatch (expected ${file.size}, actual ${stats.size})`);
        continue;
      }

      const actualHash = await sha256ForFile(fullPath);

      if (actualHash !== file.sha256) {
        failures.push(`${file.path}: sha256 mismatch (expected ${file.sha256}, actual ${actualHash})`);
      }
    }
    catch (error) {
      failures.push(`${file.path}: ${error.message}`);
    }
  }

  if ((manifest.files ?? []).length !== manifest.fileCount) {
    failures.push(`manifest fileCount mismatch (expected ${manifest.fileCount}, actual ${(manifest.files ?? []).length})`);
  }

  if (failures.length > 0) {
    process.stderr.write(`manifest verification failed for ${manifestPath}\n`);
    for (const failure of failures) {
      process.stderr.write(`- ${failure}\n`);
    }
    process.exitCode = 1;
    return;
  }

  process.stdout.write(`manifest verification passed: ${manifestPath}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.message}\n`);
  process.exitCode = 1;
});
