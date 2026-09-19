using CliComponents;

await CliApp.RunAsync(cli =>
{
	cli
		.Components<CliContext>();
});