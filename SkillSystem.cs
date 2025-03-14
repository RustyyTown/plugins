﻿namespace Oxide.Plugins
{
    using System;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Collections;
    using System.Linq;
    using UnityEngine;
    using Oxide.Core;
    using Oxide.Core.Plugins;
    using Newtonsoft.Json;
    using Oxide.Game.Rust.Cui;
    using System.Globalization;
    using Random = UnityEngine.Random;

    [Info("SkillSystem", "TopPlugin.ru", "1.1.3")]
    public class SkillSystem : RustPlugin
    {

        #region Определение переменных
        public class classSkillParameters
        {
            [JsonProperty("Количество XP(%)")]
            public double countXP;
            [JsonProperty("Количество DNK")]
            public float countDNK;
        }

        public class PlayerSkillData
        {
            [JsonProperty("Название")]
            public string Name;
            [JsonProperty("Описание")]
            public string Description;
            [JsonProperty("Shortname на который будет действовать увеличенный рейтинг скилла")]
            public List<string> ShortnameList;
            [JsonProperty("Рейты игрока ( ПРИБАВЛЯЮТСЯ % ОТ ДОБЫТОГО ИМ РЕСУРСА)")]
            public int Rate;
            [JsonProperty("Сколько % прибавлять за уровень")]
            public int PercentIncrement;
            [JsonProperty("Сколько количество жизней будет увеличиваться на XRate")]
            public double HPIncrementInRate;
            [JsonProperty("Сколько максимум хп игрок получит от регенерации")]
            public int MaxRegen;
            [JsonProperty("Максимальная защита от способности")]
            public int MaxProtection;
            [JsonProperty("Максимальный урон от способности")]
            public int MaxAtack;
            [JsonProperty("Уровень способности")]
            public int Level;
            [JsonProperty("Сколько возвращать DNK при откате способности(0-100%)")]
            public int ReturnPercent;
        }

        public static string CUILayerName = "PERCENT_LAYER_UI";
        public static string DescLayerName = "DESCRIPTION_LAYER_UI";
        public static string MessageUILayerName = "MESSAGE_LAYER_UI";
        public static string UIName = "DNKSYSTEM_LAYER_UI";

        private Dictionary<uint, BasePlayer> BradleyAtackerList = new Dictionary<uint, BasePlayer>();
        private Dictionary<uint, BasePlayer> HeliAtackerList = new Dictionary<uint, BasePlayer>();
        public Dictionary<ulong, classSkillParameters> PlayersSkillParameters = new Dictionary<ulong, classSkillParameters>();
        public Dictionary<ulong, List<PlayerSkillData>> PlayersSkillDataMassiv = new Dictionary<ulong, List<PlayerSkillData>>();


        private static DefaultSettings MainGUISettings = new DefaultSettings();

        #endregion Определение переменных

        #region CONFIGS

        //Подготавливает данные по умолчанию
        List<PlayerSkillData> DefaultPlayersSkillData()
        {
            List<PlayerSkillData> PlayersSkillDataList = new List<PlayerSkillData>();
            foreach (var skill in MainGUISettings.configSkillSettings)
            {
                PlayersSkillDataList.Add(new PlayerSkillData()
                {
                    Name = skill.Name,
                    Description = skill.Description,
                    ShortnameList = new List<string> { },
                    Level = 0,
                    HPIncrementInRate = 0.0,
                    MaxRegen = 0,
                    MaxAtack = 0,
                    MaxProtection = 0,
                    ReturnPercent = skill.ReturnPercent,
                    Rate = skill.Rate,
                    PercentIncrement = skill.PercentIncrement
                });
            }
            return PlayersSkillDataList;
        }
        //ВЫСТАВЛЯЕМ НАСТРОЙКИ ПО УМОЛЧАНИЮ (КОНФИГ)
        private class DefaultSettings
        {

            public class classSkill
            {
                [JsonProperty("Название", Order = 0)] public string Name;
                [JsonProperty("Описание", Order = 1)] public string Description;
                [JsonProperty("Shortname на который будет действовать увеличенный рейтинг скилла", Order = 2)] public List<string> ShortnameList;
                [JsonProperty("Рейты игрока ( ПРИБАВЛЯЮТСЯ % ОТ ДОБЫТОГО ИМ РЕСУРСА)", Order = 3)] public int Rate;
                [JsonProperty("Сколько % прибавлять за уровень", Order = 4)] public int PercentIncrement;
                [JsonProperty("Сколько количество жизней будет увеличиваться на XRate", Order = 5)] public double HPIncrementInRate;
                [JsonProperty("Сколько максимум хп игрок получит от регенерации", Order = 6)] public int MaxRegen;
                [JsonProperty("Максимальная защита от способности", Order = 7)] public int MaxProtection;
                [JsonProperty("Максимальный урон от способности", Order = 8)] public int MaxAtack;
                [JsonProperty("Максимальный уровень", Order = 9)] public int MaxLevel;
                [JsonProperty("Сколько возвращать DNK при откате способности(0-100%)", Order = 10)] public int ReturnPercent;
            }
            [JsonProperty("Команда для открытия системы улучшения", Order = 1)] public string CommandName = "skill";
            [JsonProperty("Предметы-исключения(если игрок добаывает этим предметом , опыт не зачисляется)", Order = 3)] public List<string> ItemExceptionList;
            [JsonProperty("Настройка способностей", Order = 2)] public List<classSkill> configSkillSettings = new List<classSkill>();
            internal class classSkillSettings
            {
                [JsonProperty("Сколько опыта давать за убийство животных", Order = 5)] public double xpForKillAnimal;
                [JsonProperty("Сколько опыта давать за убийство NPC", Order = 2)] public double xpForKillNPC;
                [JsonProperty("Сколько опыта давать за убийство игрока в голову", Order = 4)] public double xpForKillHeadshot;
                [JsonProperty("Сколько опыта давать за убийство игрока", Order = 3)] public double xpForKillPlayer;
                [JsonProperty("Сколько опыта давать за добычу ресурсов", Order = 0)] public double xpForResourceExtraction;
                [JsonProperty("Сколько опыта давать за сбитие чинука", Order = 8)] public double xpForKillChinuk;
                [JsonProperty("Сколько опыта давать за разбитие бочек", Order = 1)] public double xpForBreakBarrels;
                [JsonProperty("Сколько опыта давать за взрыв танка", Order = 7)] public double xpForKillBredley;
                [JsonProperty("Сколько опыта давать за сбитие вертолета", Order = 6)] public double xpForKillHely;
            }
            [JsonProperty("Настройка опыта за действия", Order = 0)]
            public classSkillSettings xpSkillSettingsConfig = new classSkillSettings();
            [JsonProperty("Сколько DNK давать за новый уровень", Order = 0)] public int countDNKForNewLevel = 1;

            public static DefaultSettings SetDefaultSettings()
            {
                return new DefaultSettings
                {
                    countDNKForNewLevel = 1,
                    CommandName = "skill",
                    ItemExceptionList = new List<string> {
                                "jackhammer",
                                "shotgun.double",
                            },
                    xpSkillSettingsConfig = new classSkillSettings
                    {
                        xpForResourceExtraction = 1,
                        xpForBreakBarrels = 0.3,
                        xpForKillNPC = 5,
                        xpForKillPlayer = 4,
                        xpForKillHeadshot = 6,
                        xpForKillAnimal = 3,
                        xpForKillHely = 15,
                        xpForKillBredley = 12,
                        xpForKillChinuk = 20,
                    },
                    GUISettings = new classGUISettings
                    {
                        ButtonInfoColorHEX = "#138B3AFF",
                        ButtonPlusColorHEX = "#138B3AFF",
                        ActionColorHEX = "#FFFFFF30",
                        ButtonMinusColorHEX = "#841C1CFF",
                        BGImage = "https://i.imgur.com/KSnYPLm.png",
                        BackgroundColor = "0 0 0 0.5",
                        TitleText = "Система прокачки способностей",
                        SkillBackgroundColor = "0 0 0 0.3",
                        DNKText = "Очки ДНК ",
                        ProgressSettings = new classGUISettings.Progress
                        {
                            AnchorMin = "1 0",
                            AnchorMax = "1 0",
                            OffsetMin = "-235 16",
                            OffsetMax = "-210 98",
                            ProgressBGColor = "#D6E600A7",
                        }
                    },
                    configSkillSettings = new List<classSkill> {
                                new classSkill {
                                    Name = "Добытчик",
                                        Description = "Каждый уровень повышает количество добываемых ресурсов на 10%.",
                                        ShortnameList = new List < string > {
                                            "wood",
                                            "stones",
                                            "sulfur.ore",
                                            "metal.ore",
                                            "meat.boar",
                                            "bearmeat",
                                            "fat.animal",
                                            "chicken.raw",
                                            "deermeat.raw",
                                            "horsemeat.raw",
                                            "fish.minnows",
                                            "fish.raw",
                                            "fish.troutsmall",
                                            "wolfmeat.raw",
                                            "leather",
                                            "bone.fragments"
                                        },
                                        Rate = 0,
                                        PercentIncrement = 10,
                                        MaxLevel = 10,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Cтроитель",
                                        Description = "Повышает стойкость строений на 10% с каждым уровнем и содержание шкафа",
                                        ShortnameList = new List < string > {
                                            "stones",
                                        },
                                        Rate = 0,
                                        PercentIncrement = 10,
                                        MaxLevel = 10,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Ученый",
                                        Description = "Увеличивает выпадение серной руды",
                                        ShortnameList = new List < string > {
                                            "sulfur.ore"
                                        },
                                        Rate = 0,
                                        PercentIncrement = 10,
                                        MaxLevel = 10,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Мясник",
                                        Description = "Увеличивает количество ресурсов за разделку животных",
                                        ShortnameList = new List < string > {
                                        },
                                        Rate = 0,
                                        PercentIncrement = 20,
                                        MaxLevel = 5,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Метаболизм",
                                        Description = "При возраждении у вас будет больше здоровья",
                                        ShortnameList = new List < string > {},
                                        HPIncrementInRate = 1,
                                        PercentIncrement = 6,
                                        Rate = 0,
                                        MaxLevel = 5,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Вампиризм",
                                        Description = "При нанесении урона вы будете лечиться!",
                                        ShortnameList = new List < string > {},
                                        Rate = 0,
                                        PercentIncrement = 1,
                                        MaxRegen = 50,
                                        MaxLevel = 5,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Защита",
                                        Description = "Вы будете получать меньше урона!",
                                        ShortnameList = new List < string > {},
                                        MaxProtection = 1000,
                                        PercentIncrement = 8,
                                        Rate = 0,
                                        MaxLevel = 5,
                                        ReturnPercent = 50,
                                },
                                new classSkill {
                                    Name = "Нападение",
                                        Description = "Вы будете наносить больше урона!",
                                        ShortnameList = new List < string > {},
                                        MaxAtack = 500,
                                        Rate = 0,
                                        PercentIncrement = 8,
                                        MaxLevel = 5,
                                        ReturnPercent = 50

                                },
                            },
                };
            }
            [JsonProperty("Настройка UI", Order = 1)]
            public classGUISettings GUISettings = new classGUISettings();
            internal class classGUISettings
            {
                [JsonProperty("Лого в меню способностей (PNG)", Order = 0)] public string BGImage;
                [JsonProperty("Цвет кнопки + (HEX)", Order = 1)] public string ButtonPlusColorHEX;
                [JsonProperty("Цвет панели с действием над скиллом ( + - ) (HEX)", Order = 2)] public string ActionColorHEX;
                [JsonProperty("Цвет кнопки - (HEX)", Order = 3)] public string ButtonMinusColorHEX;
                [JsonProperty("Цвет заднего фона в меню способностей", Order = 4)] public string BackgroundColor;
                [JsonProperty("Текст в меню способностей", Order = 5)] public string TitleText;
                [JsonProperty("Цвет заднего фона способности", Order = 6)] public string SkillBackgroundColor;
                [JsonProperty("Цвет кнопки открывающей описание", Order = 7)] public string ButtonInfoColorHEX;
                [JsonProperty("Текст <Очки ДНК>", Order = 8)] public string DNKText;
                internal class Progress
                {
                    [JsonProperty("AnchorMin прогресс бара", Order = 0)] public string AnchorMin;
                    [JsonProperty("AnchorMax прогресс бара", Order = 1)] public string AnchorMax;
                    [JsonProperty("OffsetMin прогресс бара", Order = 2)] public string OffsetMin;
                    [JsonProperty("OffsetMax прогресс бара", Order = 3)] public string OffsetMax;
                    [JsonProperty("Цвет заполняющейся полосы (HEX)", Order = 4)] public string ProgressBGColor;
                }
                [JsonProperty("Настройка UI-прогресс-бара", Order = 7)] public Progress ProgressSettings = new Progress();
            }
            [JsonProperty("Сообщение при 100% опыта", Order = 2)] public string LevelupText = "Вы получили очки ДНК за новый уровень! /skill";
        }

        protected override void SaveConfig() => Config.WriteObject(MainGUISettings);


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                MainGUISettings = Config.ReadObject<DefaultSettings>();
                if (MainGUISettings?.countDNKForNewLevel == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name }', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => MainGUISettings = DefaultSettings.SetDefaultSettings();

        #endregion CONFIG

        #region СОБЫТИЯ


        #region ДОБЫЧА РЕСОВ (ДРОВОСЕК, ШАХТЕР, СЕРА)
        //СОБЫТИЕ ДО СБОРА ИГРОКОМ РЕСУРСОВ
        [Oxide.Core.Plugins.HookMethod("OnDispenserGather")]
        void HookOnDisoenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            DispenserGatherFunc(dispenser, entity, item);
        }

        //ФУНКЦИЯ СРАБАТЫВАЕТ ДО НАЧАЛА ПОДНЯТИЯ РЕСУРСА
        void DispenserGatherFunc(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity as BasePlayer;
            foreach (var Data in PlayersSkillDataMassiv[player.userID])
            {
                //Цикл по элементам списка shortname
                foreach (var DataList in Data.ShortnameList)
                {
                    if (item.info.shortname == DataList)
                    {
                        int Amount = item.amount * (100 + Data.Rate) / 100;
                        item.amount = Amount;
                    }
                }
            }
        }
        #endregion ДОБЫЧА РЕСОВ (ДРОВОСЕК, ШАХТЕР, СЕРА)

        #region ПОВЫШЕНИЕ ОПЫТА ЗА ОКОНЧАНИЕ СБОРА РЕСОВ
        //ХУК НА ОКОНЧАНИЕ СБОРА РЕСУРСА
        [Oxide.Core.Plugins.HookMethod("OnDispenserBonus")]
        object MyHookDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            object TotalObject = null;
            object ObjectBonus = null;
            ObjectBonus = DispenserBonusFunc(dispenser, player, item);
            if (ObjectBonus != null) TotalObject = ObjectBonus;
            return TotalObject;
        }

        //ПОВЫШАЕМ ОПЫТ ЗА ОКНЧАНИЕ СБОРА РЕСУРСА
        object DispenserBonusFunc(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            foreach (var Data in PlayersSkillDataMassiv[player.userID])
            {
                foreach (var DataList in Data.ShortnameList)
                {
                    if (item.info.shortname == DataList)
                    {
                        int Amount = item.amount * (100 + Data.Rate) / 100;
                        item.amount = Amount;
                    }
                }
            }
            var shortName = player.GetActiveItem().info.shortname;
            if (shortName == null) return null;
            if (MainGUISettings.ItemExceptionList.Contains(shortName)) return null;
            //ДАЕМ ОПЫТ ЗА ДОБЫЧУ РЕСУРСА
            GetXP(player, MainGUISettings.xpSkillSettingsConfig.xpForResourceExtraction);
            return null;
        }

        //При собирании руды
        void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            foreach (var Data in PlayersSkillDataMassiv[player.userID])
            {
                foreach (var DataList in Data.ShortnameList)
                {
                    if (item.info.shortname == DataList)
                    {
                        int Amount = item.amount * (100 + Data.Rate) / 100;
                        item.amount = Amount;
                    }

                }

            }
        }

        #endregion ПОВЫШЕНИЕ ОПЫТА ЗА ОКОНЧАНИЕ СБОРА РЕСОВ

        #region МЕТАБОЛИЗМ
        //ХУК срабатывает когда игрок возраждается 
        [Oxide.Core.Plugins.HookMethod("OnPlayerRespawned")]
        void myHookOnPlayerRespawn(BasePlayer player)
        {
            AddHealth(player);
        }
        //Добавление жизней игроку при возрождении (Цепляется на хук)
        private void AddHealth(BasePlayer player)
        {
            if (!PlayersSkillDataMassiv.ContainsKey(player.userID)) return;
            foreach (var Data in PlayersSkillDataMassiv[player.userID])
            {
                if (Data.HPIncrementInRate > 0)
                {
                    double HPValue = Data.HPIncrementInRate * Data.Rate + Data.HPIncrementInRate;
                    double hydrValue = 5 * (Data.HPIncrementInRate * Data.Rate) + Data.HPIncrementInRate;
                    double eatValue = 10 * Data.HPIncrementInRate * Data.Rate + Data.HPIncrementInRate;
                    player.health += (float)HPValue;
                    player.metabolism.hydration.value += (float)hydrValue;
                    player.metabolism.calories.value += (float)eatValue;
                    player.metabolism.calories.value += (float)eatValue;
                }
            }
        }

        object OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {

            if (!PlayersSkillDataMassiv.ContainsKey(player.userID)) return null;
            if (oldValue < newValue) return null;
            if ((oldValue - newValue) < 5) return null;
            foreach (var Data in PlayersSkillDataMassiv[player.userID])
            {
                if (Data.HPIncrementInRate > 0)
                {
                    if ((newValue < 30) && (newValue > 0))
                    {
                        int r = Random.Range(1, 8 - Data.Level);
                        if (r == 1)
                        {
                            player.health += 18 + (Data.Level) * 2;
                        }
                    }
                }
            }

            return null;
        }

        #endregion МЕТАБОЛИЗМ

        #region ОПЫТ ЗА УБИЙСТВА
        //При смерти сущности
        [Oxide.Core.Plugins.HookMethod("OnEntityDeath")]
        void OnDeadEntity(BaseCombatEntity entity, HitInfo info)
        {
			if (entity==null || info==null) return;
            EntityDeathFunc(entity, info);
        }

        //ДОБАВЛЕНИЕ ОПЫТА ЗА УБИЙСТВА
        void EntityDeathFunc(BaseCombatEntity entity, HitInfo info)
        {
            if (entity as BaseHelicopter)
            {
                if (HeliAtackerList.ContainsKey(entity.net.ID))
                {
                    var HeliKiller = HeliAtackerList[entity.net.ID];
                    GetXP(HeliKiller, MainGUISettings.xpSkillSettingsConfig.xpForKillHely);
                }
            }
            if (entity as BradleyAPC)
            {
                if (BradleyAtackerList.ContainsKey(entity.net.ID))
                {
                    var BradleyKiller = BradleyAtackerList[entity.net.ID];
                    GetXP(BradleyKiller, MainGUISettings.xpSkillSettingsConfig.xpForKillBredley);
                }
            }
            if (entity == null || info == null || info.Initiator is BaseNpc || info.Initiator is ScientistNPC || info.InitiatorPlayer == null || info.InitiatorPlayer.GetComponent<NPCPlayer>()) return;
            if (info?.InitiatorPlayer == null) return;
            BasePlayer atacker = info.InitiatorPlayer;
            if (atacker as BasePlayer)
            {
                if (entity as BaseAnimalNPC)
                {
                    GetXP(atacker, MainGUISettings.xpSkillSettingsConfig.xpForKillAnimal);
                }
                if (entity as BasePlayer)
                {
                    if (atacker.userID == (entity as BasePlayer).userID) return;
                    if (info.isHeadshot) GetXP(atacker, MainGUISettings.xpSkillSettingsConfig.xpForKillHeadshot);
                    else GetXP(atacker, MainGUISettings.xpSkillSettingsConfig.xpForKillPlayer);
                }
                if (entity as CH47Helicopter)
                {
                    GetXP(atacker, MainGUISettings.xpSkillSettingsConfig.xpForKillChinuk);
                }
                if (entity as ScientistNPC)
                {
                    GetXP(atacker, MainGUISettings.xpSkillSettingsConfig.xpForKillNPC);
                }
                if (entity.ShortPrefabName.Contains("barrel"))
                {
                    GetXP(atacker, MainGUISettings.xpSkillSettingsConfig.xpForBreakBarrels);
                }
            }
        }

        #endregion

        //ХУК на получение дамага
        [Oxide.Core.Plugins.HookMethod("OnEntityTakeDamage")]
        void MyHookTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            TakeDamageFunc(entity, info);
        }
		
        //СОБЫТИЕ ПРИ НАНЕСЕНИИ УРОНА ПО СУЩНОСТИ
        void TakeDamageFunc(BaseCombatEntity entity, HitInfo info)
        {
			if (entity==null || info==null) return;
			
			bool atackerNPC=false;
			bool entityNPC=false;
            if (info.Initiator is BaseNpc || info.Initiator is ScientistNPC || info.Initiator is Scientist || info.Initiator is HTNPlayer || info.InitiatorPlayer == null) atackerNPC=true;
            if (entity is BaseNpc || entity is ScientistNPC || entity is Scientist || entity is HTNPlayer || entity == null) entityNPC=true;            
			if (atackerNPC==false && (info.InitiatorPlayer is BasePlayer)==false) return;
			if (entityNPC==false && (entity is BasePlayer)==false) return;
			
  		    if (entity is BaseHelicopter && !atackerNPC && info.InitiatorPlayer != null) HeliAtackerList[entity.net.ID] = info.InitiatorPlayer;
            if (entity is BradleyAPC && !atackerNPC && info.InitiatorPlayer != null) BradleyAtackerList[entity.net.ID] = info.InitiatorPlayer;
            if (atackerNPC && entityNPC) return;
			
			//если урон нанес сам себе
			if (!entityNPC && !atackerNPC) if (info.InitiatorPlayer.userID == (entity as BasePlayer).userID) return;
			//Находим рейты для атакующего и повышаем атаку + подрегениваем
			if (!atackerNPC){
				BasePlayer atacker = info.InitiatorPlayer;
				if (!PlayersSkillDataMassiv.ContainsKey(atacker.userID)) return;
				//double atackerRate=0;
				foreach (var Data in PlayersSkillDataMassiv[atacker.userID])
				{
					if (Data.MaxAtack != 0 && info.damageTypes.Total() > 5)
					{
						//РАСЧЕТЫ УСИЛЕНИЯ АТАКИ				
						double DamageValue = 1 + (0.01f * Data.Rate);
						if (DamageValue <= Data.MaxAtack) info.damageTypes.ScaleAll((float)DamageValue);
					}
					//Нужно ли регенить
					if (Data.MaxRegen > 0)
					{
						//Формула отхила
						double RegenValue = info.damageTypes.Total() * 0.01f * Data.Rate;
						if (RegenValue > Data.MaxRegen) RegenValue = Data.MaxRegen;
						atacker.Heal((float)RegenValue);
					}
				}
			}
			//Работаем с защитой жертвы смотрим рейты и снижаем атаку
			if (entity is BasePlayer) {
				BasePlayer enemy = entity as BasePlayer;
				if (!entityNPC){
					if (!PlayersSkillDataMassiv.ContainsKey(enemy.userID)) return;
					//Вычитаем защиту
					foreach (var DataTarget in PlayersSkillDataMassiv[enemy.userID]){
						if (DataTarget.MaxProtection > 0)
						{
							//Уменьшаем текущий дамаг на рейты
							double DamageTotal = 1 - (0.007 * DataTarget.Rate);
							info.damageTypes.ScaleAll((float)DamageTotal);
						}
					}	
				}
			}
        }

        //ПРИ ИНИЦИАЛИЗАЦИИ
        [Oxide.Core.Plugins.HookMethod("OnServerInitialized")]
        void MyHookServerInit()
        {
            InitSkillSystem();
        }


        //ИНИЦИАЛИЗАЦИЯ
        private void InitSkillSystem()
        {
            PlayersSkillParameters = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, classSkillParameters>>("SkillSystem/ProgressPlayer");
            PlayersSkillDataMassiv = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PlayerSkillData>>>("SkillSystem/PlayerSkills");
			
            AddImage(MainGUISettings.GUISettings.BGImage, "Logo");
            foreach (BasePlayer player in BasePlayer.activePlayerList) PlayerInitFunc(player);

            cmd.AddChatCommand(MainGUISettings.CommandName, this, nameof(cmdShowSkillWindow));
        }


        //ХУК ПРИ ВЫГРУЗКИ ПЛАГИНА
        [Oxide.Core.Plugins.HookMethod("Unload")]
        void MyHookUnload()
        {
            UnloadFunc();
			MyHookOnServerSave();
        }
        //ПРИ ВЫГРУЗКИ ПЛАГИНА
        void UnloadFunc()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, CUILayerName);

        }

        //При сохранении данных
        [Oxide.Core.Plugins.HookMethod("OnServerSave")]
        void MyHookOnServerSave()
        {
            ServerSaveFunc();
        }

        //ФУНКЦИЯ ПРИ СОХРАНЕНИИ ДАННЫХ НА СЕРВЕРЕ
        void ServerSaveFunc()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SkillSystem/ProgressPlayer", PlayersSkillParameters);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SkillSystem/PlayerSkills", PlayersSkillDataMassiv);
        }


        //При инициализации игрока
        [Oxide.Core.Plugins.HookMethod("OnPlayerConnected")]
        void MyHookPlayerInit(BasePlayer player)
        {
            PlayerInitFunc(player);
        }
        //ПРИ ИНИЦИАЛИЗАЦИИ ИГРОКА
        public void PlayerInitFunc(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => PlayerInitFunc(player));
                return;
            }
            if (!PlayersSkillParameters.ContainsKey(player.userID))
            {
                classSkillParameters SkillParametersPlayer = new classSkillParameters()
                {
                    countXP = 0,
                    countDNK = 3
                };
                PlayersSkillParameters.Add(player.userID, SkillParametersPlayer);
            }
            if (!PlayersSkillDataMassiv.ContainsKey(player.userID))
            {
                PlayersSkillDataMassiv.Add(player.userID, DefaultPlayersSkillData());
            }
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SkillSystem/ProgressPlayer", PlayersSkillParameters);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SkillSystem/PlayerSkills", PlayersSkillDataMassiv);

            ShowXPProgressGUI(player);
        }


        #endregion СОБЫТИЯ
        #region GUI

        public void ShowMainUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIName);
            CuiElementContainer UIContainer = new CuiElementContainer();
            UIContainer.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                Image = {
                        Color = MainGUISettings.GUISettings.BackgroundColor,
                        Material = "assets/content/ui/uibackgroundblur.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    }
            }, "Overlay", UIName);
            UIContainer.Add(new CuiButton
            {
                RectTransform = {
                        AnchorMin = "-100 -100",
                        AnchorMax = "100 100"
                    },
                Button = {
                        Close = UIName,
                        Color = "0 0 0 0"
                    },
                Text = {
                        FadeIn = 0.8f,
                        Text = ""
                    }
            }, UIName);
            UIContainer.Add(new CuiLabel
            {
                RectTransform = {
                        AnchorMin = "0 0.9166671",
                        AnchorMax = "1 0.9861115"
                    },
                Text = {
                        Text = MainGUISettings.GUISettings.TitleText,
                        FontSize = 30,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = getHEXColor("#FFFFFFFF")
                    }
            }, UIName);
            UIContainer.Add(new CuiElement
            {
                Parent = UIName,
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("Logo")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.3781239 0.2055555", AnchorMax = $"0.6072917 0.9055554"
                    },
                }
            });
            ShowSkillGUI(player, UIContainer);
            CuiHelper.AddUi(player, UIContainer);
        }

        //GUI ПАНЕЛЬКА ОПЫТА
        void ShowXPProgressGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUILayerName);
            CuiElementContainer container = new CuiElementContainer();
            var CurrentProgressSetting = MainGUISettings.GUISettings.ProgressSettings;
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = {
                    AnchorMin = CurrentProgressSetting.AnchorMin,
                    AnchorMax = CurrentProgressSetting.AnchorMax,
                    OffsetMin = CurrentProgressSetting.OffsetMin,
                    OffsetMax = CurrentProgressSetting.OffsetMax
                },
                Image = {
                    Color = "0.968627453 0.921568632 0.882352948 0.03529412",
                    Sprite = "assets/content/ui/ui.spashscreen.psd"
                }
            }, "Hud", CUILayerName);
            float ProgressValue = (float)PlayersSkillParameters[player.userID].countXP / 100;
            container.Add(new CuiElement
            {
                Parent = CUILayerName,
                Components = {
                    new CuiImageComponent {
                        Color = getHEXColor(CurrentProgressSetting.ProgressBGColor), Sprite = "assets/content/ui/ui.background.transparent.linear.psd"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0", AnchorMax = $"1 {ProgressValue }", OffsetMin = "1 1", OffsetMax = "-2 -1"
                    },
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = {
                    AnchorMin = "0 0.4583333",
                    AnchorMax = "1 0.6416667"
                },
                Text = {
                    Text = String.Format("{0:00.0}", PlayersSkillParameters[player.userID].countXP.ToString()) + "%",
                    FontSize = 9,
                    Font = "robotocondensed-regular.ttf",
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.3"
                }
            }, CUILayerName);
            container.Add(new CuiButton
            {
                RectTransform = {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Button = {
                    Command = "open_dnk_system",
                    Color = "0 0 0 0"
                },
                Text = {
                    FadeIn = 0.8f,
                    Text = ""
                }
            }, CUILayerName);
            CuiHelper.AddUi(player, container);
        }

        //Добавление опыта и обновление GUI
        public void GetXP(BasePlayer player, double param)
        {
			if (player==null) return;
			if (!PlayersSkillParameters.ContainsKey(player.userID)) return;
            var SkillParametrs = PlayersSkillParameters[player.userID];
            SkillParametrs.countXP += param;
            if (SkillParametrs.countXP >= 100)
            {
                SkillParametrs.countXP = 0;
                SkillParametrs.countDNK += MainGUISettings.countDNKForNewLevel;
                Log("log", $"{player.displayName} заработал очко ДНК");
                SendReply(player, MainGUISettings.LevelupText);
            }
            ShowXPProgressGUI(player);
        }

        //GUI окна скилов
        private IEnumerator ShowSkillGUI(BasePlayer player, CuiElementContainer container)
        {
            CuiHelper.DestroyUi(player, $"Score");
            //Надпись ОЧКИ ДНК
            container.Add(new CuiLabel
            {
                RectTransform = {
                    AnchorMin = "0.4296875 0.1731482",
                    AnchorMax = "0.5546875 0.2046297"
                },
                Text = {
                    Text = MainGUISettings.GUISettings.DNKText,
                    FontSize = 18,
                    Font = "robotocondensed-regular.ttf",
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, UIName, "Score");
            //Кол-во ДНК
            container.Add(new CuiLabel
            {
                RectTransform = {
                    AnchorMin = "0.4296875 0.1031482",
                    AnchorMax = "0.5546875 0.1746297"
                },
                Text = {
                    Text = String.Format("{0:00.0}", PlayersSkillParameters[player.userID].countDNK),
                    FontSize = 28,
                    Font = "robotocondensed-regular.ttf",
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.5"
                }
            }, UIName, "Score");
            for (int x = 0, y = 0, i = 0; i < MainGUISettings.configSkillSettings.Count; i++)
            {
                var SkillSetting = MainGUISettings.configSkillSettings[i];
                int currentLevel = PlayersSkillDataMassiv[player.userID][i].Level;
                CuiHelper.DestroyUi(player, $"PanelSkill_{i }");
                container.Add(new CuiPanel
                {
                    RectTransform = {
                        AnchorMin = $"{0.1015625 + (x * 0.5)} {0.75 - (y * 0.15)}",
                        AnchorMax = $"{0.3510417 + (x * 0.5)} {0.7879629 - (y * 0.15)}"
                    },
                    Image = {
                        Color = MainGUISettings.GUISettings.SkillBackgroundColor,
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    }
                }, UIName, $"PanelSkill_{i }");
                container.Add(new CuiLabel
                {
                    RectTransform = {
                        AnchorMin = "0.01252624 0.1219493",
                        AnchorMax = "0.6346555 0.9024402"
                    },
                    Text = {
                        Text = SkillSetting.Name,
                        FontSize = 18,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter
                    }
                }, $"PanelSkill_{i }");
                container.Add(new CuiPanel
                {
                    RectTransform = {
                        AnchorMin = "0.6471796 0",
                        AnchorMax = "1 1"
                    },
                    Image = {
                        Color = getHEXColor(MainGUISettings.GUISettings.ActionColorHEX),
                        Material = "assets/content/ui/uibackgroundblur.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    }
                }, $"PanelSkill_{i }", "PanelPlusMinus");
                container.Add(new CuiLabel
                {
                    RectTransform = {
                        AnchorMin = "0.2335334 0",
                        AnchorMax = "0.736527 1"
                    },
                    Text = {
                        Text = $"{currentLevel }/{SkillSetting .MaxLevel }",
                        FontSize = 14,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter
                    }
                }, "PanelPlusMinus");
                container.Add(new CuiButton
                {
                    RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "0.1241609 0.965"
                    },
                    Button = {
                        Command = $"opendescription {i }",
                        Color = getHEXColor(MainGUISettings.GUISettings.ButtonInfoColorHEX)
                    },
                    Text = {
                        Text = "?",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 22
                    }
                }, $"PanelSkill_{i }", "OpenDescription");
                container.Add(new CuiButton
                {
                    RectTransform = {
                        AnchorMin = "0.7485032 0",
                        AnchorMax = "1 0.965"
                    },
                    Button = {
                        Command = $"plussbtn {i}",
                        Color = getHEXColor(MainGUISettings.GUISettings.ButtonPlusColorHEX)
                    },
                    Text = {
                        Text = "+",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 22
                    }
                }, "PanelPlusMinus", "PlusBTN");
                container.Add(new CuiButton
                {
                    RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "0.2275454 0.965"
                    },
                    Button = {
                        Command = $"minusbtn {i}",
                        Color = getHEXColor(MainGUISettings.GUISettings.ButtonMinusColorHEX)
                    },
                    Text = {
                        Text = "-",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 22
                    }
                }, "PanelPlusMinus", "MinusBTN");
                y++;
                if (y >= 4)
                {
                    y = 0;
                    x++;
                }
            }
            return null;
        }

        //ВЫГРУЗКА UI
        private IEnumerator UnloadUI(BasePlayer player, CuiElementContainer container, int element)
        {
            CuiHelper.DestroyUi(player, DescLayerName);
            CuiHelper.DestroyUi(player, $"Description{element }");
            var CurrentSkillSettings = MainGUISettings.configSkillSettings[element];
            int CurrentLevel = PlayersSkillDataMassiv[player.userID][element].Level;
            container.Add(new CuiPanel
            {
                RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "0 -77",
                        OffsetMax = "0 -30"
                    },
                Image = {
                        Color = MainGUISettings.GUISettings.SkillBackgroundColor,
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    }
            }, $"PanelSkill_{element }", $"Description{element }");
            container.Add(new CuiLabel
            {
                RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                Text = {
                        Text = CurrentSkillSettings.Description,
                        FontSize = 15,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter
                    }
            }, $"Description{element }");
            return null; //u9340

        }


        #endregion GUI

        #region COMMANDS

        //Функция с ImageLibrary
        Plugin ImageLibrary => Interface.Oxide.RootPluginManager.GetPlugin("ImageLibrary");
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public string GetImage(string Name, ulong l = 0) => (string)ImageLibrary?.Call("GetImage", Name, l);


        //Конвертация цвета HEX
        private static string getHEXColor(string colorValue)
        {
            if (string.IsNullOrEmpty(colorValue))
            {
                colorValue = "#FFFFFFFF";
            }
            var trimedColor = colorValue.Trim('#');
            if (trimedColor.Length == 6) trimedColor += "FF";
            if (trimedColor.Length != 8)
            {
                throw new Exception(colorValue);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var RedValue = byte.Parse(trimedColor.Substring(0, 2), NumberStyles.HexNumber);
            var GreenValue = byte.Parse(trimedColor.Substring(2, 2), NumberStyles.HexNumber);
            var BlueValue = byte.Parse(trimedColor.Substring(4, 2), NumberStyles.HexNumber);
            var OpacityValue = byte.Parse(trimedColor.Substring(6, 2), NumberStyles.HexNumber);
            UnityEngine.Color UColor = new Color32(RedValue, GreenValue, BlueValue, OpacityValue);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", UColor.r, UColor.g, UColor.b, UColor.a);
        }

        //УВЕЛИЧЕНИЕ УРОВНЯ (НАЖАТИЕ НА ПЛЮС)        

        [ConsoleCommand("plussbtn")]
        void cmdPlusBtnPress(ConsoleSystem.Arg Argum)
        {
            BasePlayer player = Argum.Player();
            int element = Convert.ToInt32(Argum.Args[0]);
            cmdPlusLevel(player, element);
        }

        public void cmdPlusLevel(BasePlayer player, int element)
        {
            var CurrentSkillSettings = MainGUISettings.configSkillSettings[element];
            var CurrentPlayerSkillData = PlayersSkillDataMassiv[player.userID][element];
            var CurrentParam = PlayersSkillParameters[player.userID];
            if (CurrentParam.countDNK >= 1)
            {
                //Проверка потолка
                if (CurrentPlayerSkillData.Level >= CurrentSkillSettings.MaxLevel) return;
                //Увеличиваем уровень
                CurrentPlayerSkillData.Level++;
                //Расчитываем рейты
                CurrentPlayerSkillData.Rate += CurrentSkillSettings.PercentIncrement;

                if (CurrentSkillSettings.HPIncrementInRate > 0) CurrentPlayerSkillData.HPIncrementInRate = CurrentSkillSettings.HPIncrementInRate;
                if (CurrentSkillSettings.MaxProtection > 0) CurrentPlayerSkillData.MaxProtection = CurrentSkillSettings.MaxProtection;
                if (CurrentSkillSettings.MaxRegen > 0) CurrentPlayerSkillData.MaxRegen = CurrentSkillSettings.MaxRegen;
                if (CurrentSkillSettings.MaxAtack > 0) CurrentPlayerSkillData.MaxAtack = CurrentSkillSettings.MaxAtack;
                for (int a = 0; a < CurrentSkillSettings.ShortnameList.Count; a++)
                    if (CurrentPlayerSkillData.ShortnameList == null || !CurrentPlayerSkillData.ShortnameList.Contains(CurrentSkillSettings.ShortnameList[a])) CurrentPlayerSkillData.ShortnameList.Add(CurrentSkillSettings.ShortnameList[a]);
                CurrentParam.countDNK -= 1;
                Log("log", $"{player.displayName} потратил очко ДНК на улучшение {CurrentPlayerSkillData.Name}. Уровень способности {CurrentPlayerSkillData.Level}");
                CuiElementContainer container = new CuiElementContainer();
                ShowSkillGUI(player, container);
                CuiHelper.AddUi(player, container);
            }
        }

        [ConsoleCommand("skillpoint")]
        void GiveSkillPointConsoleCommand(ConsoleSystem.Arg arg)
        {
			var cplayer = arg?.Player();
			if (cplayer!=null) {
				if (!cplayer.IsAdmin)
				{
					SendReply(cplayer, "Команда доступна только администраторам");
					return;
				}
			}
            if (arg == null || arg.FullString.Length == 0)
            {
                PrintError("Не указаны параметры для выполнения команды skillpoint");
                return;
            }
		    BasePlayer player = BasePlayer.Find(arg.Args[0]);
		    if (player == null)
		    {
			    PrintError($"[Ошибка] Не удается найти игрока {arg.Args[0]}");
			    return;
		    }
			if (!PlayersSkillParameters.ContainsKey(player.userID)) return;
			int count=1;
			if (arg.Args.Length == 2) if (!int.TryParse(arg.Args[1], out count)){ 
				count = 1;
		    }
			//Получаем данные по игроку
			var CurrentParam = PlayersSkillParameters[player.userID];
			CurrentParam.countDNK += count;
			Puts($"{count} skillpoint gived to {player}");
			Log("log", $"{player.displayName} получил очки ДНК в количестве {count}");
			SendReply(player,$"Вы получили очки ДНК: {count}");
			
        }
        //ПРИ НАЖАТИИ НА МИНУС
        [ConsoleCommand("minusbtn")]
        void cmdMinusBtnPress(ConsoleSystem.Arg Argum)
        {
            BasePlayer player = Argum.Player();
            int element = Convert.ToInt32(Argum.Args[0]);
            cmdMinusLevel(player, element);
        }

        public void cmdMinusLevel(BasePlayer player, int element)
        {
            var CurrentSkillSettings = MainGUISettings.configSkillSettings[element];
            var CurrentPlayerSkillData = PlayersSkillDataMassiv[player.userID][element];
            var CurrentParam = PlayersSkillParameters[player.userID];
            if (CurrentPlayerSkillData.Level <= 0) return;
            CurrentPlayerSkillData.Level--;
            if (CurrentPlayerSkillData.Level == 0)
                for (int a = 0; a < CurrentSkillSettings.ShortnameList.Count; a++)
                    if (CurrentPlayerSkillData.ShortnameList.Contains(CurrentSkillSettings.ShortnameList[a]))
                    {
                        CurrentPlayerSkillData.ShortnameList.Remove(CurrentSkillSettings.ShortnameList[a]);
                        CurrentPlayerSkillData.HPIncrementInRate = 0;
                        CurrentPlayerSkillData.MaxProtection = 0;
                        CurrentPlayerSkillData.MaxAtack = 0;
                        CurrentPlayerSkillData.MaxRegen = 0;
                    }
            float DNKRefuse = (float)0.01 * (float)CurrentSkillSettings.ReturnPercent;
            CurrentPlayerSkillData.Rate -= CurrentSkillSettings.PercentIncrement;
            CurrentParam.countDNK += DNKRefuse;
            Log("log", $"{player.displayName} откатил навык {CurrentPlayerSkillData.Name} и вернул {DNKRefuse} ДНК на улучшение. Уровень способности {CurrentPlayerSkillData.Level}");
            CuiElementContainer container = new CuiElementContainer();
            ShowSkillGUI(player, container);
            CuiHelper.AddUi(player, container);
        }

        //КОМАНДА ПОКАЗАТЬ ОКНО
        void cmdShowSkillWindow(BasePlayer player)
        {
            ShowMainUI(player);
        }

        //РАЗВЕРНУТЬ ОПИСАНИЕ
        [ConsoleCommand("opendescription")]
        void cmdOpenDescription(ConsoleSystem.Arg Args)
        {
            BasePlayer player = Args.Player();
            CuiElementContainer CUiContainer = new CuiElementContainer();
            UnloadUI(player, CUiContainer, Convert.ToInt32(Args.Args[0]));
            CuiHelper.AddUi(player, CUiContainer);
        }

        //ПОКАЗАТЬ ГЛАВНОЕ ОКНО ПРОКАЧКИ
        [ConsoleCommand("open_dnk_system")]
        void cmdOpenDNKPanel(ConsoleSystem.Arg Argum)
        {
            ShowMainUI(Argum.Player());
        }


        #endregion COMMAND		

        void Log(string filename, string text) => LogToFile(filename, $"[{DateTime.Now}] {text}", this);
    }
}