using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Publishing;

await CliApp.RunAsync(cli => cli
    .Version(typeof(Program).Assembly.GetName().Version?.ToString() ?? "development")
    .Description("Open-source packaging and distribution tools for CLI applications and modules.")
    .Command("keygen", command =>
    {
        var privateFile = command.Option<string>("--private-key-file").IsRequired();
        var publicFile = command.Option<string>("--public-key-file").IsRequired();
        command.Description("Creates a free RSA signing key pair in new files. Keep the private file outside source control.")
            .OnExecute(context =>
            {
                var keys = ArtifactSigning.CreateRsaKeyPair();
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using var secret = new FileStream(context.GetOption(privateFile), options);
                using var publicKey = new FileStream(context.GetOption(publicFile), FileMode.CreateNew, FileAccess.Write);
                secret.Write(System.Text.Encoding.UTF8.GetBytes(keys.PrivateKeyPem));
                publicKey.Write(System.Text.Encoding.UTF8.GetBytes(keys.PublicKeyPem));
                return context.Console.WriteLine("Created RSA key pair.");
            });
    })
    .Command("sign", command =>
    {
        var file = command.Option<string>("--file").IsRequired();
        var key = command.Option<string>("--private-key-file").IsRequired();
        var keyId = command.Option<string>("--key-id").IsRequired();
        var output = command.Option<string>("--output").IsRequired();
        command.Description("Creates a detached RSA-PSS signature; the private key is read locally and never published.")
            .OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var signature = await ArtifactSignature.CreateAsync(context.GetOption(file), context.GetOption(keyId),
                    await File.ReadAllTextAsync(context.GetOption(key), context.CancellationToken), context.CancellationToken);
                await signature.SaveAsync(context.GetOption(output), context.CancellationToken);
                await context.Console.WriteLine("Created detached signature.");
            }));
    })
    .Command("verify", command =>
    {
        var file = command.Option<string>("--file").IsRequired();
        var signature = command.Option<string>("--signature").IsRequired();
        var key = command.Option<string>("--public-key-file").IsRequired();
        command.Description("Verifies an artifact against an independently trusted publisher public key.")
            .OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var value = ArtifactSignature.Load(context.GetOption(signature));
                var trusted = await File.ReadAllTextAsync(context.GetOption(key), context.CancellationToken);
                await new CoreVar.CommandLineInterface.Distribution.ArtifactVerifier(new Dictionary<string, string> { [value.KeyId] = trusted }, true)
                    .VerifyAsync(context.GetOption(file), new CoreVar.CommandLineInterface.Distribution.ReleaseArtifact
                    { RuntimeIdentifier = "any", Uri = new Uri(Path.GetFullPath(context.GetOption(file))), Sha256 = value.Sha256,
                        SigningKeyId = value.KeyId, Signature = value.Signature }, context.CancellationToken);
                await context.Console.WriteLine("Artifact signature verified.");
            }));
    })
    .Command("installer", installer => installer
        .Command("build", command =>
        {
            var recipe = command.Option<string>("--recipe").IsRequired();
            var wix = command.Option<string>("--wix").Default("wix");
            command.Description("Builds Windows MSI and branded setup EXE from a publisher recipe.")
                .OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
                {
                    var value = WindowsInstallerRecipe.Load(context.GetOption(recipe));
                    await WindowsInstaller.BuildAsync(value, context.GetOption(wix), context.CancellationToken);
                    await context.Console.WriteLine($"Built installers in {Path.GetFullPath(value.OutputDirectory)}");
                }));
        }))
    .Command("package", command =>
    {
        var source = command.Option<string>("--source").IsRequired();
        var output = command.Option<string>("--output").IsRequired();
        var launcher = command.Option<string?>("--launcher").IsOptional();
        command.Description("Creates a reproducible CLI or module ZIP artifact.")
            .OnExecute(context =>
            {
                PackageBuilder.Create(context.GetOption(source), context.GetOption(output), context.GetOption(launcher));
                return context.Console.WriteLine($"Created {Path.GetFullPath(context.GetOption(output))}");
            });
    })
    .Command("publish", publish => publish
        .Description("Publishes immutable artifacts to a self-hosted CoreVar registry.")
        .Command("cli", command =>
        {
            var endpoint = command.Option<Uri>("--endpoint").IsRequired();
            var tenant = command.Option<string>("--tenant").IsRequired();
            var product = command.Option<string>("--product").IsRequired();
            var version = command.Option<string>("--version").IsRequired();
            var rid = command.Option<string>("--rid").IsRequired();
            var file = command.Option<string>("--file").IsRequired();
            var signature = command.Option<string?>("--signature").IsOptional();
            var channel = command.Option<string>("--channel").Default("stable");
            var token = command.Option<string?>("--token").FromEnvironment("COREVAR_REGISTRY_TOKEN");
            command.OnExecute(async context =>
            {
                var result = await new RegistryPublisher().PublishCliAsync(context.GetOption(endpoint), context.GetOption(tenant),
                    context.GetOption(product), context.GetOption(version), context.GetOption(rid), context.GetOption(file),
                    context.GetOption(channel), context.GetOption(token), context.CancellationToken,
                    context.GetOption(signature) is { } path ? ArtifactSignature.Load(path) : null);
                await context.Console.WriteLine($"Published {result.Uri} ({result.Sha256})");
            });
        })
        .Command("module", command =>
        {
            var endpoint = command.Option<Uri>("--endpoint").IsRequired();
            var tenant = command.Option<string>("--tenant").IsRequired();
            var id = command.Option<string>("--id").IsRequired();
            var version = command.Option<string>("--version").IsRequired();
            var file = command.Option<string>("--file").IsRequired();
            var channel = command.Option<string>("--channel").Default("stable");
            var description = command.Option<string?>("--description");
            var token = command.Option<string?>("--token").FromEnvironment("COREVAR_REGISTRY_TOKEN");
            command.OnExecute(async context =>
            {
                var result = await new RegistryPublisher().PublishModuleAsync(context.GetOption(endpoint), context.GetOption(tenant),
                    context.GetOption(id), context.GetOption(version), context.GetOption(file), context.GetOption(channel),
                    context.GetOption(description), context.GetOption(token), context.CancellationToken);
                await context.Console.WriteLine($"Published {result.Uri} ({result.Sha256})");
            });
        })
        .Command("release", command =>
        {
            var recipe = command.Option<string>("--recipe").IsRequired();
            var token = command.Option<string?>("--token").IsOptional().FromEnvironment("COREVAR_REGISTRY_TOKEN");
            command.Description("Publishes and validates a complete RID-matrix release from a declarative recipe, then promotes it and emits installers.")
                .OnExecute(async context =>
                {
                    var value = ReleaseRecipePublisher.Load(context.GetOption(recipe));
                    await new ReleaseRecipePublisher().PublishAsync(value, context.GetOption(token), context.CancellationToken);
                    await context.Console.WriteLine($"Published complete {value.Product} {value.Version}; installers: {Path.GetFullPath(value.InstallerOutput)}");
                });
        })
        .Command("static-cli", command =>
        {
            var root = command.Option<string>("--root").IsRequired(); var publicBase = command.Option<Uri>("--public-base").IsRequired();
            var tenant = command.Option<string>("--tenant").IsRequired(); var product = command.Option<string>("--product").IsRequired();
            var version = command.Option<string>("--version").IsRequired(); var rid = command.Option<string>("--rid").IsRequired();
            var file = command.Option<string>("--file").IsRequired(); var channel = command.Option<string>("--channel").Default("stable");
            var signature = command.Option<string?>("--signature").IsOptional();
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var result = await new StaticRegistryPublisher(context.GetOption(root), context.GetOption(publicBase)).PublishCliAsync(context.GetOption(tenant), context.GetOption(product), context.GetOption(version), context.GetOption(rid), context.GetOption(file), context.GetOption(channel), context.CancellationToken, context.GetOption(signature) is { } path ? ArtifactSignature.Load(path) : null);
                await context.Console.WriteLine($"Published static artifact {result.Uri} ({result.Sha256})");
            }));
        })
        .Command("static-module", command =>
        {
            var root = command.Option<string>("--root").IsRequired(); var publicBase = command.Option<Uri>("--public-base").IsRequired();
            var tenant = command.Option<string>("--tenant").IsRequired(); var id = command.Option<string>("--id").IsRequired();
            var version = command.Option<string>("--version").IsRequired(); var file = command.Option<string>("--file").IsRequired();
            var channel = command.Option<string>("--channel").Default("stable"); var description = command.Option<string?>("--description");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var result = await new StaticRegistryPublisher(context.GetOption(root), context.GetOption(publicBase)).PublishModuleAsync(context.GetOption(tenant), context.GetOption(id), context.GetOption(version), context.GetOption(file), context.GetOption(channel), context.GetOption(description), context.CancellationToken);
                await context.Console.WriteLine($"Published static module {result.Package} ({result.Sha256})");
            }));
        })
        .Command("promote", command =>
        {
            var endpoint = command.Option<Uri>("--endpoint").IsRequired(); var tenant = command.Option<string>("--tenant").IsRequired();
            var product = command.Option<string>("--product").IsRequired(); var channel = command.Option<string>("--channel").IsRequired();
            var version = command.Option<string>("--version").IsRequired(); var percentage = command.Option<int>("--percentage").Default(100).Range(0, 100);
            var fallback = command.Option<string?>("--fallback-version"); var token = command.Option<string?>("--token").FromEnvironment("COREVAR_REGISTRY_TOKEN");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new RegistryPublisher().PromoteAsync(context.GetOption(endpoint), context.GetOption(tenant), context.GetOption(product), context.GetOption(channel), context.GetOption(version), context.GetOption(percentage), context.GetOption(fallback), context.GetOption(token), context.CancellationToken)));
        })
        .Command("revoke", command =>
        {
            var endpoint = command.Option<Uri>("--endpoint").IsRequired(); var tenant = command.Option<string>("--tenant").IsRequired();
            var product = command.Option<string>("--product").IsRequired(); var version = command.Option<string?>("--version");
            var sha = command.Option<string?>("--sha256"); var reason = command.Option<string>("--reason").IsRequired();
            var token = command.Option<string?>("--token").FromEnvironment("COREVAR_REGISTRY_TOKEN");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new RegistryPublisher().RevokeAsync(context.GetOption(endpoint), context.GetOption(tenant), context.GetOption(product), context.GetOption(version), context.GetOption(sha), context.GetOption(reason), context.GetOption(token), context.CancellationToken)));
        }))
    .Command("generate", generate => generate
        .Description("Generates bootstrap installers and native package-manager metadata.")
        .Command("installers", command =>
        {
            var product = command.Option<string>("--product").IsRequired();
            var catalog = command.Option<Uri>("--catalog").IsRequired();
            var output = command.Option<string>("--output").Default("dist");
            var channel = command.Option<string>("--channel").Default("stable");
            var trustedKeys = command.Option<string?>("--trusted-keys").IsOptional();
            var requireSignature = command.Option<bool>("--require-signature").Default(false);
            command.OnExecute(async context =>
            {
                var keys = context.GetOption(trustedKeys) is { } path ? PublicKeyFiles.Load(path) : null;
                var directory = context.GetOption(output); Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "install.ps1"), InstallerScriptGenerator.PowerShell(context.GetOption(product), context.GetOption(catalog), context.GetOption(channel), keys, context.GetOption(requireSignature)), context.CancellationToken);
                await File.WriteAllTextAsync(Path.Combine(directory, "install.sh"), InstallerScriptGenerator.Shell(context.GetOption(product), context.GetOption(catalog), context.GetOption(channel), keys, context.GetOption(requireSignature)), context.CancellationToken);
                await context.Console.WriteLine($"Generated installers in {Path.GetFullPath(directory)}");
            });
        })
        .Command("winget", command =>
        {
            var package = command.Option<string>("--package-id").IsRequired();
            var name = command.Option<string>("--name").IsRequired();
            var publisher = command.Option<string>("--publisher").IsRequired();
            var version = command.Option<string>("--version").IsRequired();
            var uri = command.Option<Uri>("--uri").IsRequired();
            var sha = command.Option<string>("--sha256").IsRequired();
            var architecture = command.Option<string>("--architecture").Default("x64");
            var executable = command.Option<string>("--executable").IsRequired();
            var license = command.Option<string>("--license").IsRequired();
            var output = command.Option<string>("--output").Default("winget.yaml");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new ValueTask(File.WriteAllTextAsync(context.GetOption(output), NativePackageGenerator.WinGet(context.GetOption(package), context.GetOption(name), context.GetOption(publisher), context.GetOption(version), context.GetOption(uri), context.GetOption(sha), context.GetOption(license), context.GetOption(architecture), context.GetOption(executable)), context.CancellationToken))));
        })
        .Command("homebrew", command =>
        {
            var formula = command.Option<string>("--formula").IsRequired();
            var description = command.Option<string>("--description").IsRequired();
            var version = command.Option<string>("--version").IsRequired();
            var uri = command.Option<Uri>("--uri").IsRequired();
            var sha = command.Option<string>("--sha256").IsRequired();
            var executable = command.Option<string>("--executable").IsRequired();
            var license = command.Option<string>("--license").IsRequired();
            var output = command.Option<string>("--output").Default("Formula.rb");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new ValueTask(File.WriteAllTextAsync(context.GetOption(output), NativePackageGenerator.Homebrew(context.GetOption(formula), context.GetOption(description), context.GetOption(version), context.GetOption(uri), context.GetOption(sha), context.GetOption(executable), context.GetOption(license)), context.CancellationToken))));
        })
        .Command("deb", command =>
        {
            var package = command.Option<string>("--package").IsRequired(); var version = command.Option<string>("--version").IsRequired();
            var architecture = command.Option<string>("--architecture").Default("amd64"); var maintainer = command.Option<string>("--maintainer").IsRequired();
            var description = command.Option<string>("--description").IsRequired(); var output = command.Option<string>("--output").Default("control");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new ValueTask(File.WriteAllTextAsync(context.GetOption(output), NativePackageGenerator.DebianControl(context.GetOption(package), context.GetOption(version), context.GetOption(architecture), context.GetOption(maintainer), context.GetOption(description)), context.CancellationToken))));
        })
        .Command("rpm", command =>
        {
            var package = command.Option<string>("--package").IsRequired(); var version = command.Option<string>("--version").IsRequired();
            var summary = command.Option<string>("--summary").IsRequired(); var executable = command.Option<string>("--executable").IsRequired();
            var architecture = command.Option<string>("--architecture").Default("x86_64");
            var license = command.Option<string>("--license").IsRequired();
            var output = command.Option<string>("--output").Default("package.spec");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new ValueTask(File.WriteAllTextAsync(context.GetOption(output), NativePackageGenerator.RpmSpec(context.GetOption(package), context.GetOption(version), context.GetOption(summary), context.GetOption(executable), context.GetOption(license), context.GetOption(architecture)), context.CancellationToken))));
        })
        .Command("appinstaller", command =>
        {
            var publisher = command.Option<string>("--publisher").IsRequired();
            var architecture = command.Option<string>("--architecture").Default("x64");
            var identity = command.Option<string>("--identity").IsRequired(); var version = command.Option<string>("--version").IsRequired();
            var uri = command.Option<Uri>("--uri").IsRequired(); var output = command.Option<string>("--output").Default("app.appinstaller");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new ValueTask(File.WriteAllTextAsync(context.GetOption(output), NativePackageGenerator.AppInstaller(context.GetOption(identity), context.GetOption(version), context.GetOption(uri), publisher: context.GetOption(publisher), architecture: context.GetOption(architecture)), context.CancellationToken))));
        })
        .Command("wix", command =>
        {
            var product = command.Option<string>("--product").IsRequired(); var manufacturer = command.Option<string>("--manufacturer").IsRequired();
            var version = command.Option<string>("--version").IsRequired(); var executable = command.Option<string>("--executable").IsRequired();
            var upgradeCode = command.Option<string?>("--upgrade-code").IsOptional();
            var architecture = command.Option<string>("--architecture").Default("x64");
            var scope = command.Option<string>("--scope").Default("user");
            var output = command.Option<string>("--output").Default("package.wxs");
            command.OnExecute(new Func<CommandExecutionContext, ValueTask>(context => new ValueTask(File.WriteAllTextAsync(context.GetOption(output), NativePackageGenerator.WixSource(context.GetOption(product), context.GetOption(manufacturer), context.GetOption(version), context.GetOption(executable), context.GetOption(upgradeCode) is { } guid ? Guid.Parse(guid) : null, context.GetOption(architecture), context.GetOption(scope)), context.CancellationToken))));
        })));
