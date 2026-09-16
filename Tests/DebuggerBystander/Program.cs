// A second .NET process for the live debugger tests to be attached to. It prints its own
// Debugger.IsAttached four times a second, so a test watches it the way a demo service watches
// itself, and it stops on its own after ten minutes should a test forget to kill it.
using System.Diagnostics;

const int ReportsPerSecond = 4;
const int MinutesToLive = 10;

for (var report = 0; report < ReportsPerSecond * 60 * MinutesToLive; report++)
{
    Console.Out.WriteLine(Debugger.IsAttached);
    await Task.Delay(TimeSpan.FromSeconds(1.0 / ReportsPerSecond));
}
