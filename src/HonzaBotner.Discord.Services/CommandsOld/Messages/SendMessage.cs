﻿// using System;
// using System.Text.RegularExpressions;
// using System.Threading;
// using System.Threading.Tasks;
// using DSharpPlus;
// using DSharpPlus.Entities;
// using HonzaBotner.Discord.Command;
// using Microsoft.Extensions.Logging;
//
// namespace HonzaBotner.Discord.Services.Commands.Messages
// {
//     public class SendMessage : BaseCommand
//     {
//         public const string ChatCommand = "send";
//         // ;send #general <message>
//
//         protected override bool CanBotExecute => false;
//
//         protected override CommandPermission RequiredPermission => CommandPermission.Mod;
//
//         public SendMessage(IPermissionHandler permissionHandler, ILogger<SendMessage> logger)
//             : base(permissionHandler, logger)
//         {
//         }
//
//         protected override async Task<ChatCommendExecutedResult> ExecuteAsync(DiscordClient client,
//             DiscordMessage message, CancellationToken cancellationToken = default)
//         {
//             if (message.MentionedChannels.Count.Equals(0)) return ChatCommendExecutedResult.WrongSyntax;
//             if (message.Content.Split(" ").Length < 3) return ChatCommendExecutedResult.WrongSyntax;
//
//             var channel = message.MentionedChannels[0];
//             string channelMention = message.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries)[1];
//
//             // First argument isn't a channel mention.
//             if ($"<#{channel.Id}>" != channelMention)
//             {
//                 return ChatCommendExecutedResult.WrongSyntax;
//             }
//
//             // Remove command and channel mention from message.
//             // TODO: maybe remove command part to utils?
//             const string pattern = @"^.\w+\s+<#\w+>\s+";
//             string text = message.Content;
//             string sendMessage = Regex.Replace(text, pattern, "");
//             await client.SendMessageAsync(channel, sendMessage);
//
//             return ChatCommendExecutedResult.Ok;
//         }
//     }
// }
