"""Real public CLI -> registry -> generated installer -> native module -> update.

Requires .NET 10 SDK, Python 3 and (on Windows) PowerShell 7. Uses only loopback
and a fresh temporary directory. Keeps artifacts/logs for inspection.
"""
import hashlib
import json
import os
from pathlib import Path
import platform
import secrets
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.request
import zipfile

sample = Path(__file__).resolve().parent
repo = sample.parents[1]
work = Path(tempfile.mkdtemp(prefix='cli-tools-recipe-'))
print('Artifacts and logs:', work, flush=True)

import atexit
@atexit.register
def save_logs():
    destination = repo/'.artifacts'/work.name
    destination.mkdir(parents=True, exist_ok=True)
    for filename in ('commands.log', 'registry.log', 'result.json'):
        source = work/filename
        if source.is_file(): shutil.copyfile(source, destination/filename)
    for secret in work.glob('*.private.pem'): secret.unlink(missing_ok=True)

env = {k:v for k,v in os.environ.items() if not k.startswith(('COREVAR_', 'ASPNETCORE_', 'DOTNET_URLS'))}
env['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
env['COREVAR_REGISTRY_TOKEN'] = secrets.token_hex(24)  # Ephemeral fixture credential only.
extension = '.exe' if os.name == 'nt' else ''
rid = ('win' if os.name == 'nt' else 'osx' if platform.system() == 'Darwin' else 'linux') + '-' + {'amd64':'x64','x86_64':'x64','aarch64':'arm64','arm64':'arm64'}[platform.machine().lower()]
creationflags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0

def run(*args, expected=0, process_env=None, cwd=None):
    command = list(map(str, args))
    result = subprocess.run(command, cwd=cwd or work, env=process_env or env,
        capture_output=True, text=True, timeout=300, creationflags=creationflags)
    with (work/'commands.log').open('a', encoding='utf-8') as log:
        log.write(json.dumps(command)+'\n'+result.stdout+result.stderr+'\n')
    assert result.returncode == expected, (command, result.returncode, result.stdout[-6000:], result.stderr[-6000:])
    return result.stdout + result.stderr

def publish(project, output, launcher=False):
    args = ['dotnet','publish',project,'-c','Release','-f','net10.0','-r',rid,'--self-contained','false',
        '-p:NuGetAudit=false','-p:NoWarn=1591','-p:DebugType=None','-o',output,'--nologo','-v','quiet']
    if launcher:
        args += ['-p:PublishAot=false','-p:PublishSingleFile=true','-p:PublishTrimmed=false']
    run(*args)

publish(repo/'src/CoreVar.CliTools/CoreVar.CliTools.csproj', work/'tools')
publish(repo/'src/CommandLineInterface.Registry/CommandLineInterface.Registry.csproj', work/'registry')
publish(repo/'src/CommandLineInterface.Launcher/CommandLineInterface.Launcher.csproj', work/'launcher', True)
publish(sample/'SampleHost/SampleHost.csproj', work/'host')
publish(sample/'SampleModule/SampleModule.csproj', work/'module')
tool = work/'tools'/('cli-tools'+extension)
launcher = work/'launcher'/('corevar-cli-launcher'+extension)
print('PASS: sample host, native module, public tools, registry and launcher built.', flush=True)

with socket.socket() as listener:
    listener.bind(('127.0.0.1',0))
    port = listener.getsockname()[1]
endpoint = f'http://127.0.0.1:{port}/'
run(tool, 'keygen', '--private-key-file', work/'release.private.pem', '--public-key-file', work/'release.public.pem')
public_keys = {'sample-publisher': (work/'release.public.pem').read_text()}
(work/'publisher-keys.json').write_text(json.dumps(public_keys))
(work/'registry-keys.json').write_text(json.dumps({'sample/sample-cli': public_keys}))
registry_env = dict(env, ASPNETCORE_URLS=endpoint, COREVAR_REGISTRY_PUBLIC_BASE_URL=endpoint,
    COREVAR_REGISTRY_SIGNING_KEYS_FILE=str(work/'registry-keys.json'), COREVAR_REGISTRY_SIGNED_CHANNELS='stable',
    COREVAR_REGISTRY_DATA=str(work/'data'), COREVAR_REGISTRY_API_KEYS='sample='+env['COREVAR_REGISTRY_TOKEN'])
registry_log = (work/'registry.log').open('w', encoding='utf-8')
registry = subprocess.Popen(['dotnet',str(work/'registry'/'CoreVar.CommandLineInterface.Registry.dll')],
    env=registry_env, stdout=registry_log, stderr=subprocess.STDOUT, creationflags=creationflags)

def get_json(path):
    with urllib.request.urlopen(endpoint+path, timeout=5) as response:
        return json.load(response)

def module(version):
    stage = work/('module-'+version)
    shutil.copytree(work/'module',stage/'bin'/rid)
    manifest = {'schemaVersion':'1.0','id':'sample-module','version':version,'cliCompatibility':'[1.0.0,2.0.0)',
        'runtime':{'kind':'native'},'entrypoints':{rid:{'path':f'bin/{rid}/sample-module{extension}'}},
        'commands':[{'name':'hello'}]}
    (stage/'corevar.module.json').write_text(json.dumps(manifest))
    archive = work/('module-'+version+'.zip')
    run(tool,'package','--source',stage,'--output',archive)
    with zipfile.ZipFile(archive) as package:
        assert f'bin/{rid}/sample-module{extension}' in package.namelist()
    run(tool,'publish','module','--endpoint',endpoint,'--tenant','sample','--id','sample-module',
        '--version',version,'--file',archive,'--description','Independent sample module')
    return hashlib.sha256(archive.read_bytes()).hexdigest()

def release(version, module_version, module_hash, expected=0):
    directory = work/('release-'+version)
    directory.mkdir()
    stage = directory/'host'
    shutil.copytree(work/'host',stage)
    shutil.copyfile(work/'publisher-keys.json', stage/'publisher-keys.json')
    bundle = {'schema':'corevar.cli.bundle/1','id':'sample-cli','snapshot':version,
        'hostCompatibility':'[1.0.0,2.0.0)','catalog':endpoint+'v1/sample/modules/catalog.json',
        'modules':[{'id':'sample-module','version':module_version,'sha256':module_hash,'required':True,'runtimeIdentifiers':[rid]}]}
    bundle_file = directory/'module-bundle.json'
    bundle_file.write_text(json.dumps(bundle))
    shutil.copyfile(bundle_file,stage/'module-bundle.json')
    (stage/'sample-release.json').write_text(json.dumps({'Version':version,
        'BundleSha256':hashlib.sha256(bundle_file.read_bytes()).hexdigest()}))
    archive = directory/'host.zip'
    run(tool,'package','--source',stage,'--output',archive,'--launcher',launcher)
    run(tool, 'sign', '--file', archive, '--private-key-file', work/'release.private.pem', '--key-id', 'sample-publisher', '--output', directory/'host.signature.json')
    run(tool, 'verify', '--file', archive, '--signature', directory/'host.signature.json', '--public-key-file', work/'release.public.pem')
    recipe = {'requireSignatures':True, 'signatures':{rid:'host.signature.json'}, 'endpoint':endpoint,'tenant':'sample','product':'sample-cli','version':version,
        'hostVersion':version,'frameworkVersion':'10.1.0','artifacts':{rid:'host.zip'},
        'bundleManifest':'module-bundle.json','bundleSnapshot':version,'postInstallArguments':['setup'],
        'installerOutput':'installers'}
    recipe_file = directory/'recipe.json'
    recipe_file.write_text(json.dumps(recipe))
    # Working directory deliberately differs from the recipe directory.
    run(tool,'publish','release','--recipe',recipe_file,expected=expected,cwd=work)
    return directory

def state():
    return json.loads((work/'installation'/'state.json').read_text(encoding='utf-8-sig'))

installed = work/'installation'/'bin'/('sample-cli'+extension)
client_env = dict(env, COREVAR_CLI_HOME=str(work/'installation'))
client_env.pop('COREVAR_REGISTRY_TOKEN')

def dispatch(version):
    result = json.loads(run(installed,'hello',process_env=client_env))
    assert result['version'] == version, result
    assert result['protocol'] == 'corevar.module.process/1', result
    assert result['processId'] != os.getpid() and result['processId'] > 0, result
    assert 'hello' in result['arguments'], result

try:
    for attempt in range(100):
        if registry.poll() is not None:
            raise AssertionError('Registry exited: '+(work/'registry.log').read_text())
        try:
            urllib.request.urlopen(endpoint+'healthz',timeout=1).close()
            break
        except OSError:
            time.sleep(.1)
    else:
        raise AssertionError('Loopback registry did not become healthy')
    digest = module('1.0.0')
    initial = release('1.0.0','1.0.0',digest)
    def bootstrap(directory, expected=0):
        if os.name == 'nt':
            run(shutil.which('pwsh') or 'pwsh', '-NoProfile', '-File', directory/'installers/install.ps1',
                '-InstallDir', work/'installation', '-NoPath', '-Quiet', expected=expected)
        else:
            unix_env = dict(env, HOME=str(work/'home'), INSTALL_DIR=str(work/'installation'))
            (work/'home').mkdir(exist_ok=True)
            run('sh', directory/'installers/install.sh', '--quiet', '--no-path', process_env=unix_env, expected=expected)
    bootstrap(initial)
    bootstrap(initial)  # Idempotent quiet install with signature enforcement.
    dynamic = work/'dynamic'
    (dynamic/'installers').mkdir(parents=True)
    for filename in ('install.ps1', 'install.sh'):
        urllib.request.urlretrieve(endpoint+'v1/sample/products/sample-cli/'+filename, dynamic/'installers'/filename)
    bootstrap(dynamic)  # Registry-served installers inherit owned keys and protected-channel policy.
    assert state()['version'] == '1.0.0'
    dispatch('1.0.0')
    print('PASS: public package/publish recipe -> generated installer -> native child dispatch.', flush=True)
    release('1.1.0','1.0.0',digest)
    run(installed,'update',process_env=client_env)
    assert state()['version'] == '1.1.0'
    dispatch('1.0.0')
    print('PASS: real host update and required bundle setup.', flush=True)
    next_digest = module('1.1.0')
    broken = release('1.2.0','1.1.0',next_digest)
    blob = work/'data/tenants/sample/blobs/modules/sample-module/1.1.0/module.zip'
    unavailable = blob.with_suffix('.unavailable')
    blob.rename(unavailable)  # Fault injection AFTER valid publication: download unavailable.
    try:
        previous_launcher = installed.read_bytes()
        bootstrap(broken, expected=1)
        assert state()['version'] == '1.1.0'
        assert installed.read_bytes() == previous_launcher
        dispatch('1.0.0')
        print('PASS: failed signed bootstrap upgrade preserves active host and launcher.', flush=True)
        run(installed,'update',process_env=client_env,expected=1)
        assert state()['version'] == '1.1.0'
        dispatch('1.0.0')
    finally:
        unavailable.rename(blob)
    print('PASS: required module download failure restores prior host and usable module.', flush=True)
    run(installed,'update',process_env=client_env)
    assert state()['version'] == '1.2.0'
    dispatch('1.1.0')
    run(installed,'update',process_env=client_env)  # Same version still runs readiness.
    dispatch('1.1.0')
    before = get_json('v1/sample/products/sample-cli/catalog.json')
    release('1.3.0','1.1.0','0'*64,expected=1)
    after = get_json('v1/sample/products/sample-cli/catalog.json')
    assert after['channels']['stable'] == before['channels']['stable'] == '1.2.0'
    assert next(r for r in after['releases'] if r['version']=='1.3.0').get('bundle') is None
    print('PASS: retry, same-version readiness, and bad bundle promotion rejection.', flush=True)
    (work/'result.json').write_text(json.dumps({'status':'passed','rid':rid,'registry':'loopback',
        'cases':['signed-install','quiet-idempotency','bootstrap-failure','install-dispatch','update','required-module-rollback','retry','same-version-readiness','invalid-bundle-promotion']},indent=2))
finally:
    registry.terminate()
    try:
        registry.wait(timeout=15)
    except subprocess.TimeoutExpired:
        registry.kill()
        registry.wait(timeout=10)
    registry_log.close()
