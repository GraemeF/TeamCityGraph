using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TeamCityGraph
{
    public static class ResponseExtensions
    {
        public static async Task<XDocument> GetXmlFromResponse(this HttpResponseMessage getServer)
        {
            return XDocument.Load(await getServer.Content.ReadAsStreamAsync());
        }
    }
}