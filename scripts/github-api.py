"""GitHub release transport using Git Credential Manager; credentials stay in memory."""
import argparse
import base64
import hashlib
import http.client
import json
import os
from pathlib import Path
import shutil
import ssl
import subprocess
from urllib.parse import urlsplit

parser = argparse.ArgumentParser()
parser.add_argument('action', choices=['preflight', 'push', 'release'])
parser.add_argument('--version')
parser.add_argument('--commit')
parser.add_argument('--executable')
args = parser.parse_args()
git = Path(shutil.which('git') or 'git')
git_root = git.resolve().parent.parent
manager = git_root / 'mingw64/bin/git-credential-manager.exe'
environment = os.environ.copy()
environment['GCM_INTERACTIVE'] = 'never'
for key in list(environment):
    if key.startswith('GIT_TRACE') or key in {'GIT_CURL_VERBOSE', 'GCM_TRACE', 'GCM_TRACE_SECRETS'}:
        environment.pop(key)
environment['GIT_TRACE_REDACT'] = '1'
credential = subprocess.run([str(manager), 'get'], input='protocol=https\nhost=github.com\nusername=paulnsk\n\n',
                            text=True, capture_output=True, env=environment, check=True)
values = dict(line.split('=', 1) for line in credential.stdout.splitlines() if '=' in line)
token = values.get('password')
if not token:
    raise SystemExit('GitHub credentials unavailable in Git Credential Manager')
context = ssl.create_default_context(cafile=str(git_root / 'mingw64/etc/ssl/certs/ca-bundle.crt'))
headers = {'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
           'User-Agent': 'Pikvew-release', 'X-GitHub-Api-Version': '2022-11-28'}

def request(method, url, body=None, extra=None):
    parsed = urlsplit(url)
    connection = http.client.HTTPSConnection(parsed.hostname, context=context, timeout=240)
    try:
        connection.request(method, parsed.path + ('?' + parsed.query if parsed.query else ''), body, headers | (extra or {}))
        response = connection.getresponse()
        data = response.read()
        if not 200 <= response.status < 300:
            raise RuntimeError(f'GitHub HTTP {response.status}: {data.decode("utf-8", errors="replace")[:1000]}')
        return json.loads(data) if data else None
    finally:
        connection.close()

api = 'https://api.github.com/repos/paulnsk/pikvew'
if args.action == 'preflight':
    repo = request('GET', api)
    releases = request('GET', api + '/releases')
    print(json.dumps({'repository': repo['full_name'], 'can_push': repo['permissions']['push'], 'releases': len(releases)}))
elif args.action == 'push':
    environment.update({key: '' for key in ['HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY', 'GIT_HTTP_PROXY', 'GIT_HTTPS_PROXY']})
    count = int(environment.get('GIT_CONFIG_COUNT', '0'))
    basic = base64.b64encode((values.get('username', 'paulnsk') + ':' + token).encode()).decode()
    environment.update({'GIT_CONFIG_COUNT': str(count + 1), f'GIT_CONFIG_KEY_{count}': 'http.https://github.com/.extraheader',
                        f'GIT_CONFIG_VALUE_{count}': 'Authorization: Basic ' + basic})
    subprocess.run([str(git), 'push', 'origin', 'main'], env=environment, check=True)
else:
    if not all([args.version, args.commit, args.executable]):
        parser.error('release requires --version, --commit, --executable')
    title = 'Pikvew ' + args.version
    payload = json.dumps(dict(tag_name='v' + args.version, target_commitish=args.commit, name=title, body=title, draft=True, prerelease=False))
    release = request('POST', api + '/releases', payload.encode(), {'Content-Type': 'application/json'})
    executable = Path(args.executable)
    size = executable.stat().st_size
    upload = release['upload_url'].split('{', 1)[0] + '?name=Pikvew.exe'
    with executable.open('rb') as stream:
        asset = request('POST', upload, stream, {'Content-Type': 'application/octet-stream', 'Content-Length': str(size)})
    if asset['size'] != size:
        raise RuntimeError('Uploaded asset size differs from the published executable')
    with executable.open('rb') as stream:
        digest = 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest()
    if asset.get('digest') and asset['digest'] != digest:
        raise RuntimeError('Uploaded asset checksum differs from the published executable')
    published = request('PATCH', api + '/releases/' + str(release['id']), b'{"draft":false}', {'Content-Type': 'application/json'})
    print(json.dumps({'url': published['html_url'], 'version': args.version, 'asset': asset['browser_download_url'], 'bytes': size, 'digest': digest}))
