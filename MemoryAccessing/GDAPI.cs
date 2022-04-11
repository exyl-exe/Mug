using Mug.Tracks;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mug.MemoryAccessing
{
    static class GDAPI
    {
        //GD :heart_eyes:
        const string GDProcessName = "GeometryDash";
        public const int SUBCYCLE_PER_FRAME = 4;

        //Useful values in byte[]
        public static readonly byte[] INT_ZERO_BYTES = new byte[] { 0x0, 0x0, 0x0, 0x0 };

        //Memory values to access
        static readonly MemoryValue level = new MemoryValue(new[] { 0x3222D0, 0x164}, 4);
        static readonly MemoryValue player = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224 }, 4);
        static readonly MemoryValue playerXPosition = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224, 0x67C }, 4);
        static readonly MemoryValue playerIsJumping = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224, 0x613 }, 1);
        static readonly MemoryValue playerIsJumping2 = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224, 0x614 }, 1);
        static readonly MemoryValue playerIsDead = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224, 0x63F }, 1);
        static readonly MemoryValue playerHasWon = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224, 0x662 }, 1);
        static readonly MemoryValue playerGravity = new MemoryValue(new[] { 0x3222D0, 0x164, 0x224, 0x63E }, 1);
        static readonly MemoryValue currentSessionAttempts = new MemoryValue(new[] { 0x3222D0, 0x164, 0x4A8 }, 4);

        public const int JUMP_FUNC_ADDR = 0x1F4E40;
        public const int RELEASE_FUNC_ADDR = 0x1F4F70;
        public const int RENDERING_LOOP_ADDR = 0x2029C0;
        public const int END_OF_RENDERING_LOOP_ADDR = 0x2036FB;
        public const int JUMP_FUNC_COUNTER_ADDR = 0x1F4E40;
        public const int RELEASE_FUNC_COUNTER_ADDR = 0x1F4F70;
        public const int SPAWN_FUNC_ADDR = 0x20BF00;
        public const int SUBCYCLE_ADDR = 0x202F10;

        //Generic accessers to a process' memory
        static MemoryAccesser gdAccesser;

        //To check if api has been initialized
        static bool initialized;

        public static int GetLevelAddr()
        {
            APIUsableCheck();
            int bytes = 0;
            return BitConverter.ToInt32(gdAccesser.ReadMemoryValue(level, ref bytes), 0);
        }

        public static int GetPlayerAddr()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return BitConverter.ToInt32(gdAccesser.ReadMemoryValue(player, ref bytes), 0);
        }

        public static float GetPlayerPos()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return BitConverter.ToSingle(gdAccesser.ReadMemoryValue(playerXPosition, ref bytes), 0);
        }

        public static int GetCurrentAttempt()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return BitConverter.ToInt32(gdAccesser.ReadMemoryValue(currentSessionAttempts, ref bytes), 0);
        }

        public static bool IsPlayerJumping()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return gdAccesser.ReadMemoryValue(playerIsJumping, ref bytes)[0] == 0x01 || gdAccesser.ReadMemoryValue(playerIsJumping2, ref bytes)[0] == 0x01;
        }

        public static bool IsGravityUpward()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return gdAccesser.ReadMemoryValue(playerGravity, ref bytes)[0] == 0x01;
        }

        public static bool IsInLevel()
        {
            APIUsableCheck();
            return GetLevelAddr() != 0;
        }

        public static bool HasPlayerWon()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return gdAccesser.ReadMemoryValue(playerHasWon, ref bytes)[0] == 0x1;//TODO mes meilleurs constantes
        }

        public static bool IsPlayerDead()
        {
            LevelAPIUsableCheck();
            int bytes = 0;
            return gdAccesser.ReadMemoryValue(playerIsDead, ref bytes)[0] == 0x1;
        }

        public static bool Initialize()
        {
            gdAccesser = new MemoryAccesser();     
            var success = gdAccesser.AttachTo(GDProcessName);
            if (success)
            {
                MugAPI.Initialize(gdAccesser);
                initialized = true;
            }
            return success;
        }

        public static void RevertAllGDAlterations()
        {
            gdAccesser.ResetTargetProcessMemory();
            initialized = false;
        }

        public static bool IsInitialized()
        {
            return initialized;
        }

        public static void APIUsableCheck()
        {
            if (!initialized)
            {
                throw new Exception("GD API was used without being initialized");
            }

            if (gdAccesser.process.HasExited)
            {
                throw new Exception("GD API was used but GD process has been terminated");
            }
        }

        public static void LevelAPIUsableCheck()
        {
            APIUsableCheck();
            if (!IsInLevel())
            {
                throw new Exception("GD API was used while not playing a level");
            }
        }
    }
}
