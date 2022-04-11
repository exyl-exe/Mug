using System;

namespace Mug.Tracks
{
    public enum InputKey { Spacebar, UpArrow }

    public enum InputType { Press, Release }

    public class MugInput
    {
        public const int SIZE_BYTES = sizeof(int)+sizeof(byte)+sizeof(float);
        public const int INPUT_TYPE_OFFSET = sizeof(int);
        public const int POS_OFFSET = INPUT_TYPE_OFFSET + sizeof(byte);

        public const byte RELEASE_BYTE_VALUE = 0x0;
        public const byte PRESS_BYTE_VALUE = 0x1;

        public const InputKey DEFAULT_KEY = InputKey.Spacebar;

        public InputKey key;
        public InputType type;
        public int frame;
        public float position;

        public MugInput() {
            key = DEFAULT_KEY;
            type = InputType.Press;
            frame = 0;
            position = 0;
        }

        public byte[] ToBytes()
        {
            var res = new byte[SIZE_BYTES];
            var frameBytes = BitConverter.GetBytes(frame);
            Array.Copy(frameBytes, res, sizeof(int));
            var typeByte = new byte[] { (type == InputType.Press) ? PRESS_BYTE_VALUE : RELEASE_BYTE_VALUE };
            Array.Copy(typeByte, 0, res, INPUT_TYPE_OFFSET, sizeof(byte));
            var posBytes = BitConverter.GetBytes(position);
            Array.Copy(posBytes, 0, res, POS_OFFSET, sizeof(float));
            return res;
        }

        public void LoadFromBytes(byte[] inputAsBytes)
        {
            key = DEFAULT_KEY;
            frame = BitConverter.ToInt32(inputAsBytes, 0);
            type = inputAsBytes[INPUT_TYPE_OFFSET] == RELEASE_BYTE_VALUE ? InputType.Release : InputType.Press;
            position = BitConverter.ToSingle(inputAsBytes, POS_OFFSET);
        }
    }
}
