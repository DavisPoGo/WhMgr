﻿namespace WhMgr.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DSharpPlus;
    using DSharpPlus.CommandsNext;
    using DSharpPlus.Entities;
    using DSharpPlus.Interactivity;

    using WhMgr.Configuration;
    using WhMgr.Data;
    using WhMgr.Diagnostics;
    using WhMgr.Localization;

    public static class DiscordExtensions
    {
        public const string ConfirmRegex = "\\b[Yy][Ee]?[Ss]?\\b|\\b[Nn][Oo]?\\b";
        public const string YesRegex = "[Yy][Ee]?[Ss]?";
        //private const string NoRegex = "[Nn][Oo]?";

        private static readonly IEventLogger _logger = EventLogger.GetLogger("DISCORD_EXTENSIONS");

        #region Messages

        public static async Task<List<DiscordMessage>> RespondEmbed(this DiscordMessage msg, string message)
        {
            return await msg.RespondEmbed(message, DiscordColor.Green);
        }

        public static async Task<List<DiscordMessage>> RespondEmbed(this DiscordMessage discordMessage, string message, DiscordColor color)
        {
            var messagesSent = new List<DiscordMessage>();
            var messages = message.SplitInParts(2048);
            foreach (var msg in messages)
            {
                var eb = new DiscordEmbedBuilder
                {
                    Color = color,
                    Description = msg
                };

                messagesSent.Add(await discordMessage.RespondAsync(embed: eb));
            }
            return messagesSent;
        }

        public static async Task<List<DiscordMessage>> RespondEmbed(this CommandContext ctx, string message)
        {
            return await RespondEmbed(ctx, message, DiscordColor.Green);
        }

        public static async Task<List<DiscordMessage>> RespondEmbed(this CommandContext ctx, string message, DiscordColor color)
        {
            var messagesSent = new List<DiscordMessage>();
            var messages = message.SplitInParts(2048);
            foreach (var msg in messages)
            {
                var eb = new DiscordEmbedBuilder
                {
                    Color = color,
                    Description = msg
                };

                await ctx.TriggerTypingAsync();
                messagesSent.Add(await ctx.RespondAsync(embed: eb));
            }
            return messagesSent;
        }

        public static async Task<DiscordMessage> SendDirectMessage(this DiscordMember user, DiscordEmbed embed)
        {
            if (embed == null)
                return null;

            return await user.SendDirectMessage(string.Empty, embed);
        }

        public static async Task<DiscordMessage> SendDirectMessage(this DiscordMember user, string message, DiscordEmbed embed)
        {
            try
            {
                //var dm = await client.CreateDmAsync(user);
                //if (dm != null)
                //{
                    var msg = await user.SendMessageAsync(message, false, embed);
                    return msg;
                //}
            }
            catch (Exception)
            {
                //_logger.Error(ex);
                _logger.Error($"Failed to send DM to user {user.Username}.");
            }

            return null;
        }

        #endregion

        public static async Task<DiscordMember> GetMemberById(this DiscordClient client, ulong guildId, ulong id)
        {
            if (!client.Guilds.ContainsKey(guildId))
                return null;

            var guild = client.Guilds[guildId];
            if (guild == null)
                return null;

            var members = guild.Members;
            if (members?.Count <= 0)
                return null;

            DiscordMember member = null;
            try
            {
                member = members?.FirstOrDefault(x => x.Key == id).Value;
            }
            catch { }
            if (member == null)
            {
                try
                {
                    member = await guild.GetMemberAsync(id);
                }
                catch
                {
                    return null;
                }
            }

            return member;
        }

        public static async Task<DiscordMessage> DonateUnlockFeaturesMessage(this CommandContext ctx, bool triggerTyping = true)
        {
            if (triggerTyping)
            {
                await ctx.TriggerTypingAsync();
            }

            var lang = (Translator)ctx.Services.GetService(typeof(Translator));
            var message = lang != null ? 
                    lang.Translate("DONATE_MESSAGE").FormatText(ctx.User.Username) :
                    $"{ctx.User.Username} This feature is only available to supporters, please donate to unlock this feature and more.\r\n\r\n" +
                    $"Donation information can be found by typing the `donate` command.\r\n\r\n" +
                    $"*If you have already donated and are still receiving this message, please tag an Administrator or Moderator for help.*";
            var eb = await ctx.RespondEmbed(message);
            return eb.FirstOrDefault();
        }

        internal static async Task<bool> IsDirectMessageSupported(this DiscordMessage message)
        {
            if (message?.Channel?.Guild == null)
            {
                //TODO: Localize
                await message.RespondEmbed($"{message.Author.Mention} Direct message is not supported for this command.", DiscordColor.Yellow);
                return false;
            }

            return true;
        }

        public static ulong ContextToGuild(this CommandContext ctx, Dictionary<ulong, DiscordClient> servers)
        {
            var keys = servers.Keys.ToList();
            for (var i = 0; i < keys.Count; i++)
            {
                var guildId = keys[i];
                if (!servers.ContainsKey(guildId))
                    continue;

                if (ctx.Client.CurrentUser.Id != servers[guildId].CurrentUser.Id)
                    continue;

                return guildId;
            }
            return 0;
        }

        #region Roles

        public static bool IsSupporterOrHigher(this DiscordClient client, ulong userId, ulong guildId, WhConfig config)
        {
            try
            {
                if (!config.Servers.ContainsKey(guildId))
                    return false;

                var server = config.Servers[guildId];

                var isAdmin = userId == server.OwnerId;
                if (isAdmin)
                    return true;

                var isModerator = server.Moderators.Contains(userId);
                if (isModerator)
                    return true;

                var isSupporter = client.HasSupporterRole(server.GuildId, userId, server.DonorRoleIds);
                if (isSupporter)
                    return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return false;
        }

        public static bool IsModeratorOrHigher(this ulong userId, ulong guildId, WhConfig config)
        {
            if (!config.Servers.ContainsKey(guildId))
                return false;

            var server = config.Servers[guildId];

            var isAdmin = IsAdmin(userId, server.OwnerId);
            if (isAdmin)
                return true;

            var isModerator = server.Moderators.Contains(userId);
            if (isModerator)
                return true;

            return false;
        }

        public static bool IsModerator(this ulong userId, ulong guildId, WhConfig config)
        {
            if (!config.Servers.ContainsKey(guildId))
                return false;

            return config.Servers[guildId].Moderators.Contains(userId);
        }

        public static bool IsAdmin(this ulong userId, ulong ownerId)
        {
            return userId == ownerId;
        }

        public static bool HasSupporterRole(this DiscordClient client, ulong guildId, ulong userId, List<ulong> supporterRoleIds)
        {
            if (!client.Guilds.ContainsKey(guildId))
                return false;

            var guild = client.Guilds[guildId];
            var member = guild.Members.FirstOrDefault(x => x.Key == userId).Value;
            if (member == null)
            {
                _logger.Error($"Failed to get user with id {userId}.");
                return false;
            }

            return member.HasSupporterRole(supporterRoleIds);
        }

        public static bool HasSupporterRole(this DiscordMember member, List<ulong> supporterRoleIds)
        {
            for (var i = 0; i < supporterRoleIds.Count; i++)
            {
                if (HasRole(member, supporterRoleIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<bool> HasModeratorRole(this DiscordClient client, ulong guildId, ulong userId, ulong moderatorRoleId)
        {
            var member = await client.GetMemberById(guildId, userId);
            if (member == null)
            {
                _logger.Error($"Failed to get moderator user with id {userId}.");
                return false;
            }

            return member.HasModeratorRole(moderatorRoleId);
        }

        public static bool HasModeratorRole(this DiscordMember member, ulong moderatorRoleId)
        {
            return HasRole(member, moderatorRoleId);
        }

        public static bool HasRole(this DiscordMember member, ulong roleId)
        {
            try
            {
                var role = member?.Roles.FirstOrDefault(x => x.Id == roleId);
                return role != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasRole(this DiscordClient client, DiscordMember member, string roleName)
        {
            var role = client.GetRoleFromName(roleName);
            if (role == null) return false;

            return HasRole(member, role.Id);
        }

        public static DiscordRole GetRoleFromName(this DiscordClient client, string roleName)
        {
            foreach (var guild in client.Guilds)
            {
                var role = guild.Value.Roles.FirstOrDefault(x => string.Compare(x.Value.Name, roleName, true) == 0).Value;
                if (role != null)
                {
                    return role;
                }
            }

            return null;
        }

        #endregion

        public static async Task<Tuple<DiscordChannel, long>> DeleteMessages(this DiscordClient client, ulong channelId)
        {
            var deleted = 0L;
            DiscordChannel channel;
            try
            {
                channel = await client.GetChannelAsync(channelId);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                _logger.Debug($"Failed to get Discord channel {channelId}, skipping...");
                return null;
            }

            if (channel == null)
            {
                _logger.Warn($"Failed to find channel by id {channelId}, skipping...");
                return null;
            }

            var messages = await channel?.GetMessagesAsync();
            if (messages == null)
                return null;

            while (messages.Count > 0)
            {
                for (var j = 0; j < messages.Count; j++)
                {
                    var message = messages[j];
                    if (message == null)
                        continue;

                    try
                    {
                        await message.DeleteAsync("Channel reset.");
                        deleted++;
                    }
                    catch { continue; }
                }

                try
                {
                    messages = await channel.GetMessagesAsync();
                }
                catch (Newtonsoft.Json.JsonReaderException ex)
                {
                    _logger.Error(ex);
                    continue;
                }
            }

            return Tuple.Create(channel, deleted);
        }

        #region Emojis

        public static ulong? GetEmojiId(this DiscordGuild guild, string emojiName)
        {
            return guild.Emojis.FirstOrDefault(x => string.Compare(x.Value.Name, emojiName, true) == 0).Key;
        }

        public static string GetEmoji(this string emojiName)
        {
            if (!MasterFile.Instance.Emojis.ContainsKey(emojiName))
            {
                return null;
            }
            return string.Format(Strings.EmojiSchema, emojiName, MasterFile.Instance.Emojis[emojiName]);
        }

        #endregion

        public static async Task<bool> Confirm(this CommandContext ctx, string message)
        {
            await ctx.RespondEmbed(message);
            var interactivity = (InteractivityExtension)ctx.Services.GetService(typeof(InteractivityExtension));//ctx.Client.GetModule<InteractivityModule>();
            if (interactivity == null)
            {
                _logger.Error("Interactivity model failed to load!");
                return false;
            }

            var m = await interactivity.WaitForMessageAsync(
                x => x.Channel.Id == ctx.Channel.Id
                && x.Author.Id == ctx.User.Id
                && Regex.IsMatch(x.Content, ConfirmRegex), 
                TimeSpan.FromMinutes(2));

            return Regex.IsMatch(m.Result.Content, YesRegex);
        }

        #region Colors

        public static DiscordColor BuildColor(this string iv)
        {
            if (double.TryParse(iv.Substring(0, iv.Length - 1), out var result))
            {
                if (Math.Abs(result - 100) < double.Epsilon)
                    return DiscordColor.Green;
                else if (result >= 90 && result < 100)
                    return DiscordColor.Orange;
                else if (result < 90)
                    return DiscordColor.Yellow;
            }

            return DiscordColor.White;
        }

        public static DiscordColor BuildRaidColor(this string level)
        {
            if (!int.TryParse(level, out var lvl))
                return DiscordColor.Black;

            return BuildRaidColor(lvl);
        }

        public static DiscordColor BuildRaidColor(this int level)
        {
            switch (level)
            {
                case 1:
                case 2:
                    return DiscordColor.HotPink;
                case 3:
                case 4:
                    return DiscordColor.Yellow;
                case 5:
                    return DiscordColor.Purple;
            }

            return DiscordColor.White;
        }

        #endregion
    }
}