using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Interactivity;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using LiteDB;
using Resonance.Services;
using Resonance.Modules;
using System.Collections.Generic;
using DSharpPlus.Interactivity.Extensions;

namespace Resonance
{
    class Program
    {
        private static string Token = "";
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            // Create the Data folder if it isn't already existing
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Data"));

            // Attempt to read the Token file, throws an error if it's not there.
            try
            {
                Token = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Data", "token.txt"));
            }
            catch
            {
                Console.WriteLine("No Token file found! Please create a file called \"token.txt\" in the Data folder.");
            }

            // Configure the discord client entitiy.    
            var client = new DiscordClient(new DiscordConfiguration()
            {
                Token = Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All,
                LogTimestampFormat = "MMM dd yyyy - hh:mm:ss tt",
                MinimumLogLevel = LogLevel.Debug
            });

            // Create the Dependency Injection services to be used within comman modules.
            var services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(new LiteDatabase(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Database.db")))
                .AddSingleton<Services.Utilities>()
                .AddSingleton<ButtonService>()
                .BuildServiceProvider();

            // Initiate the use of Slash Commands
            var Slash = client.UseSlashCommands(new SlashCommandsConfiguration()
            {
                Services = services
            });

            client.UseInteractivity(new InteractivityConfiguration()
            {
                PollBehaviour = DSharpPlus.Interactivity.Enums.PollBehaviour.KeepEmojis,
                Timeout = TimeSpan.FromSeconds(30)
            });





            // Register all command modules.
            Slash.RegisterCommands<CharacterModule>();
            Slash.RegisterCommands<SkillManagement>();
            Slash.RegisterCommands<InventoryManagement>();
            Slash.RegisterCommands<GameplayModule>();
            Slash.RegisterCommands<HelpModule>();



            // Register the Button Handling method into the Client.
            client.ComponentInteractionCreated += services.GetService<ButtonService>().HandleButtonAsync;
            client.Heartbeated += OnReady;


            //client.Heartbeated += async (client, e) =>
            //{
            //    // This grants user Vyklade (Bot Owner) Permission to the otherwise locked Corebook management commands.
            //    foreach (var g in client.Guilds.Select(x => x.Value))
            //    {
            //        var AppPerms = new List<DiscordGuildApplicationCommandPermissions>();

            //        var commands = Slash.RegisteredCommands;

            //        foreach (var c in commands[0].Value)
            //        {
            //            try
            //            {
            //                var m = await g.GetMemberAsync(165212654388903936);
            //                AppPerms.Add(new DiscordGuildApplicationCommandPermissions(c.Id,
            //                new DiscordApplicationCommandPermission[] { new DiscordApplicationCommandPermission(m, true) }));
            //            }
            //            catch
            //            {
            //                continue;
            //            }
            //        }

            //        await g.BatchEditApplicationCommandPermissionsAsync(AppPerms);
            //    }
            //};


            await client.ConnectAsync();


            await Task.Delay(-1);
        }

        private async static Task OnReady(DiscordClient sender, DSharpPlus.EventArgs.HeartbeatEventArgs e)
        {
            // await sender.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<DiscordApplicationCommand>());
        }

    }
}
