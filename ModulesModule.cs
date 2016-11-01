using Discord;
using Discord.Commands.Permissions.Levels;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Module Manager Extended
/// v0.9-rc4-2
/// Written by foxbot, based off of Voltana's Module Manager
/// </summary>
namespace MagBot
{
    //TODO: Save what modules have been enabled on each server
    internal class ModuleManagerExtended : IModule
    {
        private static ModuleManager _manager;
        private static DiscordClient _client;
        private static ModuleService _service;

        void IModule.Install(ModuleManager manager)
        {
            _manager = manager;
            _client = manager.Client;
            _service = manager.Client.GetService<ModuleService>(true);

            manager.CreateCommands("modules", group =>
            {
                group.MinPermissions((int)PermissionLevel.ServerAdmin);
                group.CreateCommand("list")
                    .Description("Gives a list of all available modules.")
                    .Do(async e =>
                    {
                        string text = "Available Modules: " + string.Join(", ", _service.Modules.Select(x => x.Id));
                        await e.Channel.SendMessage(text);
                    });
                group.CreateCommand("enable")
                    .Description("Enables a module for this server.")
                    .Parameter("module")
                    .PublicOnly()
                    .Do(async e =>
                    {
                        var module = GetModule(e.Args[0]);
                        if (module == null)
                        {
                            await e.Channel.SendMessage("Unknown module");
                            return;
                        }
                        if (module.FilterType == ModuleFilter.None || module.FilterType == ModuleFilter.AlwaysAllowPrivate)
                        {
                            await e.Channel.SendMessage("This module is global and cannot be enabled/disabled.");
                            return;
                        }
                        if (!module.FilterType.HasFlag(ModuleFilter.ServerWhitelist))
                        {
                            await e.Channel.SendMessage("This module doesn't support being enabled for servers.");
                            return;
                        }
                        var server = e.Server;
                        if (!module.EnableServer(server))
                        {
                            await e.Channel.SendMessage($"Module {module.Id} was already enabled for server {server.Name}.");
                            return;
                        }
                        if (e.Args[0] == "ranks")
                        {
                            await SilvRankModule.ServerInit(server);
                        }
                        List<string> settings = new List<string>();
                        if (!Program.enabledmodules.TryGetValue(e.Server.Id, out settings))
                        {
                            settings = new List<string>();
                        }
                        try { settings.Add(module.Id); Program.enabledmodules[e.Server.Id] = settings; } catch (Exception ex) { _client.Log.Log(LogSeverity.Error, "Modules", "Couldn't add a module to config", ex); }
                        await e.Channel.SendMessage($"Module {module.Id} was enabled for server {server.Name}.");
                    });
                group.CreateCommand("disable")
                    .Description("Disables a module for this server.")
                    .Parameter("module")
                    .PublicOnly()
                    .Do(e =>
                    {
                        var module = GetModule(e.Args[0]);
                        if (module == null)
                        {
                            e.Channel.SendMessage("Unknown module");
                            return;
                        }
                        if (module.FilterType == ModuleFilter.None || module.FilterType == ModuleFilter.AlwaysAllowPrivate)
                        {
                            e.Channel.SendMessage("This module is global and cannot be enabled/disabled.");
                            return;
                        }
                        if (!module.FilterType.HasFlag(ModuleFilter.ServerWhitelist))
                        {
                            e.Channel.SendMessage("This module doesn't support being enabled for servers.");
                            return;
                        }
                        var server = e.Server;
                        if (!module.DisableServer(server))
                        {
                            e.Channel.SendMessage($"Module {module.Id} was not enabled for server {server.Name}.");
                            return;
                        }
                        List<string> settings = new List<string>();
                        if (!Program.enabledmodules.TryGetValue(e.Server.Id, out settings))
                        {
                            settings = new List<string>();
                        }
                        try { settings.Remove(module.Id); Program.enabledmodules[e.Server.Id] = settings; } catch (Exception ex) { _client.Log.Log(LogSeverity.Error, "Modules", "Couldn't add a module from the config", ex); }
                        e.Channel.SendMessage($"Module {module.Id} was disabled for server {server.Name}.");
                    });

                _client.ServerAvailable += (s, e) => LoadModules(e.Server);
            });
        }

        private ModuleManager GetModule(string id)
        {
            id = id.ToLowerInvariant();
            return _service.Modules.FirstOrDefault(x => x.Id == id);
        }

        private void LoadModules(Server s)
        {
            try
            {
                List<string> settings = new List<string>();
                if (Program.enabledmodules.TryGetValue(s.Id, out settings))
                {
                    foreach (var m in settings)
                    {
                        var module = GetModule(m);
                        module.EnableServer(s);
                        _client.Log.Log(LogSeverity.Info, "Modules", $"Enabled Module {m} for {s.Name}");
                    }
                }
                
            }
            catch (Exception ex) { _client.Log.Error("Modules", ex); }
        }
    }
}