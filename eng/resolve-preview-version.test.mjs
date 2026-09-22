import assert from 'node:assert/strict';
import test from 'node:test';
import { NUGET_ORG, chooseVersion, readPublishedVersions, readVersions } from './resolve-preview-version.mjs';

test('first publish uses the configured preview; later publishes increment numerically', () => {
  assert.equal(chooseVersion('0.1.0-preview.1', []), '0.1.0-preview.1');
  assert.equal(chooseVersion('0.1.0-preview.1', ['0.1.0-preview.1']), '0.1.0-preview.2');
  assert.equal(chooseVersion('0.1.0-preview.1', ['0.1.0-preview.9', '0.1.0-preview.10']), '0.1.0-preview.11');
});

test('configured preview is a floor and other release lines do not affect it', () => {
  assert.equal(chooseVersion('0.1.0-preview.4', [
    '0.1.0-preview.1', '0.2.0-preview.99', '0.1.0', '0.1.0-rc.9',
  ]), '0.1.0-preview.4');
});

test('only unused previews on the configured release line are accepted', () => {
  const published = ['0.1.0-preview.1'];
  assert.equal(chooseVersion('0.1.0-preview.1', published, { suffix: 'preview.3' }), '0.1.0-preview.3');
  assert.equal(chooseVersion('0.1.0-preview.1', published, { tag: 'v0.1.0-preview.2' }), '0.1.0-preview.2');
  for (const suffix of ['preview.1', 'rc.2', 'preview.0', 'preview.02', 'preview.2;evil']) {
    assert.throws(() => chooseVersion('0.1.0-preview.1', published, { suffix }));
  }
  for (const tag of ['v0.1.0', 'v0.1.0-rc.2', 'v0.2.0-preview.2', 'v0.1.0-preview.1']) {
    assert.throws(() => chooseVersion('0.1.0-preview.1', published, { tag }));
  }
  assert.throws(() => chooseVersion('0.1.0', published));
});

function fakeFeed(responses) {
  return async (url, options) => {
    assert.ok(options.signal);
    if (url === 'https://feed/index.json') return Response.json({
      resources: [{ '@type': 'PackageBaseAddress/3.0.0', '@id': 'https://feed/flat/' }],
    });
    assert.ok(Object.hasOwn(responses, url), `Unexpected request ${url}`);
    const response = responses[url];
    return typeof response === 'number' ? new Response(null, { status: response }) : Response.json(response);
  };
}

test('all packages contribute, including a partially published newer preview', async () => {
  const versions = await readVersions('https://feed/index.json', ['Core', 'Provider', 'New'], {}, fakeFeed({
    'https://feed/flat/core/index.json': { versions: ['0.1.0-preview.1'] },
    'https://feed/flat/provider/index.json': { versions: ['0.1.0-preview.1', '0.1.0-preview.2'] },
    'https://feed/flat/new/index.json': 404,
  }));
  assert.equal(chooseVersion('0.1.0-preview.1', versions), '0.1.0-preview.3');
});

test('feed failures and malformed responses stop publication', async () => {
  for (const response of [401, 403, 429, 500, {}, { versions: [2] }]) {
    await assert.rejects(readVersions('https://feed/index.json', ['Core'], {}, fakeFeed({
      'https://feed/flat/core/index.json': response,
    })));
  }
  await assert.rejects(readVersions('https://feed/index.json', ['Core'], {}, async () => {
    throw new Error('Network unavailable');
  }));
  await assert.rejects(readVersions('https://feed/index.json', ['Core'], {}, async () => Response.json({})));
});

test('the next preview is cumulative across NuGet.org and GitHub Packages', async () => {
  const env = { GITHUB_REPOSITORY_OWNER: 'Owner', GITHUB_ACTOR: 'actor', GITHUB_TOKEN: 'token' };
  const github = 'https://nuget.pkg.github.com/Owner/index.json';
  const fetchImpl = async (url, options) => {
    const feed = /^https:\/\/(nuget\.pkg\.)?github[./]/.test(url) ? 'github' : 'nuget';
    assert.equal(Boolean(options.headers.authorization), feed === 'github', `Credentials sent to ${url}`);
    if (url === NUGET_ORG || url === github) return Response.json({
      resources: [{ '@type': 'PackageBaseAddress/3.0.0', '@id': `https://${feed}/flat/` }],
    });
    const versions = {
      'https://nuget/flat/core/index.json': ['0.1.0-preview.1', '0.1.0-preview.2'],
      'https://github/flat/core/index.json': ['0.1.0-preview.1', '0.1.0-preview.2', '0.1.0-preview.3'],
    }[url];
    return versions ? Response.json({ versions }) : new Response(null, { status: 404 });
  };
  const published = await readPublishedVersions(['Core'], env, fetchImpl);
  assert.equal(chooseVersion('0.1.0-preview.1', published), '0.1.0-preview.4');
  assert.throws(() => chooseVersion('0.1.0-preview.1', published, { tag: 'v0.1.0-preview.3' }));

  await assert.rejects(readPublishedVersions(['Core'], { ...env, GITHUB_TOKEN: '' }, fetchImpl));
  await assert.rejects(readPublishedVersions(['Core'], env, async (url, options) =>
    url.startsWith('https://nuget.pkg.github.com/') ? new Response(null, { status: 401 }) : fetchImpl(url, options)));
});
