using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PluginLoader;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;
using TranscendPlugins.Shared.UI;

namespace TaiPlugins
{
    [PluginDescription("整合工具面板：便捷功能、Buff/旗帜背包及三个额外饰品栏。使用 U 或 /menu 打开。")]
    public class TaiTools : PluginBase, IPluginChatCommand, IPluginDrawUI, IPluginUpdate,
        IPluginPlayerPreUpdate, IPluginPlayerUpdate, IPluginPlayerUpdateArmorSets,
        IPluginPlayerPickTile
    {
        private const int BagSize = 100;
        private const int SlotsPerPage = 25;
        private const int SlotContext = ItemSlot.Context.BankItem;
        private const int BlocksPerTickInMultiplayer = 4;
        private const int BlocksPerTickInSinglePlayer = 32;
        private const float ManaCostPerBlock = 100f;
        private const int ExtraAccessoryCount = 3;

        [SettingLabel("连锁挖矿")]
        [SettingDescription("挖掉矿脉中的一块后自动挖掉相连的同类矿物。")]
        private static readonly Setting<bool> VeinMiningEnabled = true;

        [SettingLabel("矿脉最大方块数")]
        [SettingRange(1, 10000)]
        private static readonly Setting<int> MaxBlocks = 300;

        [SettingLabel("限制挖掘距离")]
        private static readonly Setting<bool> RangeLimit = false;

        [SettingLabel("连锁挖矿消耗魔力")]
        private static readonly Setting<bool> RequireMana = false;

        [SettingLabel("Boss 召唤物不消耗")]
        [SettingDescription("使用常规 Boss 召唤物时不减少数量。")]
        private static readonly Setting<bool> BossSummonsNotConsumed = true;

        [SettingLabel("启用三个额外饰品栏")]
        [SettingDescription("所有难度固定获得三个由工具箱保存的额外饰品格。")]
        private static readonly Setting<bool> ExtraAccessoriesEnabled = true;

        [SettingIds(typeof(TileID))]
        [SettingDescription("会触发连锁挖矿的物块。")]
        private static readonly Setting<HashSet<ushort>> VeinTiles = new HashSet<ushort>
        {
            TileID.Copper, TileID.Tin, TileID.Iron, TileID.Lead, TileID.Silver, TileID.Tungsten,
            TileID.Gold, TileID.Platinum, TileID.Demonite, TileID.Crimtane, TileID.Meteorite,
            TileID.Obsidian, TileID.Hellstone, TileID.Cobalt, TileID.Palladium, TileID.Mythril,
            TileID.Orichalcum, TileID.Adamantite, TileID.Titanium, TileID.Chlorophyte, TileID.LunarOre,
            TileID.Amethyst, TileID.Topaz, TileID.Sapphire, TileID.Emerald, TileID.Ruby, TileID.Diamond
        };

        private static readonly Dictionary<int, int> StationBuffs = new Dictionary<int, int>
        {
            { ItemID.CrystalBall, BuffID.Clairvoyance },
            { ItemID.Campfire, BuffID.Campfire },
            { ItemID.HeartLantern, BuffID.HeartLamp },
            { ItemID.AmmoBox, BuffID.AmmoBox },
            { ItemID.BewitchingTable, BuffID.Bewitched },
            { ItemID.WaterCandle, BuffID.WaterCandle },
            { ItemID.PeaceCandle, BuffID.PeaceCandle },
            { ItemID.ShadowCandle, BuffID.ShadowCandle },
            { ItemID.Sunflower, BuffID.Sunflower },
            { ItemID.StarinaBottle, BuffID.StarInBottle },
            { ItemID.SharpeningStation, BuffID.Sharpened },
            { ItemID.SliceOfCake, BuffID.SugarRush },
            { ItemID.CatBast, BuffID.CatBast },
            { ItemID.WarTable, BuffID.WarTable },
            { ItemID.DeadCellsPotionStation, BuffID.DeadCellsPotionStation },
            { ItemID.Sake, BuffID.Tipsy },
            { ItemID.Ale, BuffID.Tipsy },
            { ItemID.HoneyBucket, BuffID.Honey },
            { ItemID.BottomlessHoneyBucket, BuffID.Honey }
        };

        private static readonly HashSet<int> BossSummonItems = new HashSet<int>
        {
            ItemID.SlimeCrown, ItemID.SuspiciousLookingEye, ItemID.WormFood, ItemID.BloodySpine,
            ItemID.Abeemination, ItemID.DeerThing, ItemID.QueenSlimeCrystal,
            ItemID.MechanicalEye, ItemID.MechanicalWorm, ItemID.MechanicalSkull,
            ItemID.LihzahrdPowerCell, ItemID.CelestialSigil
        };

        private readonly Item[] buffBag = new Item[BagSize];
        private readonly bool[] buffEnabled = new bool[BagSize];
        private readonly Item[] bannerBag = new Item[BagSize];
        private readonly bool[] bannerEnabled = new bool[BagSize];
        private readonly Item[] extraAccessories = new Item[ExtraAccessoryCount];

        private bool menuOpen;
        private int activeTab;
        private int bagPage;
        private int bannerPage;
        private string loadedPlayer = "";
        private bool applyingSocialArmorSet;
        private int windowX = -1;
        private int windowY = -1;
        private bool draggingWindow;
        private int dragOffsetX;
        private int dragOffsetY;
        private bool capturingMenuKey;
        private bool suppressCapturedKey;

        private bool autoFishing;
        private bool choosingFishingTarget;
        private bool hadBobber;
        private bool warnedNoBait;
        private int recastDelay;
        private Vector2 fishingTarget;

        private readonly Queue<Point> veinQueue = new Queue<Point>();
        private readonly HashSet<int> veinSeen = new HashSet<int>();
        private int veinType = -1;
        private int veinPickPower;
        private int veinMined;
        private float owedMana;
        private Point swungAt;
        private int swungAtType = -1;
        private int swungAtPickPower;

        public TaiTools() : base(toggleKey: Keys.U)
        {
            for (var i = 0; i < BagSize; i++)
            {
                buffBag[i] = new Item();
                buffEnabled[i] = true;
                bannerBag[i] = new Item();
                bannerEnabled[i] = true;
            }
            for (var i = 0; i < ExtraAccessoryCount; i++) extraAccessories[i] = new Item();

            int.TryParse(IniAPI.ReadIni("TaiTools", "WindowX", "-1"), out windowX);
            int.TryParse(IniAPI.ReadIni("TaiTools", "WindowY", "-1"), out windowY);
        }

        public override bool RespondsWhileDisabled { get { return true; } }

        protected override void Toggle()
        {
            ToggleMenu();
        }

        public bool OnChatCommand(string command, string[] args)
        {
            if (command.Equals("menu", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 0)
                    Main.NewText("用法：/menu");
                else
                    ToggleMenu();
                return true;
            }

            if (command.Equals("travel", StringComparison.OrdinalIgnoreCase))
            {
                TravelCommand(args);
                return true;
            }

            if (command.Equals("autofish", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 0)
                {
                    Main.NewText("用法：/autofish");
                    return true;
                }

                if (autoFishing)
                    SetAutoFishing(false, false);
                else
                {
                    fishingTarget = Main.MouseWorld;
                    SetAutoFishing(true, false);
                }
                return true;
            }

            return false;
        }

        public void OnDrawUI()
        {
            if (!menuOpen || Main.gameMenu) return;

            EnsureBagLoaded(Main.LocalPlayer);
            Gui.BeginFrame();

            const int width = 410;
            const int height = 560;
            var x = windowX < 0 ? Math.Max(10, (Main.screenWidth - width) / 2) : Clamp(windowX, 0, Math.Max(0, Main.screenWidth - width));
            var y = windowY < 0 ? Math.Max(10, (Main.screenHeight - height) / 2) : Clamp(windowY, 0, Math.Max(0, Main.screenHeight - height));

            HandleWindowDrag(ref x, ref y, width, height);
            var window = new Rectangle(x, y, width, height);

            Gui.Panel(window, Gui.PanelBack);
            Gui.Hover(window);
            Gui.Text("泰拉工具箱", new Vector2(x + 18, y + 14), Gui.TextHot, 1f);

            if (Gui.Button(new Rectangle(x + width - 42, y + 10, 30, 28), Gui.Close))
                ToggleMenu();

            if (Gui.Button(new Rectangle(x + 18, y + 52, 88, 34), "工具")) activeTab = 0;
            if (Gui.Button(new Rectangle(x + 113, y + 52, 88, 34), "Buff")) activeTab = 1;
            if (Gui.Button(new Rectangle(x + 208, y + 52, 88, 34), "旗帜")) activeTab = 2;
            if (Gui.Button(new Rectangle(x + 303, y + 52, 88, 34), "饰品 +3")) activeTab = 3;

            if (activeTab == 1) DrawBuffBag(x, y);
            else if (activeTab == 2) DrawBannerBag(x, y);
            else if (activeTab == 3) DrawExtraAccessories(x, y);
            else DrawTools(x, y);

            Gui.EndFrame();
        }

        private void ToggleMenu()
        {
            if (Main.gameMenu) return;

            menuOpen = !menuOpen;
            draggingWindow = false;
            if (!menuOpen && capturingMenuKey)
            {
                capturingMenuKey = false;
                suppressCapturedKey = false;
                Main.blockInput = false;
            }
            Main.blockKey = ToggleKeySetting.Value.Key.ToString();

            if (menuOpen)
            {
                Main.playerInventory = false;
                Main.ClosePlayerChat();
                Main.chatText = "";
            }
        }

        private void HandleWindowDrag(ref int x, ref int y, int width, int height)
        {
            var titleBar = new Rectangle(x, y, width - 52, 46);
            var hovered = Gui.Hover(titleBar);

            if (!draggingWindow && hovered && Main.mouseLeft && Main.mouseLeftRelease)
            {
                draggingWindow = true;
                dragOffsetX = Main.mouseX - x;
                dragOffsetY = Main.mouseY - y;
                Main.mouseLeftRelease = false;
                Gui.PlayClick();
            }

            if (!draggingWindow) return;

            Main.LocalPlayer.mouseInterface = true;
            if (Main.mouseLeft)
            {
                windowX = Clamp(Main.mouseX - dragOffsetX, 0, Math.Max(0, Main.screenWidth - width));
                windowY = Clamp(Main.mouseY - dragOffsetY, 0, Math.Max(0, Main.screenHeight - height));
                x = windowX;
                y = windowY;
                return;
            }

            draggingWindow = false;
            IniAPI.WriteIni("TaiTools", "WindowX", windowX.ToString());
            IniAPI.WriteIni("TaiTools", "WindowY", windowY.ToString());
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(value, maximum));
        }

        private void DrawTools(int x, int y)
        {
            var rowY = y + 110;

            DrawToggleRow(x, ref rowY, "连锁挖矿", VeinMiningEnabled.Value, delegate
            {
                VeinMiningEnabled.Value = !VeinMiningEnabled.Value;
                if (!VeinMiningEnabled.Value) StopVein();
            });

            DrawToggleRow(x, ref rowY, "自动钓鱼", autoFishing, delegate
            {
                if (autoFishing)
                    SetAutoFishing(false, false);
                else
                {
                    choosingFishingTarget = true;
                    menuOpen = false;
                    Main.NewText("请在水面目标位置单击左键；按 Esc 取消。", 140, 230, 140);
                }
            });

            DrawToggleRow(x, ref rowY, "Boss 召唤物不消耗", BossSummonsNotConsumed.Value, delegate
            {
                BossSummonsNotConsumed.Value = !BossSummonsNotConsumed.Value;
                BossSummonsNotConsumed.Save();
            });

            DrawToggleRow(x, ref rowY, "三个额外饰品栏", ExtraAccessoriesEnabled.Value, delegate
            {
                ExtraAccessoriesEnabled.Value = !ExtraAccessoriesEnabled.Value;
                ExtraAccessoriesEnabled.Save();
            });

            var keyRow = new Rectangle(x + 26, rowY, 358, 40);
            Gui.Fill(keyRow, Gui.PanelInner);
            Gui.TextLeftCentered("菜单快捷键", new Rectangle(keyRow.X + 12, keyRow.Y, 160, keyRow.Height), Gui.TextNormal);
            var keyLabel = capturingMenuKey ? "请按新快捷键（Esc 取消）" : ToggleKeySetting.Value.ToBinding();
            if (Gui.Button(new Rectangle(keyRow.X + 174, keyRow.Y + 5, 174, 30), keyLabel))
            {
                capturingMenuKey = true;
                suppressCapturedKey = false;
                Main.blockInput = true;
            }
            rowY += 54;

            if (Gui.Button(new Rectangle(x + 26, rowY, 170, 38), "立即召来旅商"))
                SpawnTravelMerchant();
            if (Gui.Button(new Rectangle(x + 214, rowY, 170, 38), "让旅商离开"))
                RemoveTravelMerchant();
            rowY += 56;

            Gui.Text("自动钓鱼会正常消耗鱼饵。选择钓点后会自动抛竿、收杆。",
                new Vector2(x + 26, rowY), Gui.TextDim, 0.72f);
            rowY += 30;
            Gui.Text("命令仍可用：/travel、/travel leave、/autofish",
                new Vector2(x + 26, rowY), Gui.TextDim, 0.72f);
        }

        private static void DrawToggleRow(int x, ref int y, string label, bool value, Action toggle)
        {
            var area = new Rectangle(x + 26, y, 358, 40);
            Gui.Fill(area, Gui.PanelInner);
            Gui.TextLeftCentered(label, new Rectangle(area.X + 12, area.Y, 240, area.Height), Gui.TextNormal);
            var tick = new Rectangle(area.Right - 42, area.Y + 7, 26, 26);
            Gui.Tick(tick, value);
            if (Gui.Click(area)) toggle();
            y += 54;
        }

        private void DrawBuffBag(int x, int y)
        {
            var pageStart = bagPage * SlotsPerPage;
            Gui.Text("100 格永久 Buff 背包", new Vector2(x + 24, y + 101), Gui.TextNormal, 0.8f);
            Gui.TextRight("第 " + (bagPage + 1) + " / 4 页", x + 386, y + 101, Gui.TextDim, 0.8f);

            var oldScale = Main.inventoryScale;
            Main.inventoryScale = 0.82f;

            try
            {
                for (var local = 0; local < SlotsPerPage; local++)
                {
                    var index = pageStart + local;
                    var column = local % 5;
                    var row = local / 5;
                    var position = new Vector2(x + 76 + column * 52, y + 132 + row * 52);
                    var area = new Rectangle((int)position.X, (int)position.Y, 44, 44);

                    // The array overload registers an original-inventory gamepad link from the slot index.
                    // Our virtual bag has indices above that table, so page two and later used to throw here.
                    // The ref overload draws the same item without registering a vanilla inventory slot.
                    ItemSlot.Draw(Main.spriteBatch, ref buffBag[index], SlotContext, position, Color.White);

                    if (!buffEnabled[index])
                    {
                        Gui.Fill(area, new Color(25, 25, 25, 175));
                        Gui.Border(area, Gui.TextBad);
                    }
                    else if (!buffBag[index].IsAir)
                    {
                        Gui.Border(area, Gui.TextGood);
                    }

                    if (!Gui.Hover(area)) continue;

                    ItemSlot.MouseHover(buffBag[index], SlotContext);

                    if (!Main.mouseLeft || !Main.mouseLeftRelease) continue;

                    var alt = Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
                    Main.mouseLeftRelease = false;
                    Gui.PlayClick();

                    if (alt)
                    {
                        buffEnabled[index] = !buffEnabled[index];
                        SaveBagSlot(index);
                        continue;
                    }

                    if (!Main.mouseItem.IsAir && !IsBuffItem(Main.mouseItem))
                    {
                        Main.NewText("这个物品没有可由 Buff 背包提供的增益效果。", 230, 130, 130);
                        continue;
                    }

                    SwapMouseItem(index);
                    SaveBagSlot(index);
                }
            }
            finally
            {
                Main.inventoryScale = oldScale;
            }

            if (Gui.Button(new Rectangle(x + 74, y + 398, 104, 32), "上一页", bagPage > 0))
                bagPage--;
            if (Gui.Button(new Rectangle(x + 232, y + 398, 104, 32), "下一页", bagPage < 3))
                bagPage++;

            Gui.Text("普通左键存取；Alt + 左键切换该格生效/停用。红框表示停用。",
                new Vector2(x + 24, y + 435), Gui.TextWarn, 0.68f);
        }

        private void DrawBannerBag(int x, int y)
        {
            var pageStart = bannerPage * SlotsPerPage;
            Gui.Text("100 格旗帜背包", new Vector2(x + 24, y + 101), Gui.TextNormal, 0.8f);
            Gui.TextRight("第 " + (bannerPage + 1) + " / 4 页", x + 386, y + 101, Gui.TextDim, 0.8f);

            var oldScale = Main.inventoryScale;
            Main.inventoryScale = 0.82f;
            try
            {
                for (var local = 0; local < SlotsPerPage; local++)
                {
                    var index = pageStart + local;
                    var column = local % 5;
                    var row = local / 5;
                    var position = new Vector2(x + 76 + column * 52, y + 132 + row * 52);
                    var area = new Rectangle((int)position.X, (int)position.Y, 44, 44);
                    ItemSlot.Draw(Main.spriteBatch, ref bannerBag[index], SlotContext, position, Color.White);

                    if (!bannerEnabled[index])
                    {
                        Gui.Fill(area, new Color(25, 25, 25, 175));
                        Gui.Border(area, Gui.TextBad);
                    }
                    else if (!bannerBag[index].IsAir) Gui.Border(area, Gui.TextGood);

                    if (!Gui.Hover(area)) continue;
                    ItemSlot.MouseHover(bannerBag[index], SlotContext);
                    if (!Main.mouseLeft || !Main.mouseLeftRelease) continue;

                    var alt = Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
                    Main.mouseLeftRelease = false;
                    Gui.PlayClick();
                    if (alt)
                    {
                        bannerEnabled[index] = !bannerEnabled[index];
                        SaveBannerSlot(index);
                        continue;
                    }
                    if (!Main.mouseItem.IsAir && !IsBannerItem(Main.mouseItem))
                    {
                        Main.NewText("这里只能放敌怪旗帜。", 230, 130, 130);
                        continue;
                    }
                    SwapMouseItem(bannerBag, index);
                    SaveBannerSlot(index);
                }
            }
            finally { Main.inventoryScale = oldScale; }

            if (Gui.Button(new Rectangle(x + 74, y + 398, 104, 32), "上一页", bannerPage > 0)) bannerPage--;
            if (Gui.Button(new Rectangle(x + 232, y + 398, 104, 32), "下一页", bannerPage < 3)) bannerPage++;
            Gui.Text("旗帜放入后全程提供对应敌怪的攻防加成；Alt + 左键可停用。",
                new Vector2(x + 24, y + 435), Gui.TextWarn, 0.68f);
        }

        private void DrawExtraAccessories(int x, int y)
        {
            Gui.Text("所有模式固定额外 +3 饰品栏", new Vector2(x + 24, y + 106), Gui.TextNormal, 0.82f);
            Gui.Text("饰品属性与特殊效果使用原版结算；每个角色独立保存。",
                new Vector2(x + 24, y + 136), Gui.TextDim, 0.72f);

            var oldScale = Main.inventoryScale;
            Main.inventoryScale = 1f;
            try
            {
                for (var i = 0; i < ExtraAccessoryCount; i++)
                {
                    var position = new Vector2(x + 97 + i * 76, y + 190);
                    var area = new Rectangle((int)position.X, (int)position.Y, 52, 52);
                    ItemSlot.Draw(Main.spriteBatch, ref extraAccessories[i], SlotContext, position, Color.White);
                    if (!extraAccessories[i].IsAir) Gui.Border(area, ExtraAccessoriesEnabled.Value ? Gui.TextGood : Gui.TextBad);
                    if (!Gui.Hover(area)) continue;
                    ItemSlot.MouseHover(extraAccessories[i], SlotContext);
                    if (!Main.mouseLeft || !Main.mouseLeftRelease) continue;
                    Main.mouseLeftRelease = false;
                    Gui.PlayClick();
                    if (!Main.mouseItem.IsAir && !Main.mouseItem.accessory)
                    {
                        Main.NewText("这里只能放饰品。", 230, 130, 130);
                        continue;
                    }
                    SwapMouseItem(extraAccessories, i);
                    SaveAccessorySlot(i);
                }
            }
            finally { Main.inventoryScale = oldScale; }

            Gui.Text(ExtraAccessoriesEnabled.Value ? "当前已启用。可在“工具”页临时关闭。" : "当前已停用，物品仍会安全保存在格子中。",
                new Vector2(x + 24, y + 278), ExtraAccessoriesEnabled.Value ? Gui.TextGood : Gui.TextWarn, 0.75f);
        }

        private void SwapMouseItem(int index)
        {
            SwapMouseItem(buffBag, index);
        }

        private static void SwapMouseItem(Item[] items, int index)
        {
            var slot = items[index];
            var mouse = Main.mouseItem;

            if (!slot.IsAir && !mouse.IsAir && slot.type == mouse.type && slot.prefix == mouse.prefix && slot.stack < slot.maxStack)
            {
                var moved = Math.Min(mouse.stack, slot.maxStack - slot.stack);
                slot.stack += moved;
                mouse.stack -= moved;
                if (mouse.stack <= 0) mouse.TurnToAir();
                return;
            }

            items[index] = mouse;
            Main.mouseItem = slot;

            if (items[index] == null) items[index] = new Item();
            if (Main.mouseItem == null) Main.mouseItem = new Item();
        }

        public void OnPlayerUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || Main.gameMenu) return;

            UpdateMenuKeyCapture();
            EnsureBagLoaded(player);
            ApplyBagBuffs(player);
            ApplyBannerBuffs();

            if (choosingFishingTarget)
            {
                if (Main.keyState.IsKeyDown(Keys.Escape))
                {
                    choosingFishingTarget = false;
                    Main.NewText("已取消选择钓点。", 180, 180, 180);
                }
                else if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    fishingTarget = Main.MouseWorld;
                    Main.mouseLeftRelease = false;
                    choosingFishingTarget = false;
                    SetAutoFishing(true, false);
                }
            }

            UpdateAutoFishing(player);
        }

        public void OnPlayerPreUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer || Main.gameMenu) return;
            ApplyBossSummonConsumption(player);
        }

        public void OnPlayerUpdateArmorSets(Player player)
        {
            if (player.whoAmI != Main.myPlayer || applyingSocialArmorSet) return;
            if (!((player.armor[10] == null || player.armor[10].IsAir) &&
                (player.armor[11] == null || player.armor[11].IsAir) &&
                (player.armor[12] == null || player.armor[12].IsAir)))
            {
                applyingSocialArmorSet = true;
                try
                {
                    // In 1.4.5.8 UpdateArmorSets(int) no longer uses its argument: ArmorSetBonuses.QueryContext
                    // always reads armor[0..2]. Temporarily expose the social armor there while vanilla checks it.
                    for (var i = 0; i < 3; i++)
                    {
                        var equipped = player.armor[i];
                        player.armor[i] = player.armor[i + 10];
                        player.armor[i + 10] = equipped;
                    }
                    player.UpdateArmorSets(0);
                }
                finally
                {
                    for (var i = 0; i < 3; i++)
                    {
                        var social = player.armor[i];
                        player.armor[i] = player.armor[i + 10];
                        player.armor[i + 10] = social;
                    }
                    applyingSocialArmorSet = false;
                }
            }

            if (!ExtraAccessoriesEnabled.Value) return;
            EnsureBagLoaded(player);
            for (var i = 0; i < ExtraAccessoryCount; i++)
            {
                var item = extraAccessories[i];
                if (item != null && !item.IsAir && item.accessory)
                    player.ApplyEquipFunctional(3 + i, item);
            }
        }

        private void UpdateMenuKeyCapture()
        {
            if (suppressCapturedKey)
            {
                Main.blockInput = true;
                if (Main.keyState.GetPressedKeys().Length == 0)
                {
                    suppressCapturedKey = false;
                    Main.blockInput = false;
                }
                return;
            }

            if (!capturingMenuKey) return;

            Main.blockInput = true;
            var pressed = Main.keyState.GetPressedKeys();
            if (pressed.Length == 0) return;

            if (Array.IndexOf(pressed, Keys.Escape) >= 0)
            {
                capturingMenuKey = false;
                suppressCapturedKey = true;
                Main.NewText("已取消修改菜单快捷键。", 180, 180, 180);
                return;
            }

            var key = Keys.None;
            for (var i = 0; i < pressed.Length; i++)
            {
                if (pressed[i] == Keys.LeftControl || pressed[i] == Keys.RightControl ||
                    pressed[i] == Keys.LeftShift || pressed[i] == Keys.RightShift ||
                    pressed[i] == Keys.LeftAlt || pressed[i] == Keys.RightAlt) continue;

                key = pressed[i];
                break;
            }

            if (key == Keys.None) return;

            var hotkey = ToggleKeySetting.Value;
            hotkey.Key = key;
            hotkey.Control = Array.IndexOf(pressed, Keys.LeftControl) >= 0 || Array.IndexOf(pressed, Keys.RightControl) >= 0;
            hotkey.Shift = Array.IndexOf(pressed, Keys.LeftShift) >= 0 || Array.IndexOf(pressed, Keys.RightShift) >= 0;
            hotkey.Alt = Array.IndexOf(pressed, Keys.LeftAlt) >= 0 || Array.IndexOf(pressed, Keys.RightAlt) >= 0;
            ToggleKeySetting.Save();

            capturingMenuKey = false;
            suppressCapturedKey = true;
            Main.blockKey = key.ToString();

            var conflict = Loader.HasGameConflicts(hotkey);
            Main.NewText("菜单快捷键已改为 " + hotkey.ToBinding() + (conflict ? "（与游戏按键冲突）" : "。"),
                conflict ? (byte)240 : (byte)140, conflict ? (byte)205 : (byte)230, conflict ? (byte)120 : (byte)140);
        }

        private void ApplyBagBuffs(Player player)
        {
            var hasGnome = false;
            for (var i = 0; i < BagSize; i++)
            {
                var item = buffBag[i];
                if (!buffEnabled[i] || item == null || item.IsAir) continue;

                if (item.type == ItemID.GardenGnome)
                {
                    hasGnome = true;
                    continue;
                }

                int buffType;
                if (!TryGetBuff(item, out buffType)) continue;

                var current = player.FindBuffIndex(buffType);
                if (current < 0)
                    player.AddBuff(buffType, 2);
                else if (player.buffTime[current] < 2)
                    player.buffTime[current] = 2;
            }

            // Update() has just recalculated luck, so adding once here behaves like one nearby garden gnome.
            if (hasGnome) player.luck += 0.2f;
        }

        private void ApplyBannerBuffs()
        {
            if (Main.SceneMetrics == null || Main.SceneMetrics.NPCBannerBuff == null) return;
            for (var i = 0; i < BagSize; i++)
            {
                var item = bannerBag[i];
                if (!bannerEnabled[i] || item == null || item.IsAir || !IsBannerItem(item)) continue;
                var npcType = BannerSystem.BannerToNPC(item.placeStyle);
                if (npcType >= 0 && npcType < Main.SceneMetrics.NPCBannerBuff.Length)
                {
                    Main.SceneMetrics.NPCBannerBuff[npcType] = true;
                    Main.SceneMetrics.hasBanner = true;
                }
            }
        }

        private static void ApplyBossSummonConsumption(Player player)
        {
            var keep = !BossSummonsNotConsumed.Value;
            for (var i = 0; i < player.inventory.Length; i++)
            {
                var item = player.inventory[i];
                if (item != null && !item.IsAir && BossSummonItems.Contains(item.type)) item.consumable = keep;
            }
            if (Main.mouseItem != null && !Main.mouseItem.IsAir && BossSummonItems.Contains(Main.mouseItem.type))
                Main.mouseItem.consumable = keep;
        }

        private static bool IsBuffItem(Item item)
        {
            int ignored;
            return item != null && !item.IsAir &&
                (item.type == ItemID.GardenGnome || TryGetBuff(item, out ignored));
        }

        private static bool TryGetBuff(Item item, out int buffType)
        {
            if (StationBuffs.TryGetValue(item.type, out buffType)) return true;
            if (item.type >= 0 && item.type < ItemID.Sets.IsAKite.Length && ItemID.Sets.IsAKite[item.type])
            {
                buffType = BuffID.Kite;
                return true;
            }
            if (item.type >= 0 && item.type < ItemID.Sets.Campfires.Length && ItemID.Sets.Campfires[item.type])
            {
                buffType = BuffID.Campfire;
                return true;
            }

            buffType = item.buffType;
            return buffType > 0 && buffType < Main.debuff.Length && !Main.debuff[buffType];
        }

        private static bool IsBannerItem(Item item)
        {
            return item != null && !item.IsAir && item.createTile == TileID.Banners &&
                BannerSystem.BannerToNPC(item.placeStyle) >= 0;
        }

        private void EnsureBagLoaded(Player player)
        {
            if (player == null || !player.active || string.IsNullOrEmpty(player.name) || player.name == loadedPlayer) return;

            loadedPlayer = player.name;
            var section = BagSection(player.name);
            var bannerSection = BannerSection(player.name);
            var accessorySection = AccessorySection(player.name);

            for (var i = 0; i < BagSize; i++)
            {
                buffBag[i] = new Item();
                buffEnabled[i] = true;
                bannerBag[i] = new Item();
                bannerEnabled[i] = true;

                var value = IniAPI.ReadIni(section, "Slot" + i, "", 256);
                Item item;
                bool active;
                if (TryLoadSavedItem(value, out item, out active) && IsBuffItem(item))
                {
                    buffBag[i] = item;
                    buffEnabled[i] = active;
                }

                value = IniAPI.ReadIni(bannerSection, "Slot" + i, "", 256);
                if (TryLoadSavedItem(value, out item, out active) && IsBannerItem(item))
                {
                    bannerBag[i] = item;
                    bannerEnabled[i] = active;
                }
            }

            for (var i = 0; i < ExtraAccessoryCount; i++)
            {
                extraAccessories[i] = new Item();
                Item item;
                bool ignored;
                var value = IniAPI.ReadIni(accessorySection, "Slot" + i, "", 256);
                if (TryLoadSavedItem(value, out item, out ignored) && item.accessory) extraAccessories[i] = item;
            }
        }

        private static bool TryLoadSavedItem(string value, out Item item, out bool active)
        {
            item = new Item();
            active = true;
            if (string.IsNullOrEmpty(value)) return false;

            var parts = value.Split(',');
            int type;
            int stack;
            int prefix;
            if (parts.Length != 4 || !int.TryParse(parts[0], out type) || !int.TryParse(parts[1], out stack) ||
                !int.TryParse(parts[2], out prefix) || !bool.TryParse(parts[3], out active) || type <= 0)
                return false;

            item.netDefaults(type);
            item.stack = Math.Max(1, Math.Min(stack, item.maxStack));
            if (prefix > 0) item.Prefix(prefix);
            return true;
        }

        private void SaveBagSlot(int index)
        {
            if (string.IsNullOrEmpty(loadedPlayer)) return;

            var item = buffBag[index];
            var value = item == null || item.IsAir
                ? ""
                : item.type + "," + item.stack + "," + item.prefix + "," + buffEnabled[index];

            IniAPI.WriteIni(BagSection(loadedPlayer), "Slot" + index, value);
        }

        private void SaveBannerSlot(int index)
        {
            if (string.IsNullOrEmpty(loadedPlayer)) return;
            var item = bannerBag[index];
            var value = item == null || item.IsAir ? "" :
                item.type + "," + item.stack + "," + item.prefix + "," + bannerEnabled[index];
            IniAPI.WriteIni(BannerSection(loadedPlayer), "Slot" + index, value);
        }

        private void SaveAccessorySlot(int index)
        {
            if (string.IsNullOrEmpty(loadedPlayer)) return;
            var item = extraAccessories[index];
            var value = item == null || item.IsAir ? "" :
                item.type + "," + item.stack + "," + item.prefix + ",True";
            IniAPI.WriteIni(AccessorySection(loadedPlayer), "Slot" + index, value);
        }

        private static string BagSection(string playerName)
        {
            return "TaiTools.BuffBag." + EncodePlayerName(playerName);
        }

        private static string BannerSection(string playerName)
        {
            return "TaiTools.BannerBag." + EncodePlayerName(playerName);
        }

        private static string AccessorySection(string playerName)
        {
            return "TaiTools.Accessories." + EncodePlayerName(playerName);
        }

        private static string EncodePlayerName(string playerName)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(playerName))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private void TravelCommand(string[] args)
        {
            if (args.Length == 0)
                SpawnTravelMerchant();
            else if (args.Length == 1 && args[0].Equals("leave", StringComparison.OrdinalIgnoreCase))
                RemoveTravelMerchant();
            else
                Main.NewText("用法：/travel 或 /travel leave");
        }

        private static bool SinglePlayerOnly()
        {
            if (Main.netMode == 0) return true;
            Main.NewText("此功能只能在单人世界中使用。", 230, 130, 130);
            return false;
        }

        private static void SpawnTravelMerchant()
        {
            if (!SinglePlayerOnly()) return;
            if (NPC.AnyNPCs(NPCID.TravellingMerchant))
            {
                Main.NewText("旅商已经在世界里了。", 220, 205, 120);
                return;
            }

            WorldGen.SpawnTravelNPC();
            Main.NewText("正在召来旅商。", 140, 230, 140);
        }

        private static void RemoveTravelMerchant()
        {
            if (!SinglePlayerOnly()) return;
            if (!NPC.AnyNPCs(NPCID.TravellingMerchant))
            {
                Main.NewText("旅商当前不在世界里。", 220, 205, 120);
                return;
            }

            WorldGen.UnspawnTravelNPC();
            Main.NewText("旅商已经离开。", 180, 180, 180);
        }

        private void SetAutoFishing(bool value, bool keepTarget)
        {
            if (value && !SinglePlayerOnly()) return;

            autoFishing = value;
            hadBobber = false;
            warnedNoBait = false;
            recastDelay = 0;
            if (!value) choosingFishingTarget = false;

            Main.NewText(value ? "自动钓鱼已开启。" : "自动钓鱼已关闭。",
                value ? (byte)140 : (byte)180, value ? (byte)230 : (byte)180, value ? (byte)140 : (byte)180);
        }

        private void UpdateAutoFishing(Player player)
        {
            if (!autoFishing || choosingFishingTarget) return;

            if (Main.netMode != 0)
            {
                SetAutoFishing(false, false);
                return;
            }

            if (!player.active || player.dead || player.CCed || player.noItems) return;

            var held = player.inventory[player.selectedItem];
            if (held == null || held.IsAir || held.fishingPole <= 0)
            {
                hadBobber = false;
                recastDelay = 0;
                return;
            }

            var hasBobber = false;
            var hasBite = false;

            for (var i = 0; i < Main.maxProjectiles; i++)
            {
                var projectile = Main.projectile[i];
                if (projectile == null || !projectile.active || !projectile.bobber || projectile.owner != player.whoAmI)
                    continue;

                hasBobber = true;
                if (projectile.ai[0] < 1f && projectile.ai[1] < 0f) hasBite = true;
            }

            if (hasBobber)
            {
                hadBobber = true;
                if (hasBite)
                {
                    UseFishingPole(player);
                    recastDelay = 30;
                }
                return;
            }

            if (hadBobber)
            {
                hadBobber = false;
                recastDelay = 30;
            }

            if (recastDelay > 0)
            {
                recastDelay--;
                return;
            }

            if (player.GetFishingConditions().BaitItemType <= 0)
            {
                if (!warnedNoBait)
                {
                    warnedNoBait = true;
                    Main.NewText("自动钓鱼正在等待鱼饵。", 220, 205, 120);
                }
                recastDelay = 60;
                return;
            }

            warnedNoBait = false;
            UseFishingPole(player);
            recastDelay = 15;
        }

        private void UseFishingPole(Player player)
        {
            var oldMouseX = Main.mouseX;
            var oldMouseY = Main.mouseY;
            var oldControlUseItem = player.controlUseItem;
            var oldReleaseUseItem = player.releaseUseItem;

            Main.mouseX = (int)(fishingTarget.X - Main.screenPosition.X);
            Main.mouseY = (int)(fishingTarget.Y - Main.screenPosition.Y);
            player.releaseUseItem = true;
            player.controlUseItem = true;
            player.ItemCheck();

            player.controlUseItem = oldControlUseItem;
            player.releaseUseItem = oldReleaseUseItem;
            Main.mouseX = oldMouseX;
            Main.mouseY = oldMouseY;
        }

        public void OnPlayerPickTile(Player player, int x, int y, int pickPower)
        {
            if (!VeinMiningEnabled.Value || player.whoAmI != Main.myPlayer) return;
            if (veinQueue.Count > 0 || swungAtType >= 0 || !WorldGen.InWorld(x, y, 1)) return;

            var tile = Main.tile[x, y];
            if (tile == null || !tile.active() || !VeinTiles.Value.Contains(tile.type)) return;

            swungAt = new Point(x, y);
            swungAtType = tile.type;
            swungAtPickPower = pickPower;
        }

        public void OnUpdate()
        {
            var player = Player;

            if (!VeinMiningEnabled.Value || Main.gameMenu || player == null || !player.active || player.dead)
            {
                StopVein();
                return;
            }

            if (veinQueue.Count > 0)
            {
                MineQueued(player);
                return;
            }

            if (swungAtType < 0) return;

            var from = swungAt;
            var type = swungAtType;
            var pickPower = swungAtPickPower;
            swungAtType = -1;

            var tile = Main.tile[from.X, from.Y];
            if (tile != null && tile.active() && tile.type == type) return;

            veinType = type;
            veinPickPower = pickPower;
            veinMined = 0;
            owedMana = 0f;
            veinSeen.Clear();
            veinQueue.Clear();
            Spread(player, from.X, from.Y);

            if (veinQueue.Count > 0) MineQueued(player);
        }

        private void MineQueued(Player player)
        {
            var budget = Main.netMode == 0 ? BlocksPerTickInSinglePlayer : BlocksPerTickInMultiplayer;

            while (budget-- > 0 && veinQueue.Count > 0 && veinMined < MaxBlocks.Value)
            {
                var point = veinQueue.Dequeue();
                var tile = Main.tile[point.X, point.Y];
                if (tile == null || !tile.active() || tile.type != veinType) continue;

                if (!PayMana(player))
                {
                    StopVein();
                    return;
                }

                WorldGen.KillTile(point.X, point.Y, false, false, Main.netMode == 1);
                if (Main.netMode != 0)
                    NetMessage.SendData(17, -1, -1, null, 0, point.X, point.Y, 0f, 0, 0, 0);

                veinMined++;
                Spread(player, point.X, point.Y);
            }

            if (veinQueue.Count == 0 || veinMined >= MaxBlocks.Value) StopVein();
        }

        private bool PayMana(Player player)
        {
            if (!RequireMana.Value) return true;

            owedMana += ManaCostPerBlock / (veinPickPower > 0 ? veinPickPower : 1);
            var due = (int)owedMana;
            if (due < 1) return true;
            if (!player.CheckMana(due, true)) return false;

            owedMana -= due;
            return true;
        }

        private void Spread(Player player, int x, int y)
        {
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Enqueue(player, x + dx, y + dy);
            }
        }

        private void Enqueue(Player player, int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1) || !veinSeen.Add(y * Main.maxTilesX + x)) return;

            var tile = Main.tile[x, y];
            if (tile == null || !tile.active() || tile.type != veinType) return;

            if (RangeLimit.Value &&
                !player.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple,
                    player.inventory[player.selectedItem].tileBoost)) return;

            veinQueue.Enqueue(new Point(x, y));
        }

        private void StopVein()
        {
            swungAtType = -1;
            veinQueue.Clear();
            veinSeen.Clear();
            veinType = -1;
            veinPickPower = 0;
            veinMined = 0;
            owedMana = 0f;
        }
    }
}
