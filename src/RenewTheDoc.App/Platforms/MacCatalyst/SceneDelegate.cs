using Foundation;

namespace RenewTheDoc.App;

// iOS 27-era SDKs refuse to launch apps without the UIScene life cycle.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
