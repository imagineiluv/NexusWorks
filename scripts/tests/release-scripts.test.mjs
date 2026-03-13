#!/usr/bin/env node

import test from 'node:test';
import assert from 'node:assert/strict';
import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';

const repoRoot = '/Users/imagineiluv/Documents/GitHub/NexusWorks';
const writeManifestScript = path.join(repoRoot, 'scripts', 'write-release-manifest.mjs');
const verifyManifestScript = path.join(repoRoot, 'scripts', 'verify-release-manifest.mjs');
const writeSummaryScript = path.join(repoRoot, 'scripts', 'write-release-summary.mjs');

function runNodeScript(scriptPath, args, options = {}) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [scriptPath, ...args], {
      cwd: options.cwd ?? repoRoot,
      env: options.env ?? process.env
    });

    let stdout = '';
    let stderr = '';

    child.stdout.on('data', (chunk) => {
      stdout += chunk;
    });

    child.stderr.on('data', (chunk) => {
      stderr += chunk;
    });

    child.on('close', (code) => {
      resolve({
        code,
        stdout,
        stderr
      });
    });
  });
}

async function createArtifactsDir() {
  const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'guardian-release-test-'));
  await fs.writeFile(path.join(tempDir, 'artifact-a.txt'), 'alpha\n', 'utf8');
  await fs.mkdir(path.join(tempDir, 'nested'));
  await fs.writeFile(path.join(tempDir, 'nested', 'artifact-b.txt'), 'beta\n', 'utf8');
  return tempDir;
}

test('write-release-manifest and verify-release-manifest succeed for unchanged artifacts', async () => {
  const artifactsDir = await createArtifactsDir();
  const writeResult = await runNodeScript(writeManifestScript, [
    '--artifacts-dir', artifactsDir,
    '--platform', 'macos',
    '--metadata', 'configuration=Release',
    '--metadata', 'workflow=release-mac'
  ]);

  assert.equal(writeResult.code, 0, writeResult.stderr);

  const manifestPath = path.join(artifactsDir, 'manifest.json');
  const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));

  assert.equal(manifest.platform, 'macos');
  assert.equal(manifest.fileCount, 2);
  assert.equal(manifest.metadata.workflow, 'release-mac');

  const verifyResult = await runNodeScript(verifyManifestScript, [
    '--artifacts-dir', artifactsDir
  ]);

  assert.equal(verifyResult.code, 0, verifyResult.stderr);
  assert.match(verifyResult.stdout, /manifest verification passed/);
});

test('verify-release-manifest fails after an artifact changes', async () => {
  const artifactsDir = await createArtifactsDir();
  const writeResult = await runNodeScript(writeManifestScript, [
    '--artifacts-dir', artifactsDir,
    '--platform', 'windows'
  ]);

  assert.equal(writeResult.code, 0, writeResult.stderr);

  await fs.writeFile(path.join(artifactsDir, 'artifact-a.txt'), 'mutated\n', 'utf8');

  const verifyResult = await runNodeScript(verifyManifestScript, [
    '--artifacts-dir', artifactsDir
  ]);

  assert.equal(verifyResult.code, 1);
  assert.match(verifyResult.stderr, /sha256 mismatch|size mismatch/);
});

test('write-release-summary produces a readable markdown summary', async () => {
  const artifactsDir = await createArtifactsDir();
  const writeManifestResult = await runNodeScript(writeManifestScript, [
    '--artifacts-dir', artifactsDir,
    '--platform', 'macos',
    '--metadata', 'gitBranch=main',
    '--metadata', 'gitDirty=true'
  ]);

  assert.equal(writeManifestResult.code, 0, writeManifestResult.stderr);

  const summaryResult = await runNodeScript(writeSummaryScript, [
    '--artifacts-dir', artifactsDir
  ]);

  assert.equal(summaryResult.code, 0, summaryResult.stderr);

  const summaryPath = path.join(artifactsDir, 'release-summary.md');
  const summary = await fs.readFile(summaryPath, 'utf8');

  assert.match(summary, /^# Release Summary/m);
  assert.match(summary, /gitBranch: main/);
  assert.match(summary, /\| File \| Size \| SHA-256 \|/);
});
