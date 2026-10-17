namespace NeuralisAPP_1.Services;

internal static class ApiConstants
{
#if ANDROID
    internal const string BaseUrl = "http://10.0.2.2:5084";
#else
    internal const string BaseUrl = "http://localhost:5084";
#endif
}
