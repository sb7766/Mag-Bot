using Discord.Modules;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Customsearch.v1;
using Google.Apis.Customsearch.v1.Data;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System.Xml.Linq;
using System.Net;

namespace MagBot
{
    class Search : IModule
    {
        private DiscordClient client;
        private ModuleManager manager;
        private const string apiKey = "AIzaSyB7QVhQjloGJvKqTxvvKhOxrAThBOigBl8";

        void IModule.Install(ModuleManager _manager)
        {
            manager = _manager;
            client = manager.Client;

            manager.CreateCommands("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.User);

                cgb.CreateCommand("google")
                    .Alias("g")
                    .Description("Search google for something.")
                    .Parameter("search", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = e.GetArg("search");
                        string url = google(search, "normal");
                        await e.Channel.SendMessage($"Here you go! {url}");
                    });

                cgb.CreateCommand("googleimages")
                    .Alias("gi")
                    .Description("Search google images for something.")
                    .Parameter("search", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = e.GetArg("search");
                        string url = google(search, "image");
                        await e.Channel.SendMessage($"Here you go! {url}");
                    });

                cgb.CreateCommand("youtube")
                    .Alias("yt")
                    .Description("Search YouTube for something.")
                    .Parameter("search", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = e.GetArg("search");
                        string url = youtube(search);
                        await e.Channel.SendMessage($"Here you go! {url}");
                    });

                cgb.CreateCommand("deviantart")
                    .Description("Searches DeviantArt and returns the newest image matching the search.")
                    .Alias("da")
                    .Parameter("search", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = e.GetArg("search");
                        search.Replace(" ", "+");
                        string output;
                        XDocument xml;
                        string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
                        string url = "http://backend.deviantart.com/rss.xml?order=5&q=" + search;
                        using (WebClient web = new WebClient())
                        {
                            web.Headers[HttpRequestHeader.UserAgent] = userAgent;
                            xml = XDocument.Parse(web.DownloadString(url));
                        }

                        var reader = xml.CreateReader();
                        reader.ReadToFollowing("media:content");
                        reader.MoveToAttribute("url");
                        output = reader.Value;

                        await e.Channel.SendMessage($"Here you go! {output}");
                    });
            });
        }

        private string google(string search, string type)
        {
            const string cseId = "015503969221380279001:nb-fty02lv0";
            var service = new CustomsearchService(new BaseClientService.Initializer
            {
                ApplicationName = "Mag-Bot",
                ApiKey = apiKey
            });

            CseResource.ListRequest request = service.Cse.List(search);
            request.Cx = cseId;
            if (type == "image")
            {
                request.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            }
            Google.Apis.Customsearch.v1.Data.Search result = request.Execute();
            string url = result.Items[0].Link;

            return url;
        }

        private string youtube(string search)
        {
            var service = new YouTubeService(new BaseClientService.Initializer
            {
                ApplicationName = "Mag-Bot",
                ApiKey = apiKey
            });

            var request = service.Search.List("snippet");
            request.Q = search;
            var result = request.Execute();

            string url = "https://youtube.com/watch?v=";

            foreach (var item in result.Items)
            {
                if (item.Id.Kind == "youtube#video")
                {
                    url += item.Id.VideoId;
                    return url;
                }
            }

            return url;
        }
    }
}
