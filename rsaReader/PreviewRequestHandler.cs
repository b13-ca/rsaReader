using CefSharp;
using CefSharp.Handler;

namespace rsaReader;

internal sealed class PreviewRequestHandler : RequestHandler {
    protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect) {
        if (!userGesture)
            return false;

        if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            && uri.Host.Equals("rsa.local", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
