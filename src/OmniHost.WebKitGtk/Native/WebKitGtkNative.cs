using System.Runtime.InteropServices;

namespace OmniHost.WebKitGtk.Native;

internal static class WebKitGtkNative
{
    private const string WebKitLib = "webkit2gtk";
    private const string JavaScriptCoreLib = "javascriptcoregtk";
    private const string GtkLib = "gtk-3";
    private const string GioLib = "gio-2.0";
    private const string GLibLib = "glib-2.0";
    private const string GObjectLib = "gobject-2.0";

    internal const int WebKitLoadFinished = 3;
    internal const int WebKitUserContentInjectTopFrame = 0;
    internal const int WebKitUserScriptInjectAtDocumentStart = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct GError
    {
        public uint domain;
        public int code;
        public IntPtr message;
    }

    internal delegate void GAsyncReadyCallback(IntPtr sourceObject, IntPtr result, IntPtr userData);
    internal delegate void LoadChangedCallback(IntPtr webView, int loadEvent, IntPtr userData);
    internal delegate void ScriptMessageReceivedCallback(IntPtr manager, IntPtr javascriptResult, IntPtr userData);
    internal delegate void DestroyNotify(IntPtr data);
    internal delegate void UriSchemeRequestCallback(IntPtr request, IntPtr userData);

    [DllImport(WebKitLib, EntryPoint = "webkit_user_content_manager_new")]
    internal static extern IntPtr WebKitUserContentManagerNew();

    [DllImport(WebKitLib, EntryPoint = "webkit_user_content_manager_register_script_message_handler")]
    internal static extern bool WebKitUserContentManagerRegisterScriptMessageHandler(
        IntPtr manager,
        string name);

    [DllImport(WebKitLib, EntryPoint = "webkit_user_content_manager_add_script")]
    internal static extern void WebKitUserContentManagerAddScript(IntPtr manager, IntPtr script);

    [DllImport(WebKitLib, EntryPoint = "webkit_user_script_new")]
    internal static extern IntPtr WebKitUserScriptNew(
        string source,
        int injectedFrames,
        int injectionTime,
        IntPtr allowList,
        IntPtr blockList);

    [DllImport(WebKitLib, EntryPoint = "webkit_user_script_unref")]
    internal static extern void WebKitUserScriptUnref(IntPtr script);

    [DllImport(WebKitLib, EntryPoint = "webkit_web_view_new_with_user_content_manager")]
    internal static extern IntPtr WebKitWebViewNewWithUserContentManager(IntPtr manager);

    [DllImport(WebKitLib, EntryPoint = "webkit_web_context_get_default")]
    internal static extern IntPtr WebKitWebContextGetDefault();

    [DllImport(WebKitLib, EntryPoint = "webkit_web_context_register_uri_scheme")]
    internal static extern void WebKitWebContextRegisterUriScheme(
        IntPtr context,
        string scheme,
        UriSchemeRequestCallback callback,
        IntPtr userData,
        IntPtr userDataDestroyNotify);

    [DllImport(WebKitLib, EntryPoint = "webkit_web_view_load_uri")]
    internal static extern void WebKitWebViewLoadUri(IntPtr webView, string uri);

    [DllImport(WebKitLib, EntryPoint = "webkit_web_view_get_settings")]
    internal static extern IntPtr WebKitWebViewGetSettings(IntPtr webView);

    [DllImport(WebKitLib, EntryPoint = "webkit_uri_scheme_request_get_uri")]
    internal static extern IntPtr WebKitUriSchemeRequestGetUri(IntPtr request);

    [DllImport(WebKitLib, EntryPoint = "webkit_uri_scheme_request_get_web_view")]
    internal static extern IntPtr WebKitUriSchemeRequestGetWebView(IntPtr request);

    [DllImport(WebKitLib, EntryPoint = "webkit_uri_scheme_request_finish")]
    internal static extern void WebKitUriSchemeRequestFinish(
        IntPtr request,
        IntPtr stream,
        nint streamLength,
        string contentType);

    [DllImport(WebKitLib, EntryPoint = "webkit_web_view_run_javascript")]
    internal static extern void WebKitWebViewRunJavascript(
        IntPtr webView,
        string script,
        IntPtr cancellable,
        GAsyncReadyCallback callback,
        IntPtr userData);

    [DllImport(WebKitLib, EntryPoint = "webkit_web_view_run_javascript_finish")]
    internal static extern IntPtr WebKitWebViewRunJavascriptFinish(
        IntPtr webView,
        IntPtr result,
        out IntPtr error);

    [DllImport(WebKitLib, EntryPoint = "webkit_javascript_result_get_js_value")]
    internal static extern IntPtr WebKitJavascriptResultGetJsValue(IntPtr javascriptResult);

    [DllImport(WebKitLib, EntryPoint = "webkit_javascript_result_unref")]
    internal static extern void WebKitJavascriptResultUnref(IntPtr javascriptResult);

    [DllImport(WebKitLib, EntryPoint = "webkit_settings_set_enable_javascript")]
    internal static extern void WebKitSettingsSetEnableJavascript(
        IntPtr settings,
        [MarshalAs(UnmanagedType.I1)] bool enabled);

    [DllImport(WebKitLib, EntryPoint = "webkit_settings_set_enable_developer_extras")]
    internal static extern void WebKitSettingsSetEnableDeveloperExtras(
        IntPtr settings,
        [MarshalAs(UnmanagedType.I1)] bool enabled);

    [DllImport(WebKitLib, EntryPoint = "webkit_get_major_version")]
    internal static extern uint WebKitGetMajorVersion();

    [DllImport(WebKitLib, EntryPoint = "webkit_get_minor_version")]
    internal static extern uint WebKitGetMinorVersion();

    [DllImport(WebKitLib, EntryPoint = "webkit_get_micro_version")]
    internal static extern uint WebKitGetMicroVersion();

    [DllImport(JavaScriptCoreLib, EntryPoint = "jsc_value_to_string")]
    internal static extern IntPtr JscValueToString(IntPtr value);

    [DllImport(GtkLib, EntryPoint = "gtk_fixed_put")]
    internal static extern void GtkFixedPut(IntPtr container, IntPtr widget, int x, int y);

    [DllImport(GtkLib, EntryPoint = "gtk_widget_show")]
    internal static extern void GtkWidgetShow(IntPtr widget);

    [DllImport(GtkLib, EntryPoint = "gtk_widget_set_size_request")]
    internal static extern void GtkWidgetSetSizeRequest(IntPtr widget, int width, int height);

    [DllImport(GioLib, EntryPoint = "g_memory_input_stream_new_from_data")]
    internal static extern IntPtr GMemoryInputStreamNewFromData(
        IntPtr data,
        nint length,
        DestroyNotify destroy);

    [DllImport(GObjectLib, EntryPoint = "g_signal_connect_data")]
    internal static extern ulong GSignalConnectData(
        IntPtr instance,
        string detailedSignal,
        IntPtr callback,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags);

    [DllImport(GObjectLib, EntryPoint = "g_object_unref")]
    internal static extern void GObjectUnref(IntPtr instance);

    [DllImport(GLibLib, EntryPoint = "g_free")]
    internal static extern void GFree(IntPtr memory);

    [DllImport(GLibLib, EntryPoint = "g_error_free")]
    internal static extern void GErrorFree(IntPtr error);
}
