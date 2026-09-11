"""Execute generated installers against local corrupt/untrusted fixtures; never change PATH."""
import hashlib, json, os, pathlib, platform, shutil, subprocess, tempfile, zipfile

repo = pathlib.Path(__file__).resolve().parents[1]
work = pathlib.Path(tempfile.mkdtemp(prefix='cli-bootstrap-safety-'))
print('Bootstrap safety artifacts:', work, flush=True)

import atexit
@atexit.register
def save_logs():
    destination = repo/'.artifacts'/work.name
    destination.mkdir(parents=True, exist_ok=True)
    for filename in ('commands.log', 'registry.log', 'result.json'):
        source = work/filename
        if source.is_file(): shutil.copyfile(source, destination/filename)
    for secret in work.glob('*.private.pem'): secret.unlink(missing_ok=True)

tool = work/'tools/cli-tools.dll'
rid = ('win' if os.name == 'nt' else 'osx' if platform.system() == 'Darwin' else 'linux') + '-' + {'amd64':'x64','x86_64':'x64','aarch64':'arm64','arm64':'arm64'}[platform.machine().lower()]
extension = '.exe' if os.name == 'nt' else ''
catalog_file = work/'catalog.json'
root = work/'install'
environment = {k:v for k,v in os.environ.items() if not k.startswith('COREVAR_')}

def run(*command, expected=0):
    result = subprocess.run(list(map(str,command)), cwd=repo, env=environment, capture_output=True, text=True, timeout=90,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    with (work/'commands.log').open('a', encoding='utf-8') as log: log.write(json.dumps(list(map(str,command)))+'\n'+result.stdout+result.stderr+'\n')
    assert result.returncode == expected, (command,result.returncode,result.stdout,result.stderr)
    return result

def generate(output, *extra):
    run('dotnet',tool,'generate','installers','--product','fixture','--catalog',catalog_file.as_uri(),'--output',output,*extra)

def install(scripts, expected=0):
    if os.name == 'nt': return run(shutil.which('pwsh') or 'pwsh','-NoProfile','-File',scripts/'install.ps1','-InstallDir',root,'-NoPath','-Quiet',expected=expected)
    return run('sh',scripts/'install.sh','--install-dir',root,'--no-path','--quiet',expected=expected)

def publish(version, bad_entry=None, **artifact_fields):
    archive = work/('payload-'+version.replace('/','_')+'.zip')
    with zipfile.ZipFile(archive,'w') as package:
        package.writestr('fixture'+extension,b'non-executable fixture payload')
        package.writestr('.corevar/launcher'+extension,b'non-executable fixture launcher')
        if bad_entry: package.writestr(bad_entry,b'unsafe')
    artifact = dict(runtimeIdentifier=rid,uri=archive.as_uri(),sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),**artifact_fields)
    value = dict(product='fixture',channels={'stable':version},releases=[dict(version=version,artifacts=[artifact])],revocations=[])
    catalog_file.write_text(json.dumps(value))
    return value

run('dotnet', 'publish', repo/'src/CoreVar.CliTools/CoreVar.CliTools.csproj', '-c', 'Release', '-f', 'net10.0', '-o', tool.parent)
scripts = work/'scripts'
generate(scripts)
publish('1.0.0')
install(scripts); install(scripts)
before = (root/'state.json').read_bytes()
launcher = (root/'bin'/('fixture'+extension)).read_bytes()

def unchanged():
    assert (root/'state.json').read_bytes() == before
    assert (root/'bin'/('fixture'+extension)).read_bytes() == launcher

for name in ('../outside', '..\\outside', '/absolute', 'C:/absolute'):
    value = publish('2.0.0',name)
    install(scripts,expected=1); unchanged()
value = publish('2.0.0'); value['releases'][0]['artifacts'][0]['sha256']='0'*64
catalog_file.write_text(json.dumps(value)); install(scripts,expected=1); unchanged()
value = publish('2.0.0',signingKeyId='unknown',signature='AA==')
install(scripts,expected=1); unchanged()
value = publish('2.0.0'); value['revocations']=[{'version':'2.0.0','reason':'test'}]
catalog_file.write_text(json.dumps(value)); install(scripts,expected=1); unchanged()
value = publish('2.0.0'); value['releases'][0]['postInstallArguments']=['setup']
catalog_file.write_text(json.dumps(value)); install(scripts,expected=1); unchanged()
run('dotnet',tool,'keygen','--private-key-file',work/'test.private.pem','--public-key-file',work/'test.public.pem')
(work/'keys.json').write_text(json.dumps({'trusted':(work/'test.public.pem').read_text()}))
required = work/'required'; generate(required,'--trusted-keys',work/'keys.json','--require-signature')
publish('2.0.0'); install(required,expected=1); unchanged()
(work/'test.private.pem').unlink()
print('PASS: traversal, wrong digest, unknown key, missing signature, revocation, setup failure, quiet idempotency')
