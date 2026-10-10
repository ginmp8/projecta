using System;
using System.Threading;
using Projecta.Tooling;
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
return await ProjectionCli.RunAsync(args, Console.Out, Console.Error, cancellation.Token);
