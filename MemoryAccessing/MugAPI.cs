using Iced.Intel;
using Mug.Configuration;
using Mug.Record;
using Mug.Tracks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms.VisualStyles;
using static Iced.Intel.AssemblerRegisters;

namespace Mug.MemoryAccessing
{
    static class MugAPI
    {
        const float POSITION_DELTA = 0.5f;
        //If the player's position is less than POSITION_DELTA away from an input, then that input must be played.
        //On 60fps, 1 frame ~= 4.2 units of position. The bot can therefore play frame-perfect inputs as long as frames don't less than 0.5 units of position :
        // (4.2/0.5)*60 ~= 500fps (more if the level doesn't contain any x0.5 speed).

        //TODO shouldn't be here ?
        const byte NOT_PLAYING = 0x0;
        const byte WILL_PLAY_NEXT_ATTEMPT = 0x1;
        const byte PLAYING = 0x2;

        const byte NOT_RECORDING = 0x0;
        const byte WILL_RECORD = 0x1;
        const byte RECORDING = 0x2;

        //Generic accessers to GD memory
        static MemoryAccesser gdAccesser;

        //Allocated memory segments
        //Useful for the track player
        static IntPtr currentTrackInputsAddr;
        static IntPtr playingStateAddr;
        static IntPtr playedInputsCounterAddr;
        static IntPtr loadedInputsCounterAddr;
        static IntPtr currentInputAddr;

        //Useful for the track recorder
        static IntPtr recordingStateAddr;
        static IntPtr recordedInputsCounterAddr;

        static IntPtr writeRecordedInputAddr;//TODO mutex :)
        static IntPtr firstOfRecordedInputsArrayAddr;
        static IntPtr lastOfRecordedInputsArrayAddr;
        static IntPtr readRecordedInputAddr;

        static IntPtr writeRecordedInputMetadataAddr;//TODO mutex :)
        static IntPtr firstOfRecordedInputsMetadataArrayAddr;
        static IntPtr lastOfRecordedInputsMetadataArrayAddr;
        static IntPtr readRecordedInputMetadataAddr;

        static IntPtr stopRecordingAndSaveAddr;
        static IntPtr saveRecordMetadataFuncAddr;
        static IntPtr incRecordedInputPointerFuncAddr;
        static IntPtr onRecordedAttemptStartsFuncAddr;

        //Automatically initialized on injection
        static IntPtr callJumpFuncAddr;
        static IntPtr callReleaseFuncAddr;

        static IntPtr jumpInputsCounterAddr;
        static IntPtr releaseInputsCounterAddr;
        static IntPtr frameCounterAddr;
        static IntPtr subcycleCounterAddr;

        public static void LoadTrackInMemory(MugTrack t)//Be careful, the track needs to be ordered
        {
            StopCurrentTrack();//Security, TODO réléchir si c'est bien ou pas
            var size = t.Inputs.Count;
            var trackBytes = new byte[size * MugInput.SIZE_BYTES];
            for (var i = 0; i < size; i++)
            {
                var input = t.Inputs[i];
                byte[] inputAsByte = input.ToBytes();
                Array.Copy(inputAsByte, 0, trackBytes, i * MugInput.SIZE_BYTES, MugInput.SIZE_BYTES);
            }
            gdAccesser.WriteBytes(loadedInputsCounterAddr, BitConverter.GetBytes(size));
            gdAccesser.WriteBytes(currentTrackInputsAddr, trackBytes);
        }

        public static void PlayCurrentTrack()
        {
            gdAccesser.WriteBytes(playingStateAddr, new byte[] { WILL_PLAY_NEXT_ATTEMPT });
        }

        public static void StopCurrentTrack()
        {
            gdAccesser.WriteBytes(playingStateAddr, new byte[] { NOT_PLAYING });
        }

        public static void StartRecording()
        {
            gdAccesser.WriteBytes(recordingStateAddr, new byte[] { WILL_RECORD });
            gdAccesser.CopyPointer(writeRecordedInputAddr, readRecordedInputAddr, sizeof(int));
            gdAccesser.CopyPointer(writeRecordedInputMetadataAddr, readRecordedInputMetadataAddr, sizeof(int));
        }

        public static void StopRecording()
        {
            gdAccesser.WriteBytes(recordingStateAddr, new byte[] { NOT_RECORDING });
        }

        public static void StopRecordingAndSave()
        {
            gdAccesser.Execute(stopRecordingAndSaveAddr);
        }

        public static bool IsRecording()
        {
            int bytes = 0;
            return gdAccesser.ReadBytes(recordingStateAddr, sizeof(byte), ref bytes)[0] != NOT_RECORDING;
        }

        internal static bool IsPlaying()
        {
            int bytes = 0;
            return gdAccesser.ReadBytes(playingStateAddr, sizeof(byte), ref bytes)[0] != NOT_PLAYING;
        }

        public static List<MugRecording> FetchCurrentRecords()
        {
            int bytesRead = 0;
            var lastWrittenMetadata = BitConverter.ToInt32(gdAccesser.ReadBytes(writeRecordedInputMetadataAddr, sizeof(int), ref bytesRead),0);
            var res = new List<MugRecording>();
            while (BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputMetadataAddr, sizeof(int), ref bytesRead),0) != lastWrittenMetadata)
            {
                res.Add(FetchOneRecord());
                IncreaseMetadataReaderPointer();
            }
            return res;
        }

        private static MugRecording FetchOneRecord()
        {
            int metadataBytesRead = 0;
            var res = new MugRecording();
            var metadataAddress = BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputMetadataAddr, sizeof(int), ref metadataBytesRead),0);
            var metadata = gdAccesser.ReadBytes((IntPtr)metadataAddress, MugRecording.METADATA_SIZE_BYTES, ref metadataBytesRead);
            var inputsCount = res.LoadMetadataFromBytes(metadata);
            res.Track = new MugTrack(GDConfig.RefreshRate);

            var inputList = new List<MugInput>();
            for(int i=0; i<inputsCount; i++) {
                int inputsBytesRead = 0;
                var inputAddress = BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputAddr, sizeof(int), ref inputsBytesRead),0);
                var inputAsBytes = gdAccesser.ReadBytes((IntPtr)inputAddress, MugInput.SIZE_BYTES, ref inputsBytesRead);            
                var input = new MugInput();
                input.LoadFromBytes(inputAsBytes);
                inputList.Add(input);
                IncreaseInputReaderPointer();
            }
            res.Track.Inputs = inputList;
            return res;
        }

        private static void IncreaseInputReaderPointer()
        {
            int bytesRead = 0;
            var current = BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputAddr, sizeof(int), ref bytesRead), 0);
            if (current == (int)lastOfRecordedInputsArrayAddr)
            {
                gdAccesser.WriteBytes(readRecordedInputAddr, BitConverter.GetBytes((int)firstOfRecordedInputsArrayAddr));
            }
            else
            {
                int bytes = 0;
                var pointedAddress = BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputAddr, sizeof(int), ref bytes),0);
                var newValue = BitConverter.GetBytes(pointedAddress + MugInput.SIZE_BYTES);
                gdAccesser.WriteBytes(readRecordedInputAddr, newValue);
            }
        }

        private static void IncreaseMetadataReaderPointer()
        {
            int bytesRead = 0;
            var current = BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputMetadataAddr, sizeof(int), ref bytesRead), 0);
            if (current == (int)lastOfRecordedInputsMetadataArrayAddr)
            {
                gdAccesser.WriteBytes(readRecordedInputMetadataAddr, BitConverter.GetBytes((int)firstOfRecordedInputsMetadataArrayAddr));
            }
            else
            {
                int bytes = 0;
                var pointedAddress = BitConverter.ToInt32(gdAccesser.ReadBytes(readRecordedInputMetadataAddr, sizeof(int), ref bytes), 0);
                var newValue = BitConverter.GetBytes(pointedAddress + MugRecording.METADATA_SIZE_BYTES);
                gdAccesser.WriteBytes(readRecordedInputMetadataAddr, newValue);
            }
        }

        public static int GetFrameCount()
        {
            GDAPI.LevelAPIUsableCheck();
            int bytes = 0;
            return BitConverter.ToInt32(gdAccesser.ReadBytes(frameCounterAddr, sizeof(int), ref bytes), 0);
        }

        public static int GetCurrentSubcycleCount()
        {
            GDAPI.LevelAPIUsableCheck();
            int bytes = 0;
            return BitConverter.ToInt32(gdAccesser.ReadBytes(subcycleCounterAddr, sizeof(int), ref bytes), 0);
        }

        public static void Initialize(MemoryAccesser accesser)
        {
            gdAccesser = accesser;
            InitializeNeededMemory();
            
            InjectPlayerJump();//Play
            InjectPlayerRelease();//Play
            
            InjectJumpWatcher();//Record
            InjectReleaseWatcher();//Record
            InjectRecordMetadataWriteFunc();//Record
            InjectIncWriteInputFunc();//Record
            InjectOnRecordedAttemptRespawnFunc();//Record
            InjectStopRecordingAndSaveFunc();

            InjectFrameCounter();
            InjectSubcycleCounter();
            InjectMainLoopManager();//Record + play
            InjectResetOnSpawn();//Record + play
        }

        private static void InitializeNeededMemory()
        {
            //Memory containing the track for the track player
            currentTrackInputsAddr = gdAccesser.AllocateMemory(MugTrack.MAX_INPUTS * MugInput.SIZE_BYTES);
            currentInputAddr = gdAccesser.AllocateMemory(sizeof(int));
            gdAccesser.WriteBytes(currentInputAddr, BitConverter.GetBytes((int)currentTrackInputsAddr));

            //Pointers for the track player
            playingStateAddr = gdAccesser.AllocateMemory(sizeof(byte));
            playedInputsCounterAddr = gdAccesser.AllocateMemory(sizeof(int));
            loadedInputsCounterAddr = gdAccesser.AllocateMemory(sizeof(int));

            //Pointers to the "cycling" inputs memory
            firstOfRecordedInputsArrayAddr = gdAccesser.AllocateMemory(MugRecorder.MAX_BUFFERED_INPUTS * MugInput.SIZE_BYTES);
            writeRecordedInputAddr = gdAccesser.AllocateMemory(sizeof(int));
            gdAccesser.WriteBytes(writeRecordedInputAddr, BitConverter.GetBytes((int)firstOfRecordedInputsArrayAddr));
            lastOfRecordedInputsArrayAddr = gdAccesser.AllocateMemory(sizeof(int));
            gdAccesser.WriteBytes(lastOfRecordedInputsArrayAddr, BitConverter.GetBytes((int)firstOfRecordedInputsArrayAddr + (MugRecorder.MAX_BUFFERED_INPUTS-1) * MugInput.SIZE_BYTES));

            //Pointers to the "cycling" records metadatas memory
            firstOfRecordedInputsMetadataArrayAddr = gdAccesser.AllocateMemory(MugRecorder.MAX_BUFFERED_TRACKS * MugRecording.METADATA_SIZE_BYTES);
            writeRecordedInputMetadataAddr = gdAccesser.AllocateMemory(sizeof(int));
            gdAccesser.WriteBytes(writeRecordedInputMetadataAddr, BitConverter.GetBytes((int)firstOfRecordedInputsMetadataArrayAddr));
            lastOfRecordedInputsMetadataArrayAddr = gdAccesser.AllocateMemory(sizeof(int));
            gdAccesser.WriteBytes(lastOfRecordedInputsMetadataArrayAddr, BitConverter.GetBytes((int)firstOfRecordedInputsMetadataArrayAddr + (MugRecorder.MAX_BUFFERED_TRACKS - 1) * MugInput.SIZE_BYTES));

            //Pointers for the track recorder
            recordingStateAddr = gdAccesser.AllocateMemory(sizeof(byte));
            recordedInputsCounterAddr = gdAccesser.AllocateMemory(sizeof(int));

            //Various counters
            jumpInputsCounterAddr = gdAccesser.AllocateMemory(sizeof(int));
            releaseInputsCounterAddr = gdAccesser.AllocateMemory(sizeof(int));
            frameCounterAddr = gdAccesser.AllocateMemory(sizeof(int));
            subcycleCounterAddr = gdAccesser.AllocateMemory(sizeof(int));

            //TODO une fois sobre de fatigue, voir si peut pas virer
            readRecordedInputAddr = gdAccesser.AllocateMemory(sizeof(int));
            readRecordedInputMetadataAddr = gdAccesser.AllocateMemory(sizeof(int));
        }

        private static void InjectJumpWatcher()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var asm = GetJumpCounterCode();
            gdAccesser.InsertCode(asm, moduleBaseAddress + GDAPI.JUMP_FUNC_COUNTER_ADDR);
        }

        private static void InjectReleaseWatcher()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var asm = GetReleaseCounterCode();
            gdAccesser.InsertCode(asm, moduleBaseAddress + GDAPI.RELEASE_FUNC_COUNTER_ADDR);
        }

        public static void InjectFrameCounter()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var asm = GetFrameCounterCode();
            gdAccesser.InsertCode(asm, moduleBaseAddress + GDAPI.END_OF_RENDERING_LOOP_ADDR);
        }

        public static void InjectSubcycleCounter()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var asm = GetBasicCounterCode((int)subcycleCounterAddr);
            gdAccesser.InsertCode(asm, moduleBaseAddress + GDAPI.SUBCYCLE_ADDR);
        }

        private static void InjectMainLoopManager()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var code1 = GetMainLoopPlayerCode();
            var code2 = GetMainLoopRecorderCode();
            var asm = new byte[code1.Length + code2.Length];
            Array.Copy(code1, asm, code1.Length);
            Array.Copy(code2, 0, asm, code1.Length, code2.Length);
            gdAccesser.InsertCode(asm, moduleBaseAddress + GDAPI.RENDERING_LOOP_ADDR);
        }

        private static void InjectResetOnSpawn()
        //IMPORTANT : designed such as resets have not occured, executed at the very beginning of the spawn function. Attempts etc. still not reset.
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var code1 = GetPlayerSpawnResetCode();
            var code2 = GetRecorderSpawnResetCode();
            var code3 = GetCountersResetCode();
            var asm = new byte[code1.Length + code2.Length + code3.Length];
            Array.Copy(code1, asm, code1.Length);
            Array.Copy(code2, 0, asm, code1.Length, code2.Length);
            Array.Copy(code3, 0, asm, code1.Length + code2.Length, code3.Length);
            gdAccesser.InsertCode(asm, moduleBaseAddress + GDAPI.SPAWN_FUNC_ADDR);
        }

        private static void InjectPlayerJump()
        {
            var asm = GetJumpFuncCode();
            callJumpFuncAddr = gdAccesser.InjectCode(asm);
        }

        private static void InjectPlayerRelease()
        {
            var asm = GetReleaseFuncCode();
            callReleaseFuncAddr = gdAccesser.InjectCode(asm);
        }

        private static void InjectIncWriteInputFunc()
        {
            var asm = IncWriteInputPointerCode();
            incRecordedInputPointerFuncAddr = gdAccesser.InjectCode(asm);
        }

        private static void InjectRecordMetadataWriteFunc()
        {
            var asm = WriteRecordMetadataFunc();
            saveRecordMetadataFuncAddr = gdAccesser.InjectCode(asm);
        }

        private static void InjectOnRecordedAttemptRespawnFunc()
        {
            var asm = OnRecordedAttemptStartsFuncCode();
            onRecordedAttemptStartsFuncAddr = gdAccesser.InjectCode(asm);
        }

        private static void InjectStopRecordingAndSaveFunc()
        {
            var asm = StopAndSaveRecordFuncCode();
            stopRecordingAndSaveAddr = gdAccesser.InjectCode(asm);
        }

        private static byte[] GetJumpCounterCode()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var a = new Assembler(32);

            var endOfCounter = a.CreateLabel("endOfCounter");

            a.push(eax);
            a.mov(eax,__dword_ptr[(int)moduleBaseAddress + 0x3222D0]);
            a.mov(eax, __dword_ptr[eax + 0x164]);
            a.cmp(eax, 0x0);
            a.je(endOfCounter);
            a.mov(eax, __word_ptr[eax + 0x224]);
            a.cmp(ecx, eax);
            a.jne(endOfCounter);
            a.inc(__dword_ptr[(int)jumpInputsCounterAddr]);
            a.Label(ref endOfCounter);
            a.pop(eax);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetReleaseCounterCode()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var a = new Assembler(32);

            var endOfCounter = a.CreateLabel("endOfCounter");

            a.push(eax);
            a.mov(eax, __dword_ptr[(int)moduleBaseAddress + 0x3222D0]);
            a.mov(eax, __dword_ptr[eax + 0x164]);
            a.cmp(eax, 0x0);
            a.je(endOfCounter);
            a.mov(eax, __word_ptr[eax + 0x224]);
            a.cmp(ecx, eax);
            a.jne(endOfCounter);
            a.inc(__dword_ptr[(int)releaseInputsCounterAddr]);
            a.Label(ref endOfCounter);
            a.pop(eax);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetBasicCounterCode(int counterAddress)
        {
            var a = new Assembler(32);

            a.inc(__dword_ptr[counterAddress]);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetFrameCounterCode()
        {
            var a = new Assembler(32);
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();

            var endOfCounter = a.CreateLabel("endOfCounter");

            a.push(edx);

            a.mov(edx, __dword_ptr[(int)moduleBaseAddress + 0x3222D0]);
            a.mov(edx, __dword_ptr[edx + 0x164]);
            a.mov(edx, __dword_ptr[edx + 0x224]);//edx now contains the address of the player entity

            a.cmp(__byte_ptr[edx + 0x63F], 0);//Check if player is alive
            a.jne(endOfCounter);

            a.cmp(__byte_ptr[edx + 0x662], 0);//Check if not during win animation
            a.jne(endOfCounter);

            a.inc(__dword_ptr[(int)frameCounterAddr]);

            a.Label(ref endOfCounter);
            a.pop(edx);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetMainLoopPlayerCode()
        {
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();
            var a = new Assembler(32);
            var endOfPlayer = a.CreateLabel("endOfPlayer");
            var playInput = a.CreateLabel("playInput");
            var jump = a.CreateLabel("jump");
            var release = a.CreateLabel("release");
            var endClick = a.CreateLabel("endClick");

            a.push(edx);
            a.push(esi);

            //loading the player pointer into esi
            a.mov(esi, __dword_ptr[(int)moduleBaseAddress + 0x3222D0]);
            a.mov(esi, __dword_ptr[esi + 0x164]);
            a.mov(esi, __dword_ptr[esi + 0x224]);

            a.cmp(__byte_ptr[(int)playingStateAddr], PLAYING);//Check if currently playing a track
            a.jne(endOfPlayer);

            a.mov(edx, __dword_ptr[(int)playedInputsCounterAddr]);//Check if there are inputs left to play
            a.cmp(edx, __dword_ptr[(int)loadedInputsCounterAddr]);
            a.jge(endOfPlayer);

            a.cmp(__byte_ptr[esi + 0x63F], 0);//Check if player is alive
            a.jne(endOfPlayer);

            /*a.mov(edx, __dword_ptr[(int)currentInputAddr]);//Check the frame
            a.mov(edx, __word_ptr[edx]);
            a.cmp(edx, __dword_ptr[(int)frameCounterAddr]);*/

            //Check the position
            a.mov(edx, BitConverter.ToInt32(BitConverter.GetBytes(POSITION_DELTA), 0));//Tuning XD, loads the constant into the stack
            a.push(edx);//fadd : St1 = epsilon, fcomip = st2
            a.fld(__dword_ptr[esp]);
            a.pop(edx);

            a.fld(__dword_ptr[esi + 0x67C]);//fadd : st0 = player pos, fcomip = st1
            a.fadd(st0, st1);//Adding delta to the player position, to check if it reaches the input's position

            a.mov(edx, __dword_ptr[(int)currentInputAddr]);
            a.fld(__dword_ptr[edx + MugInput.POS_OFFSET]);//fcomip = st0
            a.fcomip(st0, st1);//comparison between currentInput position and playerPosition
            a.fstp(st0);//oupsi
            a.fstp(st0);

            a.ja(endOfPlayer);//if current player pos is below next input pos then no need to click
            a.jmp(playInput);

            a.Label(ref playInput);//plays the current input
            a.mov(edx, __dword_ptr[(int)currentInputAddr]);
            a.cmp(__byte_ptr[edx + MugInput.INPUT_TYPE_OFFSET], MugInput.RELEASE_BYTE_VALUE);
            a.je(release);
            a.jmp(jump);

            //jump
            a.Label(ref jump);
            a.mov(edx, (int)callJumpFuncAddr);
            a.call(edx);
            a.jmp(endClick);

            //release
            a.Label(ref release);
            a.mov(edx, (int)callReleaseFuncAddr);
            a.call(edx);
            a.jmp(endClick);

            a.Label(ref endClick);
            a.add(__dword_ptr[(int)currentInputAddr], MugInput.SIZE_BYTES);
            a.inc(__dword_ptr[(int)playedInputsCounterAddr]);
            a.jmp(endOfPlayer);

            a.Label(ref endOfPlayer);
            a.mov(__dword_ptr[(int)subcycleCounterAddr], 0);

            a.pop(esi);
            a.pop(edx);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetMainLoopRecorderCode()
        {
            var a = new Assembler(32);
            var moduleBaseAddress = gdAccesser.GetMainModuleAdress();

            var endOfRecorder = a.CreateLabel("endOfRecorder");
            var saveInput = a.CreateLabel("saveInput");
            var recordJump = a.CreateLabel("recordJump");
            var recordRelease = a.CreateLabel("recordRelease");

            a.push(edx);
            a.push(ecx);
            a.push(ebx);
            a.cmp(__byte_ptr[(int)recordingStateAddr], RECORDING);
            a.jne(endOfRecorder);
            a.cmp(__dword_ptr[(int)jumpInputsCounterAddr], 0x0);
            a.jne(recordJump);
            a.cmp(__dword_ptr[(int)releaseInputsCounterAddr], 0x0);
            a.jne(recordRelease);
            a.jmp(endOfRecorder);

            a.Label(ref recordJump);
            a.mov(__dword_ptr[(int)jumpInputsCounterAddr], 0x0);
            a.mov(cl, 1);
            a.jmp(saveInput);

            a.Label(ref recordRelease);
            a.mov(__dword_ptr[(int)releaseInputsCounterAddr], 0x0);
            a.mov(cl, 0);
            a.jmp(saveInput);

            a.Label(ref saveInput);//TODO write input func ?
            a.mov(ebx, __dword_ptr[(int)writeRecordedInputAddr]);//place to write the input
            a.mov(edx, __dword_ptr[(int)frameCounterAddr]);//frame
            a.mov(__dword_ptr[ebx], edx);
      
            a.mov(__byte_ptr[ebx + MugInput.INPUT_TYPE_OFFSET], cl);//input type

            a.mov(edx, __dword_ptr[(int)moduleBaseAddress + 0x3222D0]);//position, TODO might need previous position
            a.mov(edx, __dword_ptr[edx + 0x164]);
            a.mov(edx, __dword_ptr[edx + 0x224]);
            a.mov(edx, __dword_ptr[edx + 0x67C]);
            a.mov(__dword_ptr[ebx + MugInput.POS_OFFSET], edx);

            a.mov(edx, (int)incRecordedInputPointerFuncAddr);
            a.call(edx);
            a.inc(__dword_ptr[(int)recordedInputsCounterAddr]);
            a.jmp(endOfRecorder);

            a.Label(ref endOfRecorder);
            a.pop(ebx);
            a.pop(ecx);
            a.pop(edx);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }


        private static byte[] GetPlayerSpawnResetCode()
        {
            var a = new Assembler(32);

            var playingRespawn = a.CreateLabel("playingRespawn");
            var donePlayingRespawn = a.CreateLabel("donePlayingRespawn");
            var endOfSpawnFunc = a.CreateLabel("endOfSpawnFunc");

            a.push(edx);

            //Track player resets
            a.mov(__dword_ptr[(int)playedInputsCounterAddr], 0);
            a.mov(edx, (int)currentTrackInputsAddr);
            a.mov(__dword_ptr[(int)currentInputAddr], edx);

            //Playing state updates
            a.cmp(__byte_ptr[(int)playingStateAddr], WILL_PLAY_NEXT_ATTEMPT);
            a.je(playingRespawn);
            a.cmp(__byte_ptr[(int)playingStateAddr], PLAYING);
            a.je(donePlayingRespawn);
            a.jmp(endOfSpawnFunc);

            a.Label(ref playingRespawn);
            a.mov(__byte_ptr[(int)playingStateAddr], PLAYING);
            a.jmp(endOfSpawnFunc);

            a.Label(ref donePlayingRespawn);
            a.mov(__byte_ptr[(int)playingStateAddr], NOT_PLAYING);

            a.Label(ref endOfSpawnFunc);
            a.pop(edx);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetRecorderSpawnResetCode()
        {
            var a = new Assembler(32);

            var endOfRecordResetFunc = a.CreateLabel("endOfRecordResetFunc");
            var recordStartsRespawn = a.CreateLabel("recordStartsRespawn");

            a.push(edx);
            a.cmp(__byte_ptr[(int)recordingStateAddr], NOT_RECORDING);
            a.je(endOfRecordResetFunc);

            a.cmp(__byte_ptr[(int)recordingStateAddr], WILL_RECORD);
            a.je(recordStartsRespawn);

            a.mov(edx, (int)saveRecordMetadataFuncAddr);//RECORDING state respawn
            a.call(edx);
            a.mov(edx, (int)onRecordedAttemptStartsFuncAddr);
            a.call(edx);
            a.jmp(endOfRecordResetFunc);

            a.Label(ref recordStartsRespawn);
            a.mov(edx, (int)onRecordedAttemptStartsFuncAddr);
            a.call(edx);
            a.mov(__byte_ptr[(int)recordingStateAddr], RECORDING);

            a.Label(ref endOfRecordResetFunc);
            a.pop(edx);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetCountersResetCode()
        {
            var a = new Assembler(32);

            //General resets
            a.mov(__dword_ptr[(int)frameCounterAddr], 0);
            a.mov(__dword_ptr[(int)subcycleCounterAddr], 0);

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetJumpFuncCode()
        {
            var a = new Assembler(32);
            var moduleBaseAddress = (int)gdAccesser.GetMainModuleAdress();
            var jumpFunc = moduleBaseAddress + GDAPI.JUMP_FUNC_ADDR;
            //save registers
            a.push(eax);
            a.push(ebx);
            a.push(ecx);
            a.push(edx);
            a.push(esi);

            //Load correct values in registers and call game jump function on player 1
            a.mov(esi, __dword_ptr[moduleBaseAddress+0x3222D0]);
            a.mov(esi, __dword_ptr[esi + 0x164]);
            a.mov(ecx, __dword_ptr[esi + 0x224]);
            a.mov(eax, __dword_ptr[esi + 0x124]);
            a.mov(eax, __dword_ptr[eax + 0x10C]);
            a.mov(eax, __dword_ptr[eax + 0x20]);
            a.xor(al, al);
            a.mov(bl, 1);
            a.mov(edx, jumpFunc);
            a.push(0);
            a.call(edx);

            //Load correct values in registers and call game jump function on player 2
            a.mov(esi, __dword_ptr[moduleBaseAddress + 0x3222D0]);
            a.mov(esi, __dword_ptr[esi + 0x164]);
            a.mov(ecx, __dword_ptr[esi + 0x228]);
            a.mov(eax, __dword_ptr[esi + 0x124]);
            a.mov(eax, __dword_ptr[eax + 0x10C]);
            a.mov(eax, __dword_ptr[eax + 0x20]);
            a.xor(al, al);
            a.mov(bl, 1);
            a.mov(edx, jumpFunc);
            a.push(0);
            a.call(edx);

            //Restore registers
            a.pop(esi);
            a.pop(edx);
            a.pop(ecx);
            a.pop(ebx);
            a.pop(eax);
            a.ret();

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] GetReleaseFuncCode()
        {
            var a = new Assembler(32);
            var moduleBaseAddress = (int)gdAccesser.GetMainModuleAdress();
            var releaseFunc = moduleBaseAddress + GDAPI.RELEASE_FUNC_ADDR;

            //save registers
            a.push(eax);
            a.push(ecx);
            a.push(edx);
            a.push(esi);

            a.mov(eax, 0);
            a.mov(esi, __dword_ptr[moduleBaseAddress+0x3222D0]);
            a.mov(esi, __dword_ptr[esi + 0x164]);
            a.mov(ecx, __dword_ptr[esi + 0x224]);
            a.mov(edx, releaseFunc);
            a.push(0);
            a.call(edx);

            a.mov(eax, 0);
            a.mov(esi, __dword_ptr[moduleBaseAddress + 0x3222D0]);
            a.mov(esi, __dword_ptr[esi + 0x164]);
            a.mov(ecx, __dword_ptr[esi + 0x228]);
            a.mov(edx, releaseFunc);
            a.push(0);
            a.call(edx);

            //Restore registers
            a.pop(esi);
            a.pop(edx);
            a.pop(ecx);
            a.pop(eax);
            a.ret();

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] IncWriteInputPointerCode()
        {
            var a = new Assembler(32);

            var resetWritePointer = a.CreateLabel("resetWritePointer");
            var endOfIncWriteInput = a.CreateLabel("endOfIncWriteInput");

            a.push(edx);
            a.mov(edx, __dword_ptr[(int)writeRecordedInputAddr]);
            a.cmp(edx, (int)lastOfRecordedInputsArrayAddr);
            a.je(resetWritePointer);
            a.add(__dword_ptr[(int)writeRecordedInputAddr], MugInput.SIZE_BYTES);
            a.jmp(endOfIncWriteInput);

            a.Label(ref resetWritePointer);
            a.mov(edx, (int)firstOfRecordedInputsArrayAddr);
            a.mov(__dword_ptr[(int)writeRecordedInputAddr], edx);
            a.jmp(endOfIncWriteInput);

            a.Label(ref endOfIncWriteInput);
            a.pop(edx);
            a.ret();

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] WriteRecordMetadataFunc()
        {
            var a = new Assembler(32);
            var mainModule = gdAccesser.GetMainModuleAdress();

            var resetWritePointer = a.CreateLabel("resetWritePointer");
            var endOfWriteAttempt = a.CreateLabel("endOfWriteAttempt");

            a.push(edx);
            a.push(ecx);
            a.sub(esp, 16);
            a.movdqu(__oword_ptr[esp], xmm0);
            a.sub(esp, 16);
            a.movdqu(__oword_ptr[esp], xmm1);

            a.mov(edx, __dword_ptr[(int)recordedInputsCounterAddr]);
            a.mov(ecx, __dword_ptr[(int)writeRecordedInputMetadataAddr]);

            a.mov(__dword_ptr[ecx], edx);//writing number of inputs

            a.mov(edx, __dword_ptr[(int)mainModule + 0x3222D0]);    //Writing attempt
            a.mov(edx, __dword_ptr[edx + 0x164]);                   //attempts = 0x3222D0, 0x164, 0x4A8
            a.mov(edx, __dword_ptr[edx + 0x4A8]);                   //current attempt
            a.mov(__dword_ptr[ecx + MugRecording.ATTEMPT_OFFSET], edx);

            a.mov(edx, __dword_ptr[(int)frameCounterAddr]);         //writing frameCount
            a.mov(__dword_ptr[ecx + MugRecording.FRAME_OFFSET], edx);

            a.mov(edx, __dword_ptr[(int)mainModule + 0x3222D0]);    //Calculating current percent
            a.mov(edx, __dword_ptr[edx + 0x164]);
            a.movss(xmm0, __qword_ptr[edx + 0x3B4]);//level size
            a.mov(edx, __dword_ptr[(int)mainModule + 0x3222D0]);
            a.mov(edx, __dword_ptr[edx + 0x164]);
            a.mov(edx, __dword_ptr[edx + 0x224]);
            a.movss(xmm1, __qword_ptr[edx + 0x34]);//player pos (in level)
            a.divss(xmm1, xmm0);//divide
            a.movd(__dword_ptr[ecx + MugRecording.END_PERCENT_OFFSET], xmm1);//store result

            a.mov(edx, __dword_ptr[(int)writeRecordedInputMetadataAddr]);//increment metadata writer
            a.cmp(edx, (int)lastOfRecordedInputsMetadataArrayAddr);//check if it has reached the end of the memory
            a.je(resetWritePointer);

            a.add(__dword_ptr[(int)writeRecordedInputMetadataAddr], MugRecording.METADATA_SIZE_BYTES);//regular increase
            a.jmp(endOfWriteAttempt);

            a.Label(ref resetWritePointer);
            a.mov(edx, (int)firstOfRecordedInputsMetadataArrayAddr);//Returning to the start of the allocated memory
            a.mov(__dword_ptr[(int)writeRecordedInputMetadataAddr], edx);
            a.jmp(endOfWriteAttempt);

            a.Label(ref endOfWriteAttempt); 

            a.movdqu(xmm1, __oword_ptr[esp]);
            a.add(esp, 16);   
            a.movdqu(xmm0,__oword_ptr[esp]);
            a.add(esp, 16);

            a.pop(ecx);
            a.pop(edx);
            a.ret();

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] OnRecordedAttemptStartsFuncCode()
        {
            var a = new Assembler(32);
            var mainModule = gdAccesser.GetMainModuleAdress();

            a.push(edx);
            a.mov(__dword_ptr[(int)recordedInputsCounterAddr], 0x0);  //reset recorded inputs counter
            a.mov(__dword_ptr[(int)releaseInputsCounterAddr], 0x0);//Reset inputs counters
            a.mov(__dword_ptr[(int)jumpInputsCounterAddr], 0x0);
            a.pop(edx);
            a.ret();

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

        private static byte[] StopAndSaveRecordFuncCode()
        {
            var a = new Assembler(32);

            a.push(edx);
            a.mov(__byte_ptr[(int)recordingStateAddr], NOT_RECORDING);
            a.mov(edx, (int)saveRecordMetadataFuncAddr);
            a.call(edx);
            a.pop(edx);
            a.ret();

            var stream = new MemoryStream();
            var writer = new StreamCodeWriter(stream);
            a.Assemble(writer, 0);
            var res = stream.ToArray();
            stream.Close();
            return res;
        }

    }
}
