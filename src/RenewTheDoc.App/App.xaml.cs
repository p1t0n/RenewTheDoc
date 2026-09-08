using Microsoft.Extensions.DependencyInjection;

namespace RenewTheDoc.App;

// Qualified: the RenewTheDoc.Application namespace shadows the unqualified MAUI type here, and a
// using-alias cannot win against an enclosing namespace member. Same at every Application.Current.
public partial class App : Microsoft.Maui.Controls.Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}