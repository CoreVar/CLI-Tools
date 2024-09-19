using CliComponents;

await CliApp.RunAsync(cli =>
{
	cli
		.EnableRepl()
		.Components<CliContext>();
});