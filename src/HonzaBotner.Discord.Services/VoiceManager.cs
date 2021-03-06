﻿using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using HonzaBotner.Discord.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonzaBotner.Discord.Services
{
    public class VoiceManager : IVoiceManager
    {
        private readonly IGuildProvider _guildProvider;
        private readonly DiscordWrapper _discordWrapper;
        private readonly CustomVoiceOptions _voiceConfig;
        private readonly ILogger<VoiceManager> _logger;

        private bool _initialized;

        private DiscordClient Client => _discordWrapper.Client;

        public VoiceManager(IGuildProvider guildProvider, DiscordWrapper discordWrapper,
            IOptions<CustomVoiceOptions> options, ILogger<VoiceManager> logger)
        {
            _guildProvider = guildProvider;
            _discordWrapper = discordWrapper;
            _voiceConfig = options.Value;
            _logger = logger;
        }

        public async Task Init()
        {
            if (!_initialized)
            {
                _initialized = true;

                Client.VoiceStateUpdated += Client_VoiceStateUpdated;

                // Startup cleaning.
                await DeleteAllUnusedVoiceChannelsAsync();
            }
        }

        private async Task Client_VoiceStateUpdated(DiscordClient client, VoiceStateUpdateEventArgs args)
        {
            if (args.After.Channel?.Id == _voiceConfig.ClickChannelId)
            {
                await AddNewVoiceChannelAsync(args.Channel, await args.Guild.GetMemberAsync(args.User.Id));
            }

            if (args.Before?.Channel != null)
            {
                if (args.Before.Channel.Id != _voiceConfig.ClickChannelId)
                {
                    await DeleteUnusedVoiceChannelAsync(args.Before.Channel);
                }
            }
        }

        public async Task AddNewVoiceChannelAsync(
            DiscordChannel channelToCloneFrom, DiscordMember member,
            string? name = null, int? limit = 0)
        {
            name = ConvertStringToValidState(name);

            try
            {
                string? userName = ConvertStringToValidState(member.Nickname, member.Username);
                DiscordChannel newChannel =
                    await channelToCloneFrom.CloneAsync($"Member {userName} created new voice channel.");

                await EditChannelAsync(newChannel, name, limit, userName);

                try
                {
                    if (member.VoiceState?.Channel != null)
                    {
                        await member.PlaceInAsync(newChannel);
                    }
                }
                catch
                {
                    // User disconnected while we were placing them.
                }

                // Placing the member in the channel failed, so remove it after some time.
                var _ = Task.Run(async () =>
                {
                    await Task.Delay(1000 * _voiceConfig.RemoveAfterCommandInSeconds);
                    await DeleteUnusedVoiceChannelAsync(newChannel);
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Creating voice channel failed.");
            }
        }

        public async Task<bool> EditVoiceChannelAsync(DiscordMember member, string? newName = null, int? limit = 0)
        {
            if (member.VoiceState?.Channel == null || member.VoiceState?.Channel.Id == _voiceConfig.ClickChannelId)
            {
                return false;
            }

            newName = ConvertStringToValidState(newName);

            DiscordChannel customVoiceCategory = member.Guild.GetChannel(_voiceConfig.ClickChannelId).Parent;

            if (!customVoiceCategory.Equals(member.VoiceState?.Channel?.Parent))
            {
                return false;
            }

            try
            {
                string? userName = ConvertStringToValidState(member.Nickname, member.Username);
                await EditChannelAsync(member.VoiceState?.Channel, newName, limit, userName);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Editing voice channel failed.");
            }

            return false;
        }

        private async Task DeleteUnusedVoiceChannelAsync(DiscordChannel channel)
        {
            if (channel.Id == _voiceConfig.ClickChannelId) return;

            if (!channel.Parent.Equals(channel.Guild.GetChannel(_voiceConfig.ClickChannelId).Parent)) return;

            if (!channel.Users.Any())
            {
                try
                {
                    await channel.DeleteAsync();
                }
                catch
                {
                    // ignored
                }
            }
        }

        private async Task DeleteAllUnusedVoiceChannelsAsync()
        {
            DiscordGuild guild = await _guildProvider.GetCurrentGuildAsync();
            DiscordChannel customVoiceCategory = guild.GetChannel(_voiceConfig.ClickChannelId).Parent;
            foreach (DiscordChannel discordChannel in customVoiceCategory.Children)
            {
                await DeleteUnusedVoiceChannelAsync(discordChannel);
            }
        }

        private string? ConvertStringToValidState(string? input, string? defaultValue = null)
        {
            input = Regex.Replace(input ?? "", @"[^\u0000-\u007F]+", string.Empty);

            if (input.Trim().Length == 0)
            {
                return defaultValue;
            }

            return input.Substring(0, Math.Min(input.Length, 30));
        }

        private async Task EditChannelAsync(DiscordChannel? channel, string? name, int? limit, string? userName)
        {
            if (channel == null) return;

            await channel.ModifyAsync(model =>
            {
                model.Name = name ?? $"{userName ?? "FIŤÁK"}'s channel";

                if (limit != null)
                {
                    model.Userlimit = Math.Max(Math.Min(limit.Value, 99), 0);
                }
            });
        }
    }
}
