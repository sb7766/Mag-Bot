﻿using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Drawing.Imaging;

namespace MagBot
{
    class PonyModule : IModule
    {
        private DiscordClient client;
        private ModuleManager _manager;

        void IModule.Install(ModuleManager manager)
        {
            _manager = manager;
            client = manager.Client;

            manager.CreateCommands("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.User);
                // Derpibooru search using default filter
                cgb.CreateCommand("derpi")
                    .Description("Returns a random image from derpibooru with the specified search tags. Separate tags with commas. Uses default filter.")
                    .Parameter("tags", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = DerpiSearch(e.GetArg("tags"), false);
                        await e.Channel.SendMessage(search);
                    });

                // Derpibooru search using everything filter
                cgb.CreateCommand("derpi_all")
                    .Description("Returns a random image from derpibooru with the specified search tags. Separate tags with commas. Uses everything filter. **Result may be NSFW!**")
                    .Parameter("tags", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        string search = DerpiSearch(e.GetArg("tags"), true);
                        await e.Channel.SendMessage(search);
                    });

                cgb.CreateCommand("genpony")
                    .Description("Generates a random pony for you to draw!")
                    .Do(async e =>
                    {
                        Image[] files = 
                        {
                            Image.FromFile("resources/gifs/tumblr_o5n1dwnPrz1v7wqx6o2_500.gif"),
                            Image.FromFile("resources/gifs/tumblr_o5n1dwnPrz1v7wqx6o3_500.gif"),
                            Image.FromFile("resources/gifs/tumblr_o5n1dwnPrz1v7wqx6o4_500.gif"),
                            Image.FromFile("resources/gifs/tumblr_o5n1dwnPrz1v7wqx6o5_500.gif"),
                            Image.FromFile("resources/gifs/tumblr_o5n1dwnPrz1v7wqx6o6_500.gif"),
                            Image.FromFile("resources/gifs/tumblr_o5n1dwnPrz1v7wqx6o7_500.gif")
                        };
                        List<Image> gifs = new List<Image>(files);
                        List<Image> frames = new List<Image>();
                        Random rand = new Random();
                        foreach (var g in gifs)
                        {
                            FrameDimension dimension = new FrameDimension(g.FrameDimensionsList[0]);
                            int count = g.GetFrameCount(dimension);
                            int frame = rand.Next(1, count);
                            g.SelectActiveFrame(dimension, frame);
                            frames.Add(g);
                        }
                        Bitmap complete = new Bitmap(frames[0].Width + frames[1].Width + frames[2].Width, frames[0].Height + frames[3].Height);
                        using (Graphics g = Graphics.FromImage(complete))
                        {
                            g.DrawImage(frames[0], 0, 0);
                            g.DrawImage(frames[1], frames[0].Width, 0);
                            g.DrawImage(frames[2], frames[0].Width + frames[1].Width, 0);
                            g.DrawImage(frames[3], 0, frames[0].Height);
                            g.DrawImage(frames[4], frames[3].Width, frames[1].Height);
                            g.DrawImage(frames[5], frames[3].Width + frames[4].Width, frames[2].Height);
                        }
                        complete.Save("resources/gifs/complete.png", ImageFormat.Png);
                        
                        await e.Channel.SendFile("resources/gifs/complete.png");
                    });
            });
        }

        private string DerpiSearch(string searchstring, bool all)
        {
            searchstring = searchstring.Replace(" ", "+");
            searchstring = searchstring.Replace(",", "%2C");
            if (searchstring == "")
            {
                searchstring = "id.gte:0";
            }
            string json = "";
            if (all)
            {
                json = new WebClient().DownloadString("https://derpibooru.org/search.json?filter_id=56027&sf=random&q=" + searchstring);
            }
            else
            {
                json = new WebClient().DownloadString("https://derpibooru.org/search.json?filter_id=100073&sf=random&q=" + searchstring);
            }

            try
            {
                dynamic images = JObject.Parse(json);
                dynamic image = images["search"][0];

                string id = image.id;
                string rawimage = image.representations.full;

                string filetype = rawimage.Substring(rawimage.LastIndexOf('.'));
                rawimage = rawimage.Remove(rawimage.IndexOf("__"));
                rawimage = rawimage + filetype;
                return "Here you go! https://derpibooru.org/" + id + " https:" + rawimage;
            }
            catch
            {
                return "Search returned no results.";
            }
        }
    }
}