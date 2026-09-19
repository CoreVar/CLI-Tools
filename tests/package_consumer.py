"""Pack and consume fresh NuGet packages and templates without source project references."""
import json, os, pathlib, shutil, subprocess, tempfile, zipfile

repo = pathlib.Path(__file__).resolve().parents[1]
work = pathlib.Path(tempfile.mkdtemp(prefix='cli-package-consumer-'))
print('Package consumer artifacts:', work, flush=True)

import atexit
@atexit.register
def save_logs():
    destination = repo/'.artifacts'/work.name
    destination.mkdir(parents=True, exist_ok=True)
    for filename in ('commands.log', 'registry.log', 'result.json'):
        source = work/filename
        if source.is_file(): shutil.copyfile(source, destination/filename)
    for secret in work.glob('*.private.pem'): secret.unlink(missing_ok=True)

version = '99.0.0-consumer.1'
packages = work / 'packages'; packages.mkdir()
environment = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', NUGET_PACKAGES=str(work/'cache'))

def run(*args, cwd=repo):
    result = subprocess.run(list(map(str, args)), cwd=cwd, env=environment, text=True, capture_output=True, timeout=300)
    with (work/'commands.log').open('a', encoding='utf-8') as log:
        log.write(json.dumps(list(map(str,args)))+'\n'+result.stdout+result.stderr+'\n')
    if result.returncode: raise RuntimeError(result.stdout[-8000:] + result.stderr[-8000:])
    return result.stdout

for project in ('src/CommandLineInterface.Abstractions/CommandLineInterface.Abstractions.csproj',
                'src/CommandLineInterface/CommandLineInterface.csproj', 'templates/ProjectTemplates.csproj'):
    run('dotnet', 'pack', project, '-c', 'Release', '-o', packages, '-p:PackageVersion='+version, '-v', 'quiet', '-clp:ErrorsOnly')

core = packages / ('CoreVar.CommandLineInterface.'+version+'.nupkg')
with zipfile.ZipFile(core) as archive:
    assert 'analyzers/dotnet/cs/CoreVar.CommandLineInterface.SourceGenerator.dll' in archive.namelist()
    assert 'lib/net8.0/CoreVar.CommandLineInterface.dll' in archive.namelist()
    assert 'lib/net10.0/CoreVar.CommandLineInterface.dll' in archive.namelist()
template = packages / ('CoreVar.CommandLineInterface.Templates.'+version+'.nupkg')
with zipfile.ZipFile(template) as archive:
    projects = [p for p in archive.namelist() if p.endswith('.csproj')]
    assert len(projects) == 3, projects
    assert all(version in archive.read(p).decode() for p in projects)

hive = work/'template-hive'
run('dotnet', 'new', 'install', template, '--debug:custom-hive', hive)
for template_name, command in (('cli-simple','start'), ('cli-components','start')):
    for framework in ('net8.0','net10.0'):
        app = work/(template_name+'-'+framework)
        run('dotnet','new',template_name,'-n','Consumer','-o',app,'--debug:custom-hive',hive)
        project = next(app.glob('*.csproj'))
        project.write_text(project.read_text().replace('net8.0', framework))
        (app/'NuGet.Config').write_text(f'<configuration><packageSources><clear/><add key="local" value="{packages.as_posix()}"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
        run('dotnet','restore',project)
        run('dotnet','build',project,'-c','Release','--no-restore','-v','quiet','-clp:ErrorsOnly')
        result = run('dotnet',app/'bin/Release'/framework/'Consumer.dll',command)
        assert 'Starting' in result, result
        print('PASS:', template_name, framework, 'fresh package execution', flush=True)
(work/'result.json').write_text(json.dumps({'status':'passed', 'version':version}))
