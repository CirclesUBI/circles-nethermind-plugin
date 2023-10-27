using System.Runtime.InteropServices;

namespace Circles.Index.Pathfinder;

public static class LibPathfinder
{
    const string DLL_PATH = "libpathfinder2.so";

    [DllImport(DLL_PATH, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ffi_initialize();

    [DllImport(DLL_PATH, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ffi_load_safes_binary(string file);

    [DllImport(DLL_PATH, CallingConvention = CallingConvention.Cdecl)]
    public static extern string ffi_compute_transfer(string request_json);
}
