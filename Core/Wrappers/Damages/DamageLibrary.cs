﻿// <copyright file="DamageLibrary.cs" company="LeagueSharp">
//    Copyright (c) 2015 LeagueSharp.
// 
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
// 
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
// 
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see http://www.gnu.org/licenses/
// </copyright>

namespace LeagueSharp.SDK.Core.Wrappers.Damages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Permissions;
    using System.Text;
    using Enumerations;
    using Events;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Properties;
    using Utils;

    /// <summary>
    ///     Damage wrapper class, contains functions to calculate estimated damage to a unit and also provides damage details.
    /// </summary>
    public static partial class Damage
    {
        #region Static Fields

        /// <summary>
        ///     The damage version files.
        /// </summary>
        private static readonly IDictionary<string, byte[]> DamageFiles = new Dictionary<string, byte[]>
                                                                              { { "5.20", Resources._5_20 } };

        #endregion

        #region Properties

        /// <summary>
        ///     Gets the Damage Collection.
        /// </summary>
        private static IDictionary<string, ChampionDamage> DamageCollection { get; } =
            new Dictionary<string, ChampionDamage>();

        #endregion

        #region Methods

        /// <summary>
        ///     Initializes a new instance of the <see cref="Damage" /> class.
        /// </summary>
        /// <param name="gameVersion">
        ///     The client version.
        /// </param>
        internal static void Initialize(Version gameVersion)
        {
            Load.OnLoad += (sender, args) =>
                {
                    OnLoad(gameVersion);
                    CreatePassives();
                };
        }

        /// <summary>
        ///     Creates the damage collection.
        /// </summary>
        /// <param name="damages">
        ///     The converted <see cref="byte" />s of damages into a dictionary collection.
        /// </param>
        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        private static void CreateDamages(IDictionary<string, JToken> damages)
        {
            foreach (var champion in GameObjects.Heroes.Select(h => h.ChampionName))
            {
                JToken value;
                if (damages.TryGetValue(champion, out value))
                {
                    DamageCollection.Add(champion, JsonConvert.DeserializeObject<ChampionDamage>(value.ToString()));
                }
            }
        }

        private static void OnLoad(Version version)
        {
            var versionString = $"{version.Major}.{version.Minor}";

            var fileBytes = DamageFiles.ContainsKey(versionString)
                                ? DamageFiles[versionString]
                                : DamageFiles.OrderByDescending(o => o.Key).FirstOrDefault().Value;
            if (fileBytes != null)
            {
                CreateDamages(JObject.Parse(Encoding.Default.GetString(fileBytes)));
                return;
            }

            Logging.Write()(LogLevel.Fatal, "No suitable damage library is available.");
        }

        /// <summary>
        ///     Resolves the spell bonus damage.
        /// </summary>
        /// <param name="source">
        ///     The source
        /// </param>
        /// <param name="target">
        ///     The target
        /// </param>
        /// <param name="spellBonus">
        ///     The spell bonus collection
        /// </param>
        /// <param name="index">
        ///     The index (spell level - 1)
        /// </param>
        /// <returns>
        ///     The <see cref="double" />.
        /// </returns>
        private static double ResolveBonusSpellDamage(
            this Obj_AI_Base source,
            Obj_AI_Base target,
            ChampionDamageSpellBonus spellBonus,
            int index)
        {
            var sourceScale = spellBonus.ScalingTarget == DamageScalingTarget.Source ? source : target;
            var percent = spellBonus.DamagePercentages?[Math.Min(index, spellBonus.DamagePercentages.Count - 1)];
            var origin = 0f;

            switch (spellBonus.ScalingType)
            {
                case DamageScalingType.BonusAttackPoints:
                    origin = sourceScale.FlatPhysicalDamageMod;
                    break;
                case DamageScalingType.AbilityPoints:
                    origin = sourceScale.TotalMagicalDamage;
                    break;
                case DamageScalingType.AttackPoints:
                    origin = sourceScale.TotalAttackDamage;
                    break;
                case DamageScalingType.MaxHealth:
                    origin = sourceScale.MaxHealth;
                    break;
                case DamageScalingType.CurrentHealth:
                    origin = sourceScale.Health;
                    break;
                case DamageScalingType.MissingHealth:
                    origin = sourceScale.MaxHealth - sourceScale.Health;
                    break;
                case DamageScalingType.Armor:
                    origin = sourceScale.Armor;
                    break;
                case DamageScalingType.MaxMana:
                    origin = sourceScale.MaxMana;
                    break;
            }

            if (!string.IsNullOrEmpty(spellBonus.ScalingBuff))
            {
                var buffCount =
                    (spellBonus.ScalingBuffTarget == DamageScalingTarget.Source ? source : target).GetBuffCount(
                        spellBonus.ScalingBuff);

                return buffCount != 0 ? (origin * (percent ?? 0)) * (buffCount + spellBonus.ScalingBuffOffset) : 0d;
            }

            return origin * (percent ?? 0);
        }

        #endregion
    }
}