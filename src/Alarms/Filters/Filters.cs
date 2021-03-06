﻿namespace WhMgr.Alarms.Filters
{
    using System;

    using WhMgr.Data;
    using WhMgr.Diagnostics;
    using WhMgr.Net.Models;

    public class Filters
    {
        private static readonly IEventLogger _logger = EventLogger.GetLogger("FILTERS");

        public Filters()
        {
            _logger.Trace($"Filters::Filters");
        }

        public bool MatchesIV(string iv, uint minimumIV, uint maximumIV)
        {
            var matchesIV = false;
            var missing = iv == "?" || string.IsNullOrEmpty(iv);
            if (!missing)
            {
                if (!double.TryParse(iv.Replace("%", ""), out double resultIV))
                {
                    _logger.Error($"Failed to parse pokemon IV value '{iv}', skipping filter check.");
                    return false;
                }

                matchesIV |= Math.Round(resultIV) >= minimumIV && Math.Round(resultIV) <= maximumIV;
            }

            matchesIV |= (missing && minimumIV == 0);

            return matchesIV;
        }

        public bool MatchesCP(string cp, uint minimumCP, uint maximumCP)
        {
            var matchesCP = false;
            var missing = cp == "?" || string.IsNullOrEmpty(cp);
            if (!missing)
            {
                if (!int.TryParse(cp, out int resultCP))
                {
                    _logger.Error($"Failed to parse pokemon CP value '{cp}', skipping filter check.");
                    return false;
                }

                matchesCP |= resultCP >= minimumCP && resultCP <= maximumCP;
            }

            matchesCP |= (missing && minimumCP == 0);

            return matchesCP;
        }

        public bool MatchesLvl(string lvl, uint minimumLvl, uint maximumLvl)
        {
            var matchesLvl = false;
            var missing = lvl == "?" || string.IsNullOrEmpty(lvl);
            if (!missing)
            {
                if (!int.TryParse(lvl, out int resultLvl))
                {
                    _logger.Error($"Failed to parse pokemon level value '{lvl}', skipping filter check.");
                    return false;
                }

                matchesLvl |= resultLvl >= minimumLvl && resultLvl <= maximumLvl;
            }

            matchesLvl |= (missing && minimumLvl == 0);

            return matchesLvl;
        }

        public bool MatchesPvPRank(int rank, uint minimumRank, uint maximumRank)
        {
            var matchesRank = false;
            var missing = rank == 0;
            if (!missing)
            {
                matchesRank |= rank >= minimumRank && rank <= maximumRank;
            }

            matchesRank |= (missing && minimumRank == 0);

            return matchesRank;
        }

        public bool MatchesIV(string iv, int minimumIV)
        {
            var matchesIV = false;
            var missing = iv == "?" || string.IsNullOrEmpty(iv);
            if (!missing)
            {
                if (!double.TryParse(iv.Replace("%", ""), out double resultIV))
                {
                    _logger.Error($"Failed to parse pokemon IV value '{iv}', skipping filter check.");
                    return false;
                }

                matchesIV |= Math.Round(resultIV) >= minimumIV;
            }

            matchesIV |= (missing && minimumIV == 0);

            return matchesIV;
        }

        public bool MatchesCP(string cp, int minimumCP)
        {
            var matchesCP = false;
            var missing = cp == "?" || string.IsNullOrEmpty(cp);
            if (!missing)
            {
                if (!int.TryParse(cp, out int resultCP))
                {
                    _logger.Error($"Failed to parse pokemon CP value '{cp}', skipping filter check.");
                    return false;
                }

                matchesCP |= resultCP >= minimumCP;
            }

            matchesCP |= (missing && minimumCP == 0);

            return matchesCP;
        }

        public bool MatchesLvl(string lvl, int minimumLvl)
        {
            var matchesLvl = false;
            var missing = lvl == "?" || string.IsNullOrEmpty(lvl);
            if (!missing)
            {
                if (!int.TryParse(lvl, out int resultLvl))
                {
                    _logger.Error($"Failed to parse pokemon level value '{lvl}', skipping filter check.");
                    return false;
                }

                matchesLvl |= resultLvl >= minimumLvl;
            }

            matchesLvl |= (missing && minimumLvl == 0);

            return matchesLvl;
        }

        public bool MatchesGender(PokemonGender gender, PokemonGender desiredGender)
        {
            return gender == desiredGender ||
                   gender == PokemonGender.Unset ||
                   gender == PokemonGender.Genderless;
        }

        public bool MatchesGender(PokemonGender gender, string desiredGender)
        {
            desiredGender = desiredGender.ToLower();

            if (desiredGender == "*" || gender == PokemonGender.Genderless || gender == PokemonGender.Unset)
                return true;

            if (desiredGender == "m" && gender == PokemonGender.Male)
                return true;

            if (desiredGender == "f" && gender == PokemonGender.Female)
                return true;

            return false;
        }

        public bool MatchesSize(PokemonSize pkmnSize, PokemonSize? filterSize)
        {
            return (filterSize.HasValue && pkmnSize == filterSize.Value) || !filterSize.HasValue;
        }

        public bool MatchesAttack(string atk, int minimumAtk)
        {
            var matchesAtk = false;
            var missing = atk == "?" || string.IsNullOrEmpty(atk);
            if (!missing)
            {
                if (!int.TryParse(atk, out int resultAtk))
                {
                    _logger.Error($"Failed to parse pokemon attack IV value '{atk}', skipping filter check.");
                    return false;
                }

                matchesAtk = resultAtk == minimumAtk;
            }

            return matchesAtk;
        }

        public bool MatchesDefense(string def, int minimumDef)
        {
            var matchesDef = false;
            var missing = def == "?" || string.IsNullOrEmpty(def);
            if (!missing)
            {
                if (!int.TryParse(def, out int resultAtk))
                {
                    _logger.Error($"Failed to parse pokemon defense IV value '{def}', skipping filter check.");
                    return false;
                }

                matchesDef = resultAtk == minimumDef;
            }

            return matchesDef;
        }

        public bool MatchesStamina(string sta, int minimumSta)
        {
            var matchesSta = false;
            var missing = sta == "?" || string.IsNullOrEmpty(sta);
            if (!missing)
            {
                if (!int.TryParse(sta, out int resultAtk))
                {
                    _logger.Error($"Failed to parse pokemon stamina IV value '{sta}', skipping filter check.");
                    return false;
                }

                matchesSta = resultAtk == minimumSta;
            }

            return matchesSta;
        }

        //public bool MatchesGreatLeague(PokemonData pkmn)
        //{
        //    if (!Database.Instance.PvPGreat.ContainsKey(pkmn.Id))
        //        return false;

        //    var greatPokemon = Database.Instance.PvPGreat[pkmn.Id];
        //    return greatPokemon.Exists(x =>
        //    {
        //        if (int.TryParse(pkmn.Attack, out var atk) && atk == x.IVs.Attack &&
        //            int.TryParse(pkmn.Defense, out var def) && def == x.IVs.Defense &&
        //            int.TryParse(pkmn.Stamina, out var sta) && sta == x.IVs.Stamina)
        //        {
        //            return true;
        //        }
        //        return false;
        //    });
        //}

        //public bool MatchesUltraLeague(PokemonData pkmn)
        //{
        //    if (!Database.Instance.PvPUltra.ContainsKey(pkmn.Id))
        //        return false;

        //    var ultraPokemon = Database.Instance.PvPUltra[pkmn.Id];
        //    return ultraPokemon.Exists(x =>
        //    {
        //        if (int.TryParse(pkmn.Attack, out var atk) && atk == x.IVs.Attack &&
        //            int.TryParse(pkmn.Defense, out var def) && def == x.IVs.Defense &&
        //            int.TryParse(pkmn.Stamina, out var sta) && sta == x.IVs.Stamina)
        //        {
        //            return true;
        //        }
        //        return false;
        //    });
        //}
    }
}