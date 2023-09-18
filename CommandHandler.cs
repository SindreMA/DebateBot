using System;
using Discord;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;
using Discord.Commands;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace DebateBot
{
    class CommandHandler
    {
        private DiscordSocketClient _client;
        private CommandService _service;
        public CommandHandler(DiscordSocketClient client)
        {
            _client = client;
            _service = new CommandService();
            _service.AddModulesAsync(Assembly.GetEntryAssembly());
            _client.MessageReceived += _client_MessageReceived;
        }
        private void Save()
        {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Program.settings, Formatting.Indented));
        }
        private async Task _client_MessageReceived(SocketMessage arg)
        {
            var msg = arg as SocketUserMessage;
            if (msg == null) return;

            var context = new SocketCommandContext(_client, msg);
            int argPost = 0;
            if (msg.HasCharPrefix('.', ref argPost))
            {
                var result = _service.ExecuteAsync(context, argPost);
                if (!result.Result.IsSuccess && result.Result.Error != CommandError.UnknownCommand && !result.Result.ErrorReason.Contains("Objektreferanse er ikke satt til en objektforekomst"))
                {
                    await context.Channel.SendMessageAsync(result.Result.ErrorReason);
                }
                await Program.Log("Invoked " + msg + " in " + context.Channel + " with " + result.Result, ConsoleColor.Magenta);
            }
            else
            {
                await Program.Log(context.Channel + "-" + context.User.Username + " : " + msg, ConsoleColor.White);
            }

        }
    }
}