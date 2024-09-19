await CliApp.RunAsync(cli =>
{
	cli
		.Command("start", startCommand =>
		{
			startCommand.OnExecute(() =>
			{
				Console.WriteLine("Starting...");
			});
		})
		.Command("stop", stopCommand =>
		{
			stopCommand.OnExecute(() =>
			{
				Console.WriteLine("Stopping...");
			});
		});
});