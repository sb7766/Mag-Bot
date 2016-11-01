using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Modules;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace MagBot
{
    class FurryModule : IModule
    {
        private DiscordClient client;
        private ModuleManager manager;

        void IModule.Install(ModuleManager _manager)
        {
            manager = _manager;
            client = manager.Client;

            manager.CreateCommands("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.User);

                cgb.CreateCommand("e621")
                    .Description("Returns a random image from e621 with the specified search tags. Maximum of 4 tags.")
                    .Parameter("search", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = e.GetArg("search");
                        string urls = e621(search, false);
                        await e.Channel.SendMessage(urls);
                    });

                cgb.CreateCommand("e621_all")
                    .Description("Returns a random image from e621 with the specified search tags. Maximum of 5 tags. **Result may be NSFW!**")
                    .Parameter("search", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = e.GetArg("search");
                        string urls = e621(search, true);
                        await e.Channel.SendMessage(urls);
                    });
            });
        }

        private string e621(string search, bool all)
        {
            if(search.Contains(","))
            {
                List<string> splitsearch = new List<string>(search.Split(','));
                for (int i = 0; i < splitsearch.Count; i++)
                {
                    string tag = splitsearch[i];
                    tag = tag.Trim(' ');
                    tag = tag.Replace(" ", "_");
                    splitsearch[i] = tag;
                }
                search = string.Join("+", splitsearch);
            }
            else
            {
                search.Replace(' ', '+');
            }

            string searchstring = "https://e621.net/post/index.json?tags=order:random+" + search;
            if (!all)
            {
                searchstring += "+rating:s";
            }
            string userAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
            string json = "";
            using (WebClient web = new WebClient())
            {
                web.Headers[HttpRequestHeader.UserAgent] = userAgent;
                json = web.DownloadString(searchstring);
            }

            string urls = "";
            try
            {
                dynamic images = JArray.Parse(json);
                dynamic image = images[0];

                string id = image.id;
                string rawimage = image.file_url;
                urls = "Here you go! https://e621.net/post/show/" + id + $" {rawimage}";
            }
            catch
            {
                return "Search returned no results.";
            }
            

            return urls;
        }
    }
}
