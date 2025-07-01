using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DiscordRPC;

namespace racman
{
    public class RaC1Addresses : IAddresses
    {
        // Current bolt count
        public uint boltCount => 0x969CA0;

        // Ratchet's coordinates
        public uint playerCoords => 0x969D60;

        // The player's current health.
        public uint playerHealth => 0x96BF88;

        // Controller inputs mask address
        public uint inputOffset => 0x964AF0;

        // Controller analog sticks address
        public uint analogOffset => 0x964A40;

        // First 0x4 for if planet should be loaded, the 0x4 after for planet to load.
        public uint loadPlanet => 0xA10700;

        // Currently loaded planet.
        public uint currentPlanet => 0x969C70;

        // Main level flags
        public uint levelFlags => 0xA0CA84;

        // Other level flags
        public uint miscLevelFlags => 0xA0CD1C;

        // Array of infobots collected
        public uint infobotFlags => 0x96CA0C;

        // Movies that have been watched
        public uint moviesFlags => 0x96BFF0;

        // Array of unlocked unlockables like gadgets, weapons and other items
        public uint unlockArray => 0x96C140;

        // Planet we're going to.
        public uint destinationPlanet => 0xa10704;

        // Current player state. 
        public uint playerState => 0x96BD64;

        // Count of frames in current level
        public uint planetFrameCount => 0xA10710;

        // Current game state, like currently playing, in menu, in ILM, etc.
        public uint gameState => 0x00A10708;

        // Which loading screen type you're current at, or the last loading screen you got in last load
        public uint loadingScreenID => 0x9645C8;

        //Frames until "Ghost Ratchet" runs out.
        public uint ghostTimer => 0x969EAC;

        // Set single byte to enable/disable drek skip.
        public uint drekSkip => 0xFACC7B;

        // Set single byte to enable/disable goodies menu. Not related to challenge mode.
        public uint goodiesMenu => 0x969CD3;

        // Array of whether or not you've unlocked any gold weapons.
        public uint goldItems => 0x969CA8;

        // Force third loading screen by setting to 2. Not to be confused with instant loads
        public uint fastLoad => 0x9645CF;

        // Array of whether or not you've collected gold bolts. 4 per planet.
        public uint goldBolts => 0xA0CA34;

        public uint debugUpdateOptions => 0x95c5c8;

        public uint debugModeControl => 0x95c5d4;

        // Values corresponding to the location of the internal table for game objects.
        public uint mobyInstances => 0x0A390A4;
        public uint mobyInstancesEnd => 0x0A390A8;
    }

    public class rac1 : IGame, IAutosplitterAvailable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vec4
        {
            public float x;
            public float y;
            public float z;
            public float w;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct GamePtr
        {
            public uint addr;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct Moby
        {
            // /* 000 */ Vec4 bSphere; // bounding sphere (x,y,z,r)
            public Vec4 bSphere;

            // /* 010 */ Vec4 position; // world position
            public Vec4 position;

            // /* 020 */ int8_t state; // FF free, FE aged-out, else active
            public sbyte state;

            // /* 021 */ uint8_t group; // collision group (init FF)
            public byte group;

            // /* 022 */ uint8_t classByte; // table key from byte_A354C0
            public byte classByte;

            // /* 023 */ uint8_t renderLayer; // always 0x80 on ctor
            public byte renderLayer;

            // /* 024 */ GamePointer pTemplate; // MobyDef / blueprint
            public GamePtr pTemplate;

            // /* 028 */ GamePointer pNext; // allocator free-list link
            public GamePtr pNext;

            // /* 02C */ float size; // default scale from template
            public float size;

            // /* 030 */ uint8_t updateDistIdx; // update-cull bucket
            public byte updateDistIdx;

            // /* 031 */ uint8_t drawn; // set by renderer
            public byte drawn;

            // /* 032 */ uint16_t drawDist; // draw-cull radius
            public ushort drawDist;

            // /* 034 */ uint16_t flags; // modeBits1
            public ushort flags;

            // /* 036 */ uint16_t flags2; // modeBits2 (init 0x7F80)
            public ushort flags2;

            // /* 038 */ uint64_t timeStamp; // 0x40404000000000 on spawn
            public ulong timeStamp;

            // /* 040 */ uint8_t scratch40[0x10]; // unknown anim/temp
            public byte field15_0x40;
            public byte field16_0x41;
            public byte field17_0x42;
            public byte cur_animation_seq;
            public byte field19_0x44;
            public byte field20_0x45;
            public byte field21_0x46;
            public byte field22_0x47;
            public float field23_0x48;
            public float field24_0x4c;

            // /* 050 */ uint8_t modelIdx; // mesh set index
            public byte modelIdx;

            // /* 051 */ uint8_t collIdx; // collision set index
            public byte collIdx;

            // /* 052 */ uint8_t vfxIdx; // VFX table index
            public byte vfxIdx;

            // /* 053 */ uint8_t sfxIdx; // SFX table index
            public byte sfxIdx;

            // /* 054 */ float scratchF0; // ctor writes 1.0f
            public float scratchF0;

            // /* 058 */ float scratchF1; //   "
            public float scratchF1;

            // /* 05C */ float scratchF2; //   "
            public float scratchF2;

            // /* 060 */ uint8_t scratch60[0x08]; // more temp bytes
            public byte field32_0x60;
            public byte field33_0x61;
            public byte field34_0x62;
            public byte field35_0x63;
            public GamePtr pUpdate;

            // /* 068 */ GamePointer pCurAnimData; // set by sub_EF74C
            public GamePtr pCurAnimData;

            // /* 06C */ GamePointer pPrevAnimData; // "
            public GamePtr pPrevAnimData;

            // /* 070 */ uint8_t _pad70;
            public byte _pad70;

            // /* 071 */ uint8_t lightOverride; // FF
            public byte lightOverride;

            // /* 072 */ uint8_t alphaOverride; // FF
            public byte alphaOverride;

            // /* 073 */ uint8_t _pad73;
            public byte _pad73;

            // /* 074 */ GamePointer pBehaviour; // update-fn table
            public GamePtr pBehaviour;

            // /* 078 */ GamePointer pVarBlock; // 0x80-byte per-moby vars
            public GamePtr pVarBlock;

            // /* 07C */ uint8_t animState1; // template+0x11
            public byte animState1;

            // /* 07D */ uint8_t animState2; // FF
            public byte animState2;

            // /* 07E */ uint8_t extraFlag; // template+0x12
            public byte extraFlag;

            // /* 07F */ uint8_t boneFlag; // 0x18 if dynamic-bones
            public byte boneFlag;

            // /* 080 */ uint8_t _pad80[0x04];
            public byte _pad80_1;
            public byte _pad80_2;
            public byte _pad80_3;
            public byte _pad80_4;

            // /* 084 */ GamePointer pIK1;
            public GamePtr pIK1;

            // /* 088 */ GamePointer pIK2;
            public GamePtr pIK2;

            // /* 08C */ uint8_t _pad8C[0x04];
            public byte _pad8C_1;
            public byte _pad8C_2;
            public byte _pad8C_3;
            public byte _pad8C_4;

            // /* 090 */ GamePointer pScript; // behaviour script ptr
            public GamePtr pScript;

            // /* 094 */ GamePointer pHomeWaypoint; // template+0x10
            public GamePtr pHomeWaypoint;

            // /* 098 */ uint8_t _pad98[0x08];
            public byte _pad98_1;
            public byte _pad98_2;
            public byte _pad98_3;
            public byte _pad98_4;
            public byte _pad98_5;
            public byte _pad98_6;
            public byte _pad98_7;
            public byte _pad98_8;

            // /* 0A0 */ uint8_t colourR; // 7F
            public byte colourR;

            // /* 0A1 */ uint8_t colourG; // 7F
            public byte colourG;

            // /* 0A2 */ uint8_t colourB; // 80
            public byte colourB;

            // /* 0A3 */ uint8_t colourA; // 80
            public byte colourA;

            // /* 0A4 */ uint8_t paletteIdx; // FF
            public byte paletteIdx;

            // /* 0A5 */ uint8_t _padA5;
            public byte _padA5;

            // /* 0A6 */ uint16_t classID; // oClass param
            public ushort classID;

            // /* 0A8 */ GamePointer debugPtr; // string-table thunk
            public GamePtr debugPtr;

            // /* 0AC */ GamePointer jitThunk; // generated code ptr
            public GamePtr jitThunk;

            // /* 0B0 */ uint8_t _padB0[0x0D];
            public byte _padB0_1;
            public byte _padB0_2;
            public byte _padB0_3;
            public byte _padB0_4;
            public byte _padB0_5;
            public byte _padB0_6;
            public byte _padB0_7;
            public byte _padB0_8;
            public byte _padB0_9;
            public byte _padB0_10;
            public byte _padB0_11;
            public byte _padB0_12;

            // /* 0BD */ uint8_t deformFlag; // set if template->flag_F
            public byte deformFlag;

            // /* 0BE */ uint8_t _padBE[0x32];
            public byte _padBE_1;
            public byte _padBE_2;
            public fixed byte rMtx[48]; // Rest of the padding combined with rMtx

            // /* 0F0 */ Vec4 rotation; // Euler/quat, zero on spawn
            public Vec4 rotation;

            public static unsafe Moby ByteArrayToMoby(byte[] bytes)
            {
                if (BitConverter.IsLittleEndian)
                {
                    var type = typeof(Moby);
                    foreach (var field in type.GetFields())
                    {
                        // Skip byte fields and fixed byte arrays
                        if (field.FieldType == typeof(byte) || 
                            (field.FieldType.IsArray && field.FieldType.GetElementType() == typeof(byte)))
                            continue;

                        // Get the offset of the field
                        var offset = Marshal.OffsetOf(type, field.Name).ToInt32();

                        if (field.FieldType == typeof(Vec4))
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                Array.Reverse(bytes, offset+(i*4), 4);
                            }
                        }

                        // Determine number of bytes to reverse based on field's type
                        int numBytesToReverse = 0;
                        if (field.FieldType == typeof(short) || field.FieldType == typeof(ushort))
                            numBytesToReverse = 2;
                        else if (field.FieldType == typeof(int) || field.FieldType == typeof(uint) ||
                                 field.FieldType == typeof(float) || field.FieldType == typeof(GamePtr))
                            numBytesToReverse = 4;
                        else if (field.FieldType == typeof(long) || field.FieldType == typeof(ulong) ||
                                 field.FieldType == typeof(double))
                            numBytesToReverse = 8;

                        // Reverse the bytes if needed
                        if (numBytesToReverse > 0)
                            Array.Reverse(bytes, offset, numBytesToReverse);
                    }
                }

                fixed (byte* ptr = &bytes[0])
                {
                    return (Moby)Marshal.PtrToStructure((IntPtr)ptr, typeof(Moby));
                }
            }
        };

        public enum DebugOption
        {
            UpdateRatchet, 
            UpdateMobys,
            UpdateParticles,
            UpdateCamera,
            NormalCamera,
            Freecam,
            FreecamCharacter
        }

        public static RaC1Addresses addr = new RaC1Addresses();

        public DiscordRpcClient DiscordClient;
        
        private Timestamps initialTimestamp;
        
        private uint lastPlanetIndex = 100;
        
        public void InitializeDiscordRPC()
        {
            if (DiscordClient != null)
            {
                DiscordClient.Dispose();
                DiscordClient = null;
            }
            DiscordClient = new DiscordRpcClient("1326847296025006110");
            DiscordClient.Initialize();
            initialTimestamp = Timestamps.Now;
        }

        public rac1(IPS3API api) : base(api)
        {
            this.planetsList = new string[] {
                "Veldin",
                "Novalis",
                "Aridia",
                "Kerwan",
                "Eudora",
                "Rilgar",
                "Blarg",
                "Umbris",
                "Batalia",
                "Gaspar",
                "Orxon",
                "Pokitaru",
                "Hoven",
                "Gemlik",
                "Oltanis",
                "Quartu",
                "Kalebo3",
                "Fleet",
                "Veldin2"
            };
        }

        public dynamic Unlocks = new
        {
            HeliPack = ("Heli-Pack", 2),
            ThrusterPack = ("Thruster-Pack", 3),
            HydroPack = ("Hydro-Pack", 4),
            SonicSummoner = ("Sonic Summoner", 5),
            O2Mask = ("O2 Mask", 6),
            PilotsHelmet = ("Pilots Helmet", 7),
            Wrench = ("Wrench", 8),
            SuckCannon = ("Suck Cannon", 9),
            BombGlove = ("Bomb Glove", 10),
            Devastator = ("Devastator", 11),
            Swingshot = ("Swingshot", 12),
            Visibomb = ("Visibomb", 13),
            Taunter = ("Taunter", 14),
            Blaster = ("Blaster", 15),
            Pyrocitor = ("Pyrocitor", 16),
            MineGlove = ("Mine Glove", 17),
            Walloper = ("Walloper", 18),
            TeslaClaw = ("Tesla Claw", 19),
            GloveOfDoom = ("Glove Of Doom", 20),
            MorphORay = ("Morph-O-Ray", 21),
            Hydrodisplacer = ("Hydrodisplacer", 22),
            RYNO = ("RYNO", 23),
            DroneDevice = ("Drone Device", 24),
            DecoyGlove = ("Decoy Glove", 25),
            Trespasser = ("Trespasser", 26),
            MetalDetector = ("Metal Detector", 27),
            Magneboots = ("Magneboots", 28),
            Grindboots = ("Grindboots", 29),
            Hoverboard = ("Hoverboard", 30),
            Hologuise = ("Hologuise", 31),
            PDA = ("PDA", 32),
            MapOMatic = ("Map-O-Matic", 33),
            BoltGrabber = ("Bolt Grabber", 34),
            Persuader = ("Persuader", 35)
        };

        private int ghostRatchetSubID = -1;

        /// <summary>
        /// Enables instant loads by overwriting code that starts loads somehow.
        /// </summary>
        /// <param name="toggle">if true, writes instant load code to the game, if false restores the original code</param>
        public override void SetFastLoads(bool toggle)
        {
            if (toggle)
            {
                api.WriteMemory(pid, 0x0DF254, 0x60000000);
                api.WriteMemory(pid, 0x165450, 0x2C03FFFF);
            }
            else
            {
                api.WriteMemory(pid, 0x0DF254, 0x40820188);
                api.WriteMemory(pid, 0x165450, 0x2c030000);
            }
        }

        /// <summary>
        /// Sets an unlockable item/gadget/weapon so that it's owned, or optionally unlocks it as gold instead.
        /// </summary>
        /// <param name="item">Item as tuple, needs item "id" in second tuple item, first item is string, but its value doesn't really matter.</param>
        /// <param name="unlocked">true if owned, false if not</param>
        /// <param name="gold">Whether to set item as golded (true) or to just unlock item (false)</param>
        public void SetUnlock((string, int) item, bool unlocked, bool gold = false)
        {
            api.WriteMemory(pid, (gold ? rac1.addr.goldItems : rac1.addr.unlockArray) + (uint)item.Item2, BitConverter.GetBytes(unlocked));
        }

        Dictionary<int, bool> ownedUnlocks = new Dictionary<int, bool>();
        Dictionary<int, bool> ownedGoldItems = new Dictionary<int, bool>();
        long lastUnlocksUpdate = 0;
        long lastGoldItemsUpdate = 0;

        public IEnumerable<(uint addr, uint size)> AutosplitterAddresses => new (uint, uint)[]
            {
                (addr.playerCoords, 8),
                (addr.destinationPlanet + 3, 1),
                (addr.currentPlanet + 3, 1),
                (addr.playerState+2, 2),
                (addr.planetFrameCount, 4),
                (addr.gameState, 4),
                (addr.loadingScreenID + 3, 1),
                (0x00aff000+3, 1),
                (0x00aff010+3, 1),
                (0x00aff020+3, 1),
                (0xa0ca75, 1),
                (0x00aff030+3, 1),
                (0x0096bff1, 2),
            };

        /// <summary>
        /// Updates internal list of unlocked items. There was a bug in the Ratchetron C# API that maked it unfeasibly slow to get each item as a single byte.
        /// </summary>
        private void UpdateUnlocks()
        {
            if (DateTime.Now.Ticks < lastUnlocksUpdate + 10000000)
            {
                return;
            }

            byte[] memory = api.ReadMemory(pid, rac1.addr.unlockArray, 40);

            var i = 0;
            foreach (byte item in memory)
            {
                ownedUnlocks[i] = item != 0;
                i++;
            }

            lastUnlocksUpdate = DateTime.Now.Ticks;
        }

        /// <summary>
        /// Updates internal list of golded items. There's a bug in Ratchetron or the Ratchetron C# API that makes it unfeasibly slow to get each item as a single byte.
        /// This function can be called as often as you'd like, but it only updates every second or so, as to not overload the Ratchetron API. No idea why the API is so fucky, this might be fixed in the future, who knows. 
        /// </summary>
        private void UpdateGoldItems()
        {
            if (DateTime.Now.Ticks < lastGoldItemsUpdate + 10000000)
            {
                return;
            }

            byte[] memory = api.ReadMemory(pid, rac1.addr.goldItems, 40);

            var i = 0;
            foreach (byte item in memory)
            {
                ownedGoldItems[i] = item != 0;
                i++;
            }

            lastGoldItemsUpdate = DateTime.Now.Ticks;
        }

        /// <summary>
        /// If you do or do not have an item unlocked or golded.
        /// </summary>
        /// <param name="item">Tuple<string, int> of item to check</param>
        /// <param name="gold">Whether or not to check for golded state</param>
        /// <returns></returns>
        public bool HasUnlock((string, int) item, bool gold = false)
        {
            UpdateGoldItems();
            UpdateUnlocks();
            return gold ? ownedGoldItems[item.Item2] : ownedUnlocks[item.Item2];
        }

        /// <summary>
        /// Get list of unlockables in the game
        /// </summary>
        /// <returns></returns>
        public (string, int)[] GetUnlocks()
        {
            List<(string, int)> unlocks = new List<(string, int)>();
           
            foreach (var item in Unlocks.GetType().GetProperties())
            {
                unlocks.Add(item.GetValue(Unlocks));
            }


            return unlocks.ToArray();
        }

        /// <summary>
        /// Resets level flag of destination planet
        /// </summary>
        public override void ResetLevelFlags()
        {

            // Not working properly right now?
            api.WriteMemory(pid, rac1.addr.levelFlags + (planetToLoad * 0x10), 0x10, new byte[0x10]);
            api.WriteMemory(pid, rac1.addr.miscLevelFlags + (planetToLoad * 0x100), 0x100, new byte[0x100]);
            api.WriteMemory(pid, rac1.addr.infobotFlags + planetToLoad, 1, new byte[1]);
            api.WriteMemory(pid, rac1.addr.moviesFlags, 0xc0, new byte[0xC0]);

            if (planetToLoad == 3)
            {
                api.WriteMemory(pid, 0x96C378, 0xF0, new byte[0xF0]);
                SetUnlock(Unlocks.HeliPack, false);
                SetUnlock(Unlocks.Swingshot, false);
            }

            if (planetToLoad == 4)
            {
                api.WriteMemory(pid, 0x96C468, 0x40, new byte[0x40]);
                SetUnlock(Unlocks.SuckCannon, false);
            }

            if (planetToLoad == 5)
            {
                api.WriteMemory(pid, 0x96C498, 0xa0, new byte[0xA0]);
            }

            if (planetToLoad == 6)
            {
                SetUnlock(Unlocks.Grindboots, false);
            }

            if (planetToLoad == 8)
            {
                api.WriteMemory(pid, 0x96C5A8, 0x40, new byte[0x40]);
            }

            if (planetToLoad == 9)
            {
                api.WriteMemory(pid, 0x96C5E8, 0x20, new byte[0x20]);
                SetUnlock(Unlocks.PilotsHelmet, false);
            }

            if (planetToLoad == 10)
            {
                SetUnlock(Unlocks.Magneboots, false);

                if (HasUnlock(Unlocks.O2Mask))
                {
                    // Figure it out
                    api.WriteMemory(pid, rac1.addr.infobotFlags + 11, 1);
                }
            }

            if (planetToLoad == 11)
            {
                SetUnlock(Unlocks.ThrusterPack, false);
                SetUnlock(Unlocks.O2Mask, false);
            }
        }


        public override void ResetGoldBolts(uint planetIndex)
        {
            api.WriteMemory(pid, rac1.addr.goldBolts + (planetIndex * 4), 0);
        }

        public void ResetAllGoldBolts()
        {
            api.WriteMemory(pid, rac1.addr.goldBolts, new byte[80]);
        }

        public void UnlockAllGoldBolts()
        {
            api.WriteMemory(pid, rac1.addr.goldBolts, Enumerable.Repeat((byte)1, 80).ToArray());
        }

        /// <summary>
        /// Whether or not goodies menu is enabled
        /// </summary>
        /// <returns>true if enabled false if not</returns>
        public bool GoodiesMenuEnabled()
        {
            return BitConverter.ToBoolean(api.ReadMemory(pid, rac1.addr.goodiesMenu, 1), 0);
        }

        /// <summary>
        /// Inifnite health is set by overwriting game code that deals health with nops.
        /// </summary>
        /// <param name="enabled">if true overwrites game code with nops, if false restores original game code</param>
        public void SetInfiniteHealth(bool enabled)
        {
            if (enabled)
            {
                api.WriteMemory(pid, 0x7F558, 4, new byte[] { 0x30, 0x64, 0x00, 0x00 });
            }
            else
            {
                api.WriteMemory(pid, 0x7F558, 4, new byte[] { 0x30, 0x64, 0x9c, 0xe0 });
            }
        }

        /// <summary>
        /// Ghost ratchet works by having a frame countdown, we hard enable ghost ratchet by freezing the frame countdown to 10.
        /// </summary>
        /// <param name="enabled">if true freezes frame countdown to 10, if false releases the freeze</param>
        public void SetGhostRatchet(bool enabled)
        {
            if (enabled) {
                ghostRatchetSubID = api.FreezeMemory(pid, addr.ghostTimer, 10);
            }
                else
            {
                api.ReleaseSubID(ghostRatchetSubID);
            }
        }

        /// <summary>
        /// Drek skip sets the destroyer to be up.
        /// </summary>
        /// <param name="enabled">Destroyer up or not</param>
        public void SetDrekSkip(bool enabled)
        {
            api.WriteMemory(pid, rac1.addr.drekSkip, 1, BitConverter.GetBytes(enabled));
        }

        /// <summary>
        /// Goodies menu is the NG+ goodies menu at the bottom of the main pause menu. This does not set challenge mode, it only enables the goodies menu as if you "timewarped to before you beat Drek" on Veldin 2.
        /// </summary>
        /// <param name="enabled">Sets or unsets goodies menu</param>
        public void SetGoodies(bool enabled)
        {
            api.WriteMemory(pid, rac1.addr.goodiesMenu, 1, BitConverter.GetBytes(enabled));
        }

        /// <summary>
        /// Overwrites game code that decreases ammo when you use a weapon
        /// </summary>
        /// <param name="toggle">Overwrites ammo decreasement code with nops on true, restores original game code on false</param>
        public override void ToggleInfiniteAmmo(bool toggle = false)
        {
            if (toggle)
            {
                api.WriteMemory(pid, 0xAA2DC, 4, new byte[] { 0x60, 0x00, 0x00, 0x00 });
            }
            else
            {
                api.WriteMemory(pid, 0xAA2DC, 4, new byte[] { 0x7d, 0x05, 0x39, 0x2e });
            }
        }

        /// <summary>
        /// Resets shooting skill points so that they don't carry over.
        /// </summary>
        /// <param name="reset">Sets or unsets shooting skill point variables.</param>
        public void SetShootSkillPoints(bool reset = false)
        {
            if (reset)
            {
                // Reset Batalia Sonic Summoner
                api.WriteMemory(pid, 0xA15F3C, 8, new string('0', 16));

                // Reset all other shooting SPs.
                api.WriteMemory(pid, 0x96C9DC, 32, new string('0', 64));
            }
            else
            {
                // Setup Batalia Sonic Summoner.
                api.WriteMemory(pid, 0xA15F3C, 8, string.Concat(Enumerable.Repeat("00000001", 2)));

                // Setup all shooting SPs.
                api.WriteMemory(pid, 0x96C9DC, 32,string.Concat(Enumerable.Repeat("00000020", 8)));
            }
        }


        public override void SetupFile()
        {
            throw new NotImplementedException();
        }

        public override void CheckInputs(object sender, EventArgs e)
        {
            if (Inputs.RawInputs == ConfigureCombos.saveCombo && inputCheck)
            {
                SavePosition();
                inputCheck = false;
            }
            if (Inputs.RawInputs == ConfigureCombos.loadCombo && inputCheck)
            {
                LoadPosition();
                inputCheck = false;
            }
            if (Inputs.RawInputs == ConfigureCombos.dieCombo && inputCheck)
            {
                KillYourself();
                inputCheck = false;
            }
            if (Inputs.RawInputs == ConfigureCombos.loadPlanetCombo & inputCheck)
            {
                LoadPlanet();
                inputCheck = false;
            }
            if (Inputs.RawInputs == ConfigureCombos.runScriptCombo && inputCheck)
            {
                AttachPS3Form.scripting?.RunCurrentCode();
                inputCheck = false;
            }
            if (Inputs.RawInputs == 0x00 & !inputCheck)
            {
                inputCheck = true;
            }
        }

        public override void CheckPlanetForDiscordRPC(object sender = null, EventArgs e = null)
        {
            if (!DiscordTimer.Enabled) {
                if (DiscordClient != null)
                {
                    DiscordClient.Dispose();
                    DiscordClient = null;
                    lastPlanetIndex = 100; // Valeur invalide pour forcer une mise à jour
                }
                return;
            }
            
            byte[] planetData = api.ReadMemory(pid, rac1.addr.currentPlanet, 4);
            if (planetData?.Length != 4) return; 
            
            uint planetindex = BitConverter.ToUInt32(planetData.Reverse().ToArray(), 0);
            
            if (planetindex != lastPlanetIndex) {
                if (DiscordClient == null) InitializeDiscordRPC();
                lastPlanetIndex = planetindex;
                if (planetindex < planetsList.Length)
                    UpdateRichPresence(planetsList[planetindex]);
            }
        }

        public void UpdateRichPresence(string planetname)
        {
            if (DiscordClient == null)
                return;
            var imageKey = planetname.ToLower();
            try {
                DiscordClient.SetPresence(new RichPresence()
                {
                    Details = planetname,
                    Timestamps = initialTimestamp,
                    Assets = new Assets()
                    {
                        LargeImageKey = "rac1",
                        LargeImageText = "Ratchet & Clank",
                        SmallImageKey = imageKey,
                        SmallImageText = planetname,
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cant update : {ex.Message}");
            }
        }

        public List<DebugOption> DebugOptions()
        {
            List<DebugOption> options = new List<DebugOption>();

            uint debugUpdateOptions = BitConverter.ToUInt32(api.ReadMemory(pid, addr.debugUpdateOptions, 4).Reverse().ToArray(), 0);
            uint debugModeControl = BitConverter.ToUInt32(api.ReadMemory(pid, addr.debugModeControl, 4).Reverse().ToArray(), 0);

            if ((debugUpdateOptions & 1) > 0)
            {
                options.Add(DebugOption.UpdateRatchet);
            }

            if ((debugUpdateOptions & 2) > 0)
            {
                options.Add(DebugOption.UpdateMobys);
            }

            if ((debugUpdateOptions & 4) > 0)
            {
                options.Add(DebugOption.UpdateParticles);
            }

            if (debugModeControl == 0)
            {
                options.Add(DebugOption.NormalCamera);
            }

            if (debugModeControl == 1)
            {
                options.Add(DebugOption.Freecam);
            }

            if (debugModeControl == 2)
            {
                options.Add(DebugOption.FreecamCharacter);
            }

            return options;
        }

        public void SetDebugOption(DebugOption option, bool value)
        {
            uint debugUpdateOptions = BitConverter.ToUInt32(api.ReadMemory(pid, addr.debugUpdateOptions, 4).Reverse().ToArray(), 0);

            switch (option)
            {
                case DebugOption.UpdateRatchet:
                    api.WriteMemory(pid, addr.debugUpdateOptions, (value ? debugUpdateOptions | 1 : debugUpdateOptions ^ 1));
                    break;
                case DebugOption.UpdateMobys:
                    api.WriteMemory(pid, addr.debugUpdateOptions, (value ? debugUpdateOptions | 2 : debugUpdateOptions ^ 2));
                    break;
                case DebugOption.UpdateParticles:
                    api.WriteMemory(pid, addr.debugUpdateOptions, (value ? debugUpdateOptions | 4 : debugUpdateOptions ^ 4));
                    break;
                case DebugOption.UpdateCamera:
                    api.WriteMemory(pid, addr.debugUpdateOptions, (value ? debugUpdateOptions | 8 : debugUpdateOptions ^ 8));
                    break;

                case DebugOption.NormalCamera:
                    SetDebugOption(DebugOption.UpdateCamera, true);
                    api.WriteMemory(pid, addr.debugModeControl, 0);
                    break;
                case DebugOption.Freecam:
                    SetDebugOption(DebugOption.UpdateCamera, false);
                    api.WriteMemory(pid, addr.debugModeControl, 1);
                    break;
                case DebugOption.FreecamCharacter:
                    SetDebugOption(DebugOption.UpdateCamera, false);
                    api.WriteMemory(pid, addr.debugModeControl, 2);
                    break;
            }
        }
    }
}
