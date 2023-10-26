namespace Circles.Index.Pathfinder;

public static class RpcCalls
{
    public static string LoadSafesBinary(string pathToBinaryDump)
    {
        return "{\n    \"id\":\"" + DateTime.Now.Ticks +
               "\", \n    \"method\": \"load_safes_binary\", \n    \"params\": {\n        \"file\": \"" +
               pathToBinaryDump + "\"\n    }\n}";
    }
}
