import argparse, base64, fcntl, hashlib, json, os, pathlib, platform, shutil, stat, subprocess, sys, tempfile, urllib.request, uuid, zipfile

CONFIG = json.loads(base64.b64decode('__CONFIG__'))

def segment(value):
    if not value or value in ('.', '..') or any(c in value for c in '/\\:\x00'):
        raise ValueError('Invalid version or channel path segment')
    return value

def download(uri, target):
    with urllib.request.urlopen(uri, timeout=60) as source, open(target, 'wb') as output:
        shutil.copyfileobj(source, output)

def digest(path):
    with open(path, 'rb') as source:
        result = hashlib.sha256()
        for block in iter(lambda: source.read(1024 * 1024), b''): result.update(block)
        return result.hexdigest()

def normalized(value):
    return value.lower().replace('sha256:', '').replace('-', '')

def verify(path, artifact, keys, required, temporary):
    actual = digest(path)
    if actual != normalized(artifact['sha256']): raise ValueError('SHA-256 verification failed')
    signature = artifact.get('signature')
    if not signature:
        if required or artifact.get('signingKeyId'): raise ValueError('A trusted signature is required')
        return
    key = keys.get(artifact.get('signingKeyId'))
    if not key: raise ValueError('Artifact signing key is not trusted')
    key_path, signature_path, data_path = (temporary / name for name in ('public.pem', 'signature.bin', 'digest.txt'))
    key_path.write_text(key)
    signature_path.write_bytes(base64.b64decode(signature, validate=True))
    data_path.write_bytes(actual.upper().encode('ascii'))
    subprocess.run(['openssl', 'dgst', '-sha256', '-verify', str(key_path), '-signature', str(signature_path),
        '-sigopt', 'rsa_padding_mode:pss', '-sigopt', 'rsa_pss_saltlen:32', str(data_path)], check=True, stdout=subprocess.DEVNULL)

def extract(archive, root):
    with zipfile.ZipFile(archive) as package:
        if len(package.infolist()) > 100000 or sum(i.file_size for i in package.infolist()) > 4 * 1024**3:
            raise ValueError('Archive extraction limit exceeded')
        names = set()
        for entry in package.infolist():
            path = pathlib.PurePosixPath(entry.filename.replace('\\', '/'))
            if path.is_absolute() or '..' in path.parts or any(':' in p for p in path.parts): raise ValueError('Archive path traversal was blocked')
            target = root.joinpath(*path.parts)
            if target in names or target == root: raise ValueError('Duplicate or invalid archive path')
            names.add(target)
            mode = entry.external_attr >> 16
            if stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR): raise ValueError('Archive links are not supported')
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True); continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with package.open(entry) as source, target.open('xb') as output: shutil.copyfileobj(source, output)
            if mode & 0o777: target.chmod(mode & 0o777)

def write_atomic(path, data):
    temporary = path.with_name(path.name + '.new-' + uuid.uuid4().hex)
    try:
        with open(temporary, 'wb') as output:
            output.write(data); output.flush(); os.fsync(output.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)

def install(args):
    segment(args.channel)
    root = pathlib.Path(args.install_dir).expanduser().absolute()
    root.mkdir(parents=True, exist_ok=True)
    with (root / '.operation.lock').open('a+b') as lock:
        try: fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError: raise RuntimeError('Another installation operation is running')
        with tempfile.TemporaryDirectory(prefix='.setup-', dir=root) as folder:
            temporary = pathlib.Path(folder)
            catalog_file = temporary / 'catalog.json'
            download(CONFIG['catalog'], catalog_file)
            catalog = json.loads(catalog_file.read_text())
            if catalog['product'] != CONFIG['product']: raise ValueError('Catalog product mismatch')
            version = segment(args.version or catalog['channels'][args.channel])
            release = next(r for r in catalog['releases'] if r['version'] == version)
            machine = {'x86_64':'x64', 'amd64':'x64', 'aarch64':'arm64', 'arm64':'arm64'}.get(platform.machine().lower(), platform.machine().lower())
            rid = ('osx' if sys.platform == 'darwin' else 'linux') + '-' + machine
            artifact = next(a for a in release['artifacts'] if a['runtimeIdentifier'] == rid)
            if any(r.get('version') == version or (r.get('sha256') and normalized(r['sha256']) == normalized(artifact['sha256'])) for r in catalog.get('revocations', [])):
                raise ValueError('The selected release has been revoked')
            archive = temporary / 'artifact.zip'
            download(artifact['uri'], archive)
            keys = dict(CONFIG['trustedPublicKeys'])
            if args.trusted_keys: keys.update(json.loads(pathlib.Path(args.trusted_keys).read_text()))
            verify(archive, artifact, keys, CONFIG['requireSignature'] or args.require_signature, temporary)
            staging = temporary / 'payload'; staging.mkdir()
            extract(archive, staging)
            executable = staging / CONFIG['product']; launcher = staging / '.corevar' / 'launcher'
            if not executable.is_file() or not launcher.is_file(): raise ValueError('Artifact is missing its host or stable launcher')
            executable.chmod(0o755); launcher.chmod(0o755)
            bundle = release.get('bundle')
            if bundle:
                bundle_path = staging / '.corevar' / 'module-bundle.json'
                download(bundle['manifest'], bundle_path)
                if digest(bundle_path) != normalized(bundle['sha256']): raise ValueError('Module bundle SHA-256 verification failed')
            target = root / 'versions' / version
            target.parent.mkdir(exist_ok=True)
            marker = target / '.corevar-artifact.sha256'
            if target.exists():
                if not marker.is_file() or normalized(marker.read_text()) != digest(archive): raise ValueError('Existing version has different or unknown contents')
            else:
                (staging / '.corevar-artifact.sha256').write_text(digest(archive))
                os.replace(staging, target)
            state_path = root / 'state.json'
            previous = json.loads(state_path.read_text()) if state_path.exists() else {}
            pointers = {p: p.read_bytes() for p in (root / 'modules').glob('*/current.json')}
            bin_dir = root / 'bin'; bin_dir.mkdir(exist_ok=True)
            installed = bin_dir / CONFIG['product']
            backup = installed.read_bytes() if installed.exists() else None
            committed = False
            try:
                environment = dict(os.environ, COREVAR_CLI_HOME=str(root), COREVAR_CLI_ACTIVE_VERSION=version)
                for key in ('COREVAR_MODULE_BUNDLE', 'COREVAR_MODULE_BUNDLE_SHA256', 'COREVAR_MODULE_BUNDLE_SNAPSHOT'): environment.pop(key, None)
                if bundle:
                    environment.update(COREVAR_MODULE_BUNDLE=str(target / '.corevar' / 'module-bundle.json'), COREVAR_MODULE_BUNDLE_SHA256=bundle['sha256'], COREVAR_MODULE_BUNDLE_SNAPSHOT=bundle['snapshot'])
                if release.get('postInstallArguments'):
                    subprocess.run([str(target / CONFIG['product']), *release['postInstallArguments']], env=environment, check=True,
                        stdout=subprocess.DEVNULL if args.quiet else None, stdin=subprocess.DEVNULL if args.quiet else None)
                write_atomic(installed, (target / '.corevar' / 'launcher').read_bytes()); installed.chmod(0o755)
                state = dict(schemaVersion='1.0', product=CONFIG['product'], version=version, channel=args.channel,
                    catalog=CONFIG['catalog'], provider='direct', entrypoint=CONFIG['product'],
                    installationId=previous.get('installationId', uuid.uuid4().hex),
                    previousVersion=previous.get('previousVersion') if previous.get('version') == version else previous.get('version'))
                write_atomic(state_path, json.dumps(state).encode())
                committed = True
            finally:
                if not committed:
                    for pointer in (root / 'modules').glob('*/current.json'):
                        if pointer not in pointers: pointer.unlink()
                    for pointer, content in pointers.items(): write_atomic(pointer, content)
                    if backup is not None: write_atomic(installed, backup); installed.chmod(0o755)
                    else: installed.unlink(missing_ok=True)
            if not args.no_path:
                link = pathlib.Path.home() / '.local' / 'bin' / CONFIG['product']
                try:
                    link.parent.mkdir(parents=True, exist_ok=True)
                    if link.exists() or link.is_symlink():
                        if not link.is_symlink() or link.resolve() != installed.resolve(): raise ValueError('PATH entry belongs to another installation')
                    else: link.symlink_to(installed)
                except (OSError, ValueError) as error: print(f'Installed successfully; add {bin_dir} to PATH manually: {error}', file=sys.stderr)
            if not args.quiet: print(f"Installed {CONFIG['product']} {version} to {root}")

parser = argparse.ArgumentParser(description='Install a versioned CLI without administrator access')
parser.add_argument('--version', default=os.getenv('VERSION'))
parser.add_argument('--channel', default=os.getenv('CHANNEL', CONFIG['channel']))
parser.add_argument('--install-dir', default=os.getenv('INSTALL_DIR', str(pathlib.Path.home() / '.local' / 'share' / CONFIG['product'])))
parser.add_argument('--no-path', action='store_true')
parser.add_argument('--quiet', action='store_true')
parser.add_argument('--require-signature', action='store_true')
parser.add_argument('--trusted-keys')
try: install(parser.parse_args())
except (Exception, KeyboardInterrupt) as error:
    print(f'Installation failed: {error}', file=sys.stderr)
    sys.exit(1)
