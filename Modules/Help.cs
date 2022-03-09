using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Text;
using LiteDB;
using System.Threading.Tasks;
using Resonance.Collections;
using System.Linq;
using Resonance.Services;

namespace Resonance.Modules
{

    
    public class HelpModule : ApplicationCommandModule
    {
        public Services.Utilities Utils;
        public LiteDatabase db;

        [SlashCommand("Help","Open the help menu.")]
        public async Task Help(InteractionContext context)
        {
            await context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Choose a help topic!")
                    .AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Primary,"h0,0","Commands"),
                        new DiscordLinkButtonComponent("https://scribe.pf2.tools/v/fsmjQlP8","Corebook")
                    }));
        }
    }
}
