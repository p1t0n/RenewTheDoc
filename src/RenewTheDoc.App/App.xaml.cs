using Microsoft.Extensions.DependencyInjection;
using RenewTheDoc.Application.Documents;

namespace RenewTheDoc.App;

// Qualified: the RenewTheDoc.Application namespace shadows the unqualified MAUI type here, and a
// using-alias cannot win against an enclosing namespace member. Same at every Application.Current.
public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly DocumentAppService _documents;

	public App(DocumentAppService documents)
	{
		_documents = documents;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		ReplanRemindersInBackground();

		return new Window(new AppShell());
	}

	/// <summary>
	/// Reconciles every Document's Reminder with the platform on launch (spec §8 step 9). The
	/// database is already open — MauiProgram waits for that before the host is built — and nothing
	/// on screen reads the result, so the pass is started and left alone: the first frame costs
	/// exactly what it did before. One window on mobile means this runs once per launch.
	/// </summary>
	private void ReplanRemindersInBackground()
	{
		var nowLocal = DateTime.Now; // clock read at the edge, as on every other call into the service

		_ = Task.Run(async () =>
		{
			try
			{
				await _documents.ReplanAllRemindersAsync(nowLocal);
			}
			catch (Exception)
			{
				// A launch with stale reminders beats a launch that does not happen: the read itself
				// can fail (an unreadable row throws on reconstitution), and there is no screen yet
				// to tell. Per-Document scheduling failures are already absorbed inside the pass.
			}
		});
	}
}