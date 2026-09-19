"""Build native installers; exercise installation only on an explicitly opted-in CI runner."""
import argparse, json, os, pathlib, subprocess, sys, uuid

parser = argparse.ArgumentParser()
parser.add_argument('--install', action='store_true')
args = parser.parse_args()
if os.name != 'nt': raise SystemExit('Windows is required')
if args.install and os.getenv('CI', '').lower() != 'true': raise SystemExit('--install requires an isolated CI runner (CI=true)')
repo = pathlib.Path(__file__).resolve().parents[1]
work = repo/'.artifacts'/'native-installer'; work.mkdir(parents=True, exist_ok=True)
tool = work/'payload/cli-tools.dll'

def run(*command, allowed=(0,)):
    result = subprocess.run(list(map(str, command)), cwd=repo, text=True, capture_output=True, timeout=300, creationflags=subprocess.CREATE_NO_WINDOW)
    with (work/'commands.log').open('a', encoding='utf-8') as output:
        output.write(json.dumps(list(map(str, command)))+'\n'+result.stdout+result.stderr+'\n')
    if result.returncode not in allowed: raise RuntimeError(f'{command}: {result.returncode}\n{result.stdout}\n{result.stderr}')
    return result.stdout

run('dotnet', 'publish', repo/'src/CoreVar.CliTools/CoreVar.CliTools.csproj', '-c', 'Release', '-o', work/'payload', '-v', 'quiet')
product = 'CLI Tools Acceptance ' + uuid.uuid4().hex[:8]
recipe = dict(product=product, publisher='Community Publisher', version='1.0.0', sourceDirectory='payload', executable='cli-tools.exe',
    upgradeCode=str(uuid.uuid4()), bundleUpgradeCode=str(uuid.uuid4()), architecture='x64', scope='user', outputDirectory='output')
recipe_file = work/'recipe.json'; recipe_file.write_text(json.dumps(recipe))
run('dotnet', tool, 'installer', 'build', '--recipe', recipe_file, '--wix', repo/'.tools/wix.exe')
bundle = work/'output'/(product+'-setup.exe')
assert bundle.is_file() and (work/'output/product.msi').is_file()
downloader_recipe = dict(recipe, downloadUrl='https://example.invalid/product.msi', outputDirectory='downloader')
downloader_file = work/'downloader.json'; downloader_file.write_text(json.dumps(downloader_recipe))
run('dotnet', tool, 'installer', 'build', '--recipe', downloader_file, '--wix', repo/'.tools/wix.exe')
downloader = work/'downloader'/(product+'-setup.exe')
assert downloader.is_file() and downloader.stat().st_size < bundle.stat().st_size
if args.install:
    installed = pathlib.Path(os.environ['LOCALAPPDATA'])/product/'cli-tools.exe'
    try:
        run(bundle, '/quiet', '/norestart', '/log', work/'install.log', allowed=(0,3010))
        assert installed.is_file()
        run(installed, '--version')
        run(bundle, '/repair', '/quiet', '/norestart', '/log', work/'repair.log', allowed=(0,3010))
        recipe['version'] = '1.0.1'; recipe['outputDirectory'] = 'upgrade'
        recipe_file.write_text(json.dumps(recipe))
        run('dotnet', tool, 'installer', 'build', '--recipe', recipe_file, '--wix', repo/'.tools/wix.exe')
        bundle = work/'upgrade'/(product+'-setup.exe')
        run(bundle, '/quiet', '/norestart', '/log', work/'upgrade.log', allowed=(0,3010))
        run(installed, '--version')
    finally:
        run(bundle, '/uninstall', '/quiet', '/norestart', '/log', work/'uninstall.log', allowed=(0,3010))
    assert not installed.exists()
print('PASS: native Windows installer ' + ('install/repair/upgrade/uninstall' if args.install else 'build'))
