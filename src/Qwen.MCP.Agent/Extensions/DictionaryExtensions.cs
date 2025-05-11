using Microsoft.KernelMemory;

namespace Qwen.MCP.Agent.Extensions;

public static class DictionaryExtensions 
{
    public static TagCollection ToTagCollection(
        this IDictionary<string, List<string?>> dict
    )
    {
        var tags = new TagCollection();
        foreach(var (key, value) in dict)
        {
            tags[key] = value;
        }
        return tags;
    }
}
