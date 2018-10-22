﻿namespace WhMgr
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using WhMgr.Configuration;
    using WhMgr.Data;
    using WhMgr.Data.Models;
    using WhMgr.Diagnostics;
    using WhMgr.Extensions;
    using WhMgr.Geofence;
    using WhMgr.Net.Models;
    using WhMgr.Net.Webhooks;

    using DSharpPlus;
    using DSharpPlus.Entities;
    using DSharpPlus.EventArgs;

    //TODO: Backup subscriptions database.
    //TODO: Notification limiter.

    public class Bot
    {
        #region Variables

        private readonly DiscordClient _client;
        private readonly WebhookManager _whm;
        private readonly WhConfig _whConfig;
        private readonly IEventLogger _logger;

        #endregion

        #region Constructor

        public Bot(WhConfig whConfig)
        {
            var name = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName;
            _logger = EventLogger.GetLogger(name);
            _logger.Trace($"Bot::Bot [WhConfig={whConfig.GuidId}]");

            _whConfig = whConfig;

            _whm = new WebhookManager(_whConfig.WebHookPort, _whConfig.MapProvider, _whConfig.MapProviderFork);
            _whm.PokemonAlarmTriggered += OnPokemonAlarmTriggered;
            _whm.RaidAlarmTriggered += OnRaidAlarmTriggered;
            _whm.PokemonSubscriptionTriggered += OnPokemonSubscriptionTriggered;
            _whm.RaidSubscriptionTriggered += OnRaidSubscriptionTriggered;

            _logger.Info("WebHookManager is running...");
            
            _client = new DiscordClient(new DiscordConfiguration
            {
                AutomaticGuildSync = true,
                AutoReconnect = true,
                EnableCompression = true,
                Token = _whConfig.Token,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true
            });
        }

        #endregion

        #region Public Methods

        public void Start()
        {
            _logger.Trace("Bot::Start");
            _logger.Info("Connecting to Discord...");

            _client.Ready += Client_Ready;
            _client.MessageCreated += Client_MessageCreated;
            _client.ConnectAsync();
        }

        #endregion

        #region Discord Events

        private async Task Client_Ready(ReadyEventArgs e)
        {
            _logger.Info($"Connected.");

            await Task.CompletedTask;
        }

        private async Task Client_MessageCreated(MessageCreateEventArgs e)
        {
            if (e.Author.IsBot)
                return;

            if (e.Author.Id != _whConfig.OwnerId)
                return;

            if (!e.Message.Content.StartsWith("!", StringComparison.Ordinal))
                return;

            await HandleCommands(e.Message);
        }

        #endregion

        #region WebHookManager Events

        private async void OnPokemonAlarmTriggered(object sender, PokemonAlarmTriggeredEventArgs e)
        {
            if (!_whConfig.Enabled)
                return;

            _logger.Info($"Pokemon Found [Alarm: {e.Alarm.Name}, Pokemon: {e.Pokemon.Id}, Despawn: {e.Pokemon.DespawnTime}");

            var wh = _whm.WebHooks[e.Alarm.Name];
            if (wh == null)
            {
                _logger.Error($"Failed to parse webhook data from {e.Alarm.Name} {e.Alarm.Webhook}.");
                return;
            }

            var form = e.Pokemon.Id.GetPokemonForm(e.Pokemon.FormId);
            var pkmn = Database.Instance.Pokemon[e.Pokemon.Id];
            var pkmnImage = string.Format(Strings.PokemonImage, e.Pokemon.Id, Convert.ToInt32(string.IsNullOrEmpty(e.Pokemon.FormId) ? "0" : e.Pokemon.FormId));
            var eb = BuildPokemonMessage(e.Pokemon, e.Alarm.Name);

            var whData = await _client.GetWebhookWithTokenAsync(wh.Id, wh.Token);
            var name = $"{pkmn.Name}{e.Pokemon.Gender.GetPokemonGenderIcon()}{form}";
            await whData.ExecuteAsync(string.Empty, name, pkmnImage, false, new List<DiscordEmbed> { eb });
        }

        private async void OnRaidAlarmTriggered(object sender, RaidAlarmTriggeredEventArgs e)
        {
            if (!_whConfig.Enabled)
                return;

            _logger.Info($"Raid Found [Alarm: {e.Alarm.Name}, Raid: {e.Raid.PokemonId}, Level: {e.Raid.Level}, StartTime: {e.Raid.StartTime}]");

            var wh = _whm.WebHooks[e.Alarm.Name];
            if (wh == null)
            {
                _logger.Error($"Failed to parse webhook data from {e.Alarm.Name} {e.Alarm.Webhook}.");
                return;
            }

            var pkmn = Database.Instance.Pokemon[e.Raid.PokemonId];
            var pkmnImage = e.Raid.IsEgg ? string.Format(Strings.EggImage, e.Raid.Level) : string.Format(Strings.PokemonImage, e.Raid.PokemonId, 0);
            var eb = BuildRaidMessage(e.Raid, e.Alarm.Name);

            var whData = await _client.GetWebhookWithTokenAsync(wh.Id, wh.Token);
            var name = e.Raid.IsEgg ? $"Level {e.Raid.Level} {pkmn.Name}" : pkmn.Name;
            await whData.ExecuteAsync(string.Empty, name, pkmnImage, false, new List<DiscordEmbed> { eb });
        }

        #endregion

        #region Subscription Events

        private void OnPokemonSubscriptionTriggered(object sender, PokemonData e)
        {
            ProcessPokemonSubscription(e).GetAwaiter().GetResult();
        }

        private void OnRaidSubscriptionTriggered(object sender, RaidData e)
        {
            ProcessRaidSubscription(e).GetAwaiter().GetResult();
        }

        #endregion

        #region Subscription Processor

        private async Task ProcessPokemonSubscription(PokemonData pkmn)
        {
            var db = Database.Instance;
            if (!db.Pokemon.ContainsKey(pkmn.Id))
                return;

            var loc = _whm.GeofenceService.GetGeofence(_whm.Geofences, new Location(pkmn.Latitude, pkmn.Longitude));
            if (loc == null)
            {
                _logger.Error($"Failed to lookup city from coordinates {pkmn.Latitude},{pkmn.Longitude} {db.Pokemon[pkmn.Id].Name} {pkmn.IV}, skipping...");
                return;
            }

            SubscriptionObject user;
            bool isSupporter;
            PokemonSubscription subscribedPokemon;
            var pokemon = db.Pokemon[pkmn.Id];
            bool matchesIV;
            bool matchesLvl;
            bool matchesGender;
            var embed = BuildPokemonMessage(pkmn, loc.Name);

            var keys = db.Subscriptions.Keys.ToList();
            for (var i = 0; i < keys.Count; i++)
            {
                try
                {
                    var userId = keys[i];
                    user = db.Subscriptions[userId];

                    if (user == null)
                        continue;

                    if (!user.Enabled)
                        continue;

                    var member = await _client.GetMemberById(_whConfig.GuidId, userId);
                    if (member == null)
                    {
                        _logger.Error($"Failed to find member with id {userId}.");
                        continue;
                    }

                    isSupporter = member.Roles.Select(x => x.Id).Contains(_whConfig.SupporterRoleId);
                    if (!isSupporter)
                    {
                        _logger.Debug($"User {member.Username} is not a supporter, skipping pokemon {pkmn.Id}...");
                        continue;
                    }

                    if (!user.Pokemon.ContainsKey(pkmn.Id))
                        continue;

                    subscribedPokemon = user.Pokemon[pkmn.Id];
                    if (subscribedPokemon == null)
                        continue;

                    if (!member.Roles.Select(x => x.Name).Contains(loc.Name))
                    {
                        _logger.Debug($"Skipping user {member.DisplayName} ({member.Id}) for {pokemon.Name} {pkmn.IV}, no city role '{loc.Name}'.");
                        continue;
                    }

                    matchesIV = _whm.Filters.MatchesIV(pkmn.IV, /*_whConfig.OnlySendEventPokemon ? _whConfig.EventPokemonMinimumIV :*/ subscribedPokemon.MinimumIV);
                    //var matchesCP = _whm.Filters.MatchesCpFilter(pkmn.CP, subscribedPokemon.MinimumCP);
                    matchesLvl = _whm.Filters.MatchesLvl(pkmn.Level, subscribedPokemon.MinimumLevel);
                    matchesGender = _whm.Filters.MatchesGender(pkmn.Gender, subscribedPokemon.Gender);

                    if (!(matchesIV && matchesLvl && matchesGender))
                        continue;

                    if (user.Limiter.IsLimited())
                    {
                        //if (!user.NotifiedOfLimited)
                        //{
                        //    await _client.SendDirectMessage(member, string.Format(NotificationsLimitedMessage, NotificationLimiter.MaxNotificationsPerMinute), null);
                        //    user.NotifiedOfLimited = true;
                        //}

                        continue;
                    }

                    //user.NotifiedOfLimited = false;

                    _logger.Info($"Notifying user {member.Username} that a {pokemon.Name} {pkmn.CP}CP {pkmn.IV} IV L{pkmn.Level} has spawned...");

                    if (embed == null)
                        continue;

                    //if (await CheckIfExceededNotificationLimit(user)) return;

                    user.NotificationsToday++;

                    await SendNotification(userId, pokemon.Name, embed);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }
        }

        private async Task ProcessRaidSubscription(RaidData raid)
        {
            var db = Database.Instance;
            if (!db.Pokemon.ContainsKey(raid.PokemonId))
                return;

            var loc = _whm.GeofenceService.GetGeofence(_whm.Geofences, new Location(raid.Latitude, raid.Longitude));
            if (loc == null)
            {
                _logger.Error($"Failed to lookup city for coordinates {raid.Latitude},{raid.Longitude}, skipping...");
                return;
            }

            bool isSupporter;
            SubscriptionObject user;
            RaidSubscription subscribedRaid;
            var embed = BuildRaidMessage(raid, loc.Name);

            if (DateTime.Now > raid.EndTime)
            {
                _logger.Info($"Raid {raid.PokemonId} already expired, skipping...");
                return;
            }

            var keys = db.Subscriptions.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                try
                {
                    var userId = keys[i];
                    user = db.Subscriptions[userId];

                    if (user == null)
                        continue;

                    if (!user.Enabled)
                        continue;

                    //if (await RemoveUserIfNotExists(userId))
                    //    return;

                    var member = await _client.GetMemberById(_whConfig.GuidId, userId);
                    if (member == null)
                    {
                        _logger.Error($"Failed to find member with id {userId}.");
                        continue;
                    }

                    isSupporter = member.Roles.Select(x => x.Id).Contains(_whConfig.SupporterRoleId);
                    if (!isSupporter)
                    {
                        _logger.Info($"User {userId} is not a supporter, skipping raid boss {raid.PokemonId}...");
                        continue;
                    }

                    if (!user.Raids.ContainsKey(raid.PokemonId))
                        continue;

                    subscribedRaid = user.Raids[raid.PokemonId];
                    if (subscribedRaid == null)
                        continue;

                    var pokemon = db.Pokemon[raid.PokemonId];
                    if (!member.Roles.Select(x => x.Name).Contains(loc.Name))
                    {
                        _logger.Debug($"[{loc.Name}] Skipping notification for user {member.DisplayName} ({member.Id}) for Pokemon {pokemon.Name} because they do not have the city role '{loc.Name}'.");
                        continue;
                    }

                    var exists = user.Raids.FirstOrDefault(x => x.Value.PokemonId == raid.PokemonId &&
                    (
                        string.IsNullOrEmpty(x.Value.City) || (!string.IsNullOrEmpty(x.Value.City) && string.Compare(loc.Name, x.Value.City, true) == 0)
                    )).Value != null;
                    if (!exists)
                    {
                        _logger.Debug($"Skipping notification for user {member.DisplayName} ({member.Id}) for Pokemon {pokemon.Name} because the raid is in city '{loc.Name}'.");
                        continue;
                    }

                    if (user.Limiter.IsLimited())
                    {
                        //if (!user.NotifiedOfLimited)
                        //{
                        //    await _client.SendDirectMessage(member, string.Format(NotificationsLimitedMessage, NotificationLimiter.MaxNotificationsPerMinute), null);
                        //    user.NotifiedOfLimited = true;
                        //}

                        continue;
                    }

                    //user.NotifiedOfLimited = false;

                    _logger.Info($"Notifying user {member.Username} that a {raid.PokemonId} raid is available...");

                    //if (await CheckIfExceededNotificationLimit(user)) return;

                    user.NotificationsToday++;

                    await SendNotification(userId, pokemon.Name, embed);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }
        }

        #endregion

        #region Embed Builder

        private DiscordEmbed BuildPokemonMessage(PokemonData pokemon, string city)
        {
            _logger.Trace($"Bot::BuildPokemonMessage [Pokemon={pokemon.Id}, City={city}]");

            var db = Database.Instance;
            var pkmn = db.Pokemon[pokemon.Id];
            if (pkmn == null)
            {
                _logger.Error($"Failed to lookup Pokemon '{pokemon.Id}' in database.");
                return null;
            }

            var form = pokemon.Id.GetPokemonForm(pokemon.FormId);
            var eb = new DiscordEmbedBuilder
            {
                Title = string.IsNullOrEmpty(city) ? "DIRECTIONS" : city,
                Url = string.Format(Strings.GoogleMaps, pokemon.Latitude, pokemon.Longitude),
                ImageUrl = string.Format(Strings.GoogleMapsStaticImage, pokemon.Latitude, pokemon.Longitude),
                ThumbnailUrl = string.Format(Strings.PokemonImage, pokemon.Id, Convert.ToInt32(string.IsNullOrEmpty(pokemon.FormId) ? "0" : pokemon.FormId)),
                Color = BuildColor(pokemon.IV)
            };

            if (pokemon.IV == "?" || pokemon.Level == "?" || string.IsNullOrEmpty(pokemon.Level))
            {
                eb.Description = $"{pkmn.Name} {form}{pokemon.Gender.GetPokemonGenderIcon()} Despawn: {pokemon.DespawnTime.ToLongTimeString()} ({pokemon.SecondsLeft.ToReadableStringNoSeconds()} left)\r\n";
            }
            else
            {
                eb.Description = $"{pkmn.Name} {form}{pokemon.Gender.GetPokemonGenderIcon()} {pokemon.IV} L{pokemon.Level} Despawn: {pokemon.DespawnTime.ToLongTimeString()} ({pokemon.SecondsLeft.ToReadableStringNoSeconds()} left)\r\n\r\n";
                eb.Description += $"**Details:** CP: {pokemon.CP} IV: {pokemon.IV} LV: {pokemon.Level}\r\n";
                eb.Description += $"**IV Stats:** Atk: {pokemon.Attack}/Def: {pokemon.Defense}/Sta: {pokemon.Stamina}\r\n";
            }

            if (!string.IsNullOrEmpty(form))
            {
                eb.Description += $"**Form:** {form}\r\n";
            }

            if (int.TryParse(pokemon.Level, out int lvl) && lvl >= 30)
            {
                eb.Description += $":white_sun_rain_cloud: Boosted\r\n";
            }

            //var maxCp = db.MaxCpAtLevel(pokemon.Id, 40);
            //var maxWildCp = db.MaxCpAtLevel(pokemon.Id, 35);
            //eb.Description += $"**Max Wild CP:** {maxWildCp}, **Max CP:** {maxCp} \r\n";

            //if (pkmn.Types.Count > 0)
            //{
            //    var types = new List<string>();
            //    pkmn.Types.ForEach(x =>
            //    {
            //        if (Strings.TypeEmojis.ContainsKey(x.Type.ToLower()))
            //        {
            //            types.Add($"{Strings.TypeEmojis[x.Type.ToLower()]} {x.Type}");
            //        }
            //    });
            //    eb.Description += $"**Types:** {string.Join("/", types)}\r\n";
            //}

            //if (float.TryParse(pokemon.Height, out float height) && float.TryParse(pokemon.Weight, out float weight))
            //{
            //    var size = db.GetSize(pokemon.Id, height, weight);
            //    eb.Description += $"**Size:** {size}\r\n";
            //}

            var fastMoveId = Convert.ToInt32(pokemon.FastMove ?? "0");
            if (Database.Instance.Movesets.ContainsKey(fastMoveId))
            {
                var fastMove = Database.Instance.Movesets[fastMoveId];
                //var fastMoveIcon = Strings.TypeEmojis.ContainsKey(fastMove.Type.ToLower()) ? Strings.TypeEmojis[fastMove.Type.ToLower()] : fastMove.Type;
                eb.Description += $"**Fast Move:** {fastMove.Name}\r\n";
            }

            var chargeMoveId = Convert.ToInt32(pokemon.ChargeMove ?? "0");
            if (db.Movesets.ContainsKey(chargeMoveId))
            {
                var chargeMove = Database.Instance.Movesets[chargeMoveId];
                //var chargeMoveIcon = Strings.TypeEmojis.ContainsKey(chargeMove.Type.ToLower()) ? Strings.TypeEmojis[chargeMove.Type.ToLower()] : chargeMove.Type;
                eb.Description += $"**Charge Move:** {chargeMove.Name} ({chargeMove.Type})\r\n";
            }

            eb.Description += $"**Location:** {Math.Round(pokemon.Latitude, 5)},{Math.Round(pokemon.Longitude, 5)}";
            eb.ImageUrl = string.Format(Strings.GoogleMapsStaticImage, pokemon.Latitude, pokemon.Longitude) + $"&key={_whConfig.GmapsKey}";
            eb.Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                Text = $"versx | {DateTime.Now}"
            };
            var embed = eb.Build();

            return embed;
        }

        private DiscordEmbed BuildRaidMessage(RaidData raid, string city)
        {
            _logger.Trace($"Bot::BuildRaidMessage [Raid={raid.PokemonId}, City={city}]");

            var db = Database.Instance;
            var pkmn = db.Pokemon[raid.PokemonId];
            if (pkmn == null)
            {
                _logger.Error($"Failed to lookup Raid Pokemon '{raid.PokemonId}' in database.");
                return null;
            }

            var pkmnImage = raid.IsEgg ? string.Format(Strings.EggImage, raid.Level) : string.Format(Strings.PokemonImage, raid.PokemonId, 0);
            var eb = new DiscordEmbedBuilder
            {
                Title = string.IsNullOrEmpty(city) ? "DIRECTIONS" : city,
                Url = string.Format(Strings.GoogleMaps, raid.Latitude, raid.Longitude),
                ImageUrl = string.Format(Strings.GoogleMapsStaticImage, raid.Latitude, raid.Longitude),
                ThumbnailUrl = pkmnImage,
                Color = BuildRaidColor(Convert.ToInt32(raid.Level))
            };

            if (raid.IsEgg)
            {
                eb.Description = $"Level {raid.Level} {pkmn.Name} Hatches: {raid.StartTime.ToLongTimeString()}\r\n";
                eb.Description += $"{raid.GymName}\r\n\r\n";
                eb.Description += $"**Ends:** {raid.EndTime.ToLongTimeString()} ({DateTime.Now.GetTimeRemaining(raid.EndTime).ToReadableStringNoSeconds()} left)\r\n";
            }
            else
            {
                eb.Description = $"{pkmn.Name} Raid Ends: {raid.EndTime.ToLongTimeString()}\r\n";
                eb.Description += $"{raid.GymName}\r\n\r\n";
                eb.Description += $"**Started:** {raid.StartTime.ToLongTimeString()}\r\n";
                eb.Description += $"**Ends:** {raid.EndTime.ToLongTimeString()} ({raid.EndTime.GetTimeRemaining().ToReadableStringNoSeconds()} left)\r\n";

                var perfectRange = raid.PokemonId.GetPokemonCpRange(20);
                var boostedRange = raid.PokemonId.GetPokemonCpRange(25);
                eb.Description += $"**Perfect CP:** {perfectRange.Best} / :white_sun_rain_cloud: {boostedRange.Best}\r\n";

                if (pkmn.Types != null)
                {
                    //var types = new List<string>();
                    //pkmn.Types.ForEach(x =>
                    //{
                    //    if (Strings.TypeEmojis.ContainsKey(x.Type.ToLower()))
                    //    {
                    //        types.Add(Strings.TypeEmojis[x.Type.ToLower()] + " " + x.Type);
                    //    }
                    //});
                    //eb.Description += $"**Types:** {string.Join("/", types)}\r\n";
                }

                var fastMoveId = Convert.ToInt32(raid.ChargeMove ?? "0");
                if (db.Movesets.ContainsKey(fastMoveId))
                {
                    var fastMove = db.Movesets[fastMoveId];
                    //var fastMoveIcon = Strings.TypeEmojis.ContainsKey(fastMove.Type.ToLower()) ? Strings.TypeEmojis[fastMove.Type.ToLower()] : fastMove.Type;
                    //eb.Description += $"**Fast Move:** {Strings.TypeEmojis[fastMove.Type.ToLower()]} {fastMove.Name}\r\n";
                    eb.Description += $"**Fast Move:** {fastMove.Name} ({fastMove.Type})\r\n";
                }

                var chargeMoveId = Convert.ToInt32(raid.ChargeMove ?? "0");
                if (db.Movesets.ContainsKey(chargeMoveId))
                {
                    var chargeMove = db.Movesets[chargeMoveId];
                    //var chargeMoveIcon = Strings.TypeEmojis.ContainsKey(chargeMove.Type.ToLower()) ? Strings.TypeEmojis[chargeMove.Type.ToLower()] : chargeMove.Type;
                    //eb.Description += $"**Charge Move:** {Strings.TypeEmojis[chargeMove.Type.ToLower()]} {chargeMove.Name}\r\n";
                    eb.Description += $"**Charge Move:** {chargeMove.Name} ({chargeMove.Type})\r\n";
                }

                if (pkmn.Types != null)
                {
                    var strengths = new List<string>();
                    var weaknesses = new List<string>();
                    for (var i = 0; i < pkmn.Types.Count; i++)
                    {
                        strengths.AddRange(pkmn.Types[i].Type.GetStrengths().Distinct());
                        weaknesses.AddRange(pkmn.Types[i].Type.GetWeaknesses().Distinct());
                    }

                    if (strengths.Count > 0)
                    {
                        eb.Description += $"**Strong Against:** {string.Join(", ", strengths)}\r\n";
                    }
                    if (weaknesses.Count > 0)
                    {
                        eb.Description += $"**Weaknesses:** {string.Join(", ", weaknesses)}\r\n";
                    }
                }
            }

            eb.Description += $"**Location:** {Math.Round(raid.Latitude, 5)},{Math.Round(raid.Longitude, 5)}";
            eb.ImageUrl = string.Format(Strings.GoogleMapsStaticImage, raid.Latitude, raid.Longitude) + $"&key={_whConfig.GmapsKey}";
            eb.Footer = new DiscordEmbedBuilder.EmbedFooter
            {
                Text = $"versx | {DateTime.Now}"
            };
            var embed = eb.Build();

            return embed;
        }

        #endregion

        #region Private Methods

        private async Task HandleCommands(DiscordMessage e)
        {
            switch (e.Content.ToLower())
            {
                case "!refresh":
                    await RemoveUserRoles(e);
                    break;
                case "!createdb":
                    Database.Instance.Subscriptions.Add(0, new SubscriptionObject
                    {
                        Enabled = true,
                        Pokemon = new Dictionary<int, PokemonSubscription>
                            {
                                { 0, new PokemonSubscription { } }
                            },
                        Raids = new Dictionary<int, RaidSubscription>
                            {
                                { 0, new RaidSubscription { } }
                            }
                    });
                    Database.Instance.Save();
                    break;
            }
        }

        private async Task SendNotification(ulong userId, string pokemon, DiscordEmbed embed)
        {
            _logger.Trace($"Bot::SendNotification [UserId={userId}, Pokemon={pokemon}, Embed={embed.Title}]");
            _logger.Info($"Notifying using {userId} of {pokemon} spawn.");

            var user = await _client.GetUserAsync(userId);
            if (user == null)
            {
                _logger.Error($"Failed to find user from id {userId}.");
                return;
            }

            await _client.SendDirectMessage(user, embed);
        }

        private async Task<bool> RemoveUserIfNotExists(ulong userId)
        {
            _logger.Trace($"Bot::RemoveUserIfNotExists [UserId={userId}]");

            var db = Database.Instance;
            var discordUser = await _client.GetUserAsync(userId);
            if (discordUser == null)
            {
                if (!db.Subscriptions.Remove(userId))
                {
                    _logger.Error($"Failed to remove non-existing user {userId} from subscriptions database.");
                }

                _logger.Info($"User {userId} removed from subscriptions database.");

                db.Save();
            }

            return discordUser == null;
        }

        private async Task RemoveUserRoles(DiscordMessage e)
        {
            var cityRoles = new string[]
            {
                "RanchoRaids",
                "UplandRaids",
                "OntarioRaids",
                "ChinoRaids",
                "ClaremontRaids",
                "PomonaRaids",
                "MontclairRaids",
                "EastLARaids",
                "WhittierRaids",
                "Rancho",
                "Upland",
                "Ontario",
                "Chino",
                "Claremont",
                "Pomona",
                "Montclair",
                "EastLA",
                "Whittier",
                "100iv",
                "90iv",
                "lunatone",
                "Nests",
                "Raids",
                "Families",
                "Quests",
                "RaidTrain"
            };

            var success = 0;
            var failed = 0;
            //var members = new List<DiscordMember> { await GetMemeberById(_whConfig.OwnerId) };
            var members = e.Channel.Guild.Members;
            for (var usrId = 0; usrId < members.Count; usrId++)
            {
                var member = members[usrId];
                var roleIds = member.Roles.Select(x => x.Id).ToList();
                var roleNames = member.Roles.Select(x => x.Name).ToList();

                await e.RespondAsync($"Starting role refresh for user {member.Username} ({member.Id}).");

                //// Skip supporters and members that already have a team role set.
                if (roleIds.Contains(_whConfig.SupporterRoleId) || roleNames.Contains("Valor") || roleNames.Contains("Mystic") || roleNames.Contains("Instinct"))
                    continue;

                _logger.Debug($"Checking user {member.Username} ({member.Id}) roles...");
                var list = new List<string>();
                foreach (var cityRole in cityRoles)
                {
                    if (!roleNames.Contains(cityRole))
                        continue;

                    try
                    {
                        var role = member.Roles.FirstOrDefault(x => string.Compare(x.Name, cityRole, true) == 0);
                        await member.RevokeRoleAsync(role, "Roles refreshed.");
                        _logger.Debug($"Removed role {role.Name} ({role.Id}) from user {member.Username} ({member.Id}).");
                        success++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                        failed++;
                    }
                }

                await e.RespondAsync($"Role fresh for user {member.Username} ({member.Id}) finished.");
            }
        }

        private static DiscordColor BuildColor(string iv)
        {
            if (int.TryParse(iv.Substring(0, iv.Length - 1), out int result))
            {
                if (result == 100)
                    return DiscordColor.Green;
                else if (result >= 90 && result < 100)
                    return DiscordColor.Orange;
                else if (result < 90)
                    return DiscordColor.Yellow;
            }

            return DiscordColor.White;
        }

        private static DiscordColor BuildRaidColor(int level)
        {
            switch (level)
            {
                case 1:
                    return DiscordColor.HotPink;
                case 2:
                    return DiscordColor.HotPink;
                case 3:
                    return DiscordColor.Yellow;
                case 4:
                    return DiscordColor.Yellow;
                case 5:
                    return DiscordColor.Purple;
            }

            return DiscordColor.White;
        }

        #endregion

        //private void ParseLogs()
        //{
        //    var logsFile = "logs.log";
        //    var logLines = System.IO.File.ReadAllLines(logsFile);
        //    for (var i = 0; i < logLines.Length; i++)
        //    {
        //        var logLine = logLines[i];
        //        var logData = logLine.Split(' ');
        //        var dateTime = $"{logData[0]} {logData[1]}";
        //        var logType = logData[2];
        //        var logMessage = string.Join(" ", logData.Skip(3));

        //        Console.WriteLine($"{dateTime} {logType} {logMessage}");
        //    }
        //}
    }

    public class NotificationProcessor
    {

    }

    public class NotificationLimiter
    {
        public const int MaxNotificationsPerMinute = 60;
        public const int ThresholdTimeout = 60;

        private readonly DateTime _start;
        private DateTime _last;

        public int Count { get; private set; }

        public TimeSpan TimeLeft { get; private set; }

        public NotificationLimiter()
        {
            _start = DateTime.Now;
            _last = DateTime.Now;

            Count = 0;
            TimeLeft = TimeSpan.MinValue;
        }

        public virtual bool IsLimited()
        {
            TimeLeft = DateTime.Now.Subtract(_last);

            var sixtySeconds = TimeSpan.FromSeconds(ThresholdTimeout);
            var oneMinutePassed = TimeLeft >= sixtySeconds;
            if (oneMinutePassed)
            {
                Reset();
                _last = DateTime.Now;
            }

            if (Count >= MaxNotificationsPerMinute)
            {
                //Limited
                return true;
            }

            Count++;

            return false;
        }

        public virtual void Reset()
        {
            Count = 0;
        }
    }
}