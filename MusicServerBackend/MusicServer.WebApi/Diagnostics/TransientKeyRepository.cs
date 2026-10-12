using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace MusicServer.Diagnostics;

// .NET's key-ring warmup must also stay in memory in this temporary experiment.
// No profile key directory, persisted key, or production key ring is involved.
public sealed class TransientKeyRepository : IXmlRepository
{
    private readonly List<XElement> elements = [];
    private readonly object gate = new();

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (gate) return elements.Select(element => new XElement(element)).ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (gate) elements.Add(new XElement(element));
    }
}
