using RenewTheDoc.App.Pages;

namespace RenewTheDoc.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AddDocumentPage), typeof(AddDocumentPage));
    }
}
